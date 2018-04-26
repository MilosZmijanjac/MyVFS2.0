using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using System.Text.RegularExpressions;

namespace MyVFS
{
    class MyVFS : IDokanOperations
    {
        class Time { public DateTime created, accessed, modified; public Time() { created = accessed = modified = DateTime.Now; } }
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        Dictionary<string, Time> times = new Dictionary<string, Time>();
        HashSet<string> dirs = new HashSet<string>();

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            if (info.DeleteOnClose == true)

                if (info.IsDirectory)
                    dirs.Remove(fileName);
                else
                    files.Remove(fileName);
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            info.Context = null;

        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (Path.GetFileNameWithoutExtension(fileName).Length > 25)
                return NtStatus.Error;

            if (fileName == "\\")
            {
                dirs.Add(fileName);
                if (!times.ContainsKey(fileName))
                    times.Add(fileName, new Time());
                return NtStatus.Success;
            }
            if (access == DokanNet.FileAccess.ReadAttributes && mode == FileMode.Open)
            { return NtStatus.Error; }
            if (share == FileShare.Delete)
            { return NtStatus.Success; }
            if (fileName.ToCharArray().Count(c => c == '\\') > 12)
            { return NtStatus.Success; }
            if (mode == FileMode.CreateNew)
            {
                if (attributes == FileAttributes.Directory || info.IsDirectory)
                    dirs.Add(fileName);
                else if (!files.Keys.Contains(fileName))
                    files.Add(fileName, new byte[0]);
                else
                    files.Add(fileName = (fileName + (int.Parse(fileName) + 1).ToString()), new byte[0]);
                if (!times.ContainsKey(fileName))
                    times.Add(fileName, new Time());
            }
            if (mode == FileMode.Open)
            {
                if (files.Keys.Contains(fileName) || (info.IsDirectory = dirs.Contains(fileName)))
                {
                    info.Context = ModuleHandle.EmptyHandle;
                }
                else return NtStatus.Error;
            }
            if (mode == FileMode.Truncate)
                if (!(files.Keys.Contains(fileName) || (info.IsDirectory = dirs.Contains(fileName))))
                    return NtStatus.Error;

            return NtStatus.Success;
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            if (dirs.Contains(fileName))
                return NtStatus.Success;
            else
                return NtStatus.ObjectPathNotFound;
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            if (files.ContainsKey(fileName))
                return NtStatus.Success;
            else
                return NtStatus.Error;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            files = new List<FileInformation>();
            int pathCount = fileName.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Length;
            foreach (var x in dirs.Where(x => x.StartsWith(fileName) && x.Length > fileName.Length))
            {
                int pathCountX = x.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (pathCount == (pathCountX - 1))
                {
                    FileInformation fileInfo = new FileInformation();
                    fileInfo.Attributes = FileAttributes.Directory;
                    fileInfo.FileName = Path.GetFileName(x);
                    fileInfo.CreationTime = times[x].created;
                    fileInfo.LastAccessTime = times[x].accessed;
                    fileInfo.LastWriteTime = times[x].modified;
                    files.Add(fileInfo);
                }
            }
            foreach (var x in this.files.Where(x => x.Key.StartsWith(fileName) && x.Key.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Length == pathCount + 1))
            {
                FileInformation fileInfo = new FileInformation();
                fileInfo.FileName = Path.GetFileName(x.Key);
                fileInfo.Length = x.Value.Length;
                fileInfo.CreationTime = times[x.Key].created;
                fileInfo.LastAccessTime = times[x.Key].accessed;
                fileInfo.LastWriteTime = times[x.Key].modified;
                files.Add(fileInfo);
            }
            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {

            files = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            return NtStatus.Success;
        }
        private long storedSize()
        {
            long size = 0;
            foreach (var x in files.Keys)
                size += files[x].LongLength;
            return size;

        }
        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            totalNumberOfBytes = 536_870_912; // size of a drive in Byte (512 MB)
            freeBytesAvailable = totalNumberOfBytes - storedSize();
            totalNumberOfFreeBytes = freeBytesAvailable;
            return NtStatus.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            if (dirs.Contains(fileName))
            {
                fileInfo = new FileInformation()
                {
                    FileName = Path.GetFileName(fileName),
                    Attributes = FileAttributes.Directory,
                    CreationTime = times[fileName].created,
                    LastWriteTime = times[fileName].modified,
                    LastAccessTime = times[fileName].accessed
                };
            }
            else if (files.ContainsKey(fileName))
            {


                byte[] file = files[fileName];
                fileInfo = new FileInformation()
                {
                    FileName = Path.GetFileName(fileName),
                    Length = file.Length,
                    Attributes = FileAttributes.Normal,
                    CreationTime = times[fileName].created,
                    LastWriteTime = times[fileName].modified,
                    LastAccessTime = times[fileName].accessed
                };
            }
            else
            {
                fileInfo = default(FileInformation);
                return NtStatus.Error;
            }
            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            security = null;
            return NtStatus.Error;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = "VFS-LAB2";
            features = FileSystemFeatures.SupportsTransactions;
            fileSystemName = "NTFS";
            return NtStatus.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            return NtStatus.Error;
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        private int filesInFolder(string fileName)
        {
            int i = 0;
            foreach (var x in files.Keys)
                if (Path.GetDirectoryName(x) == Path.GetDirectoryName(fileName))
                    i++;
            return i;
        }
        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {

            if (info.IsDirectory)
            {   dirs.Remove(oldName);
                dirs.Add(newName);
                times.Add(newName, times[oldName]);
                times.Remove(oldName);
                return NtStatus.Success; }

            if (!files.Keys.Contains(newName))
            {
                info.Context = null;
                files.Add(newName, files[oldName]);
                files.Remove(oldName);
                times.Add(newName, times[oldName]); times.Remove(oldName);
                return DokanResult.Success;
            }
            else if (replace)
            {
                info.Context = null;
                if (!info.IsDirectory)
                {
                    files.Remove(newName);
                    files.Add(newName, files[oldName]);
                    files.Remove(oldName);
                    times.Add(newName, times[oldName]); times.Remove(oldName);
                }
                return DokanResult.Success;
            }
            return NtStatus.Error;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            bytesRead = (int)(buffer.Length < (files[fileName].Length - offset) ? buffer.Length : (files[fileName].Length - offset));

            Array.Copy(files[fileName], offset, buffer, 0, bytesRead);

            return NtStatus.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            return NtStatus.Error;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            try
            {
                byte[] file = files[fileName];
                Array.Resize(ref file, (int)length);
                files[fileName] = file;
                return NtStatus.Success;
            }
            catch (Exception)
            {
                return NtStatus.Error;
            }

        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {

            return NtStatus.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return NtStatus.Error;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            times[fileName].accessed = lastAccessTime ?? times[fileName].accessed;
            times[fileName].created = creationTime ?? times[fileName].created;
            times[fileName].modified = lastWriteTime ?? times[fileName].modified;
            return NtStatus.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            return NtStatus.Error;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {

            if (filesInFolder(fileName) > 16)
            { bytesWritten = 0; return NtStatus.BadFileType; }
            if (!Regex.IsMatch(fileName, "\\.[0-9a-z]{3}$"))
            { bytesWritten = 0; return NtStatus.BadFileType; }
            if (files[fileName].Length > 32 * 1024 * 1024)
            { bytesWritten = 0; DeleteFile(fileName, info); return NtStatus.BadFileType; } 


            bytesWritten = (int)(buffer.Length < (files[fileName].Length - offset) ? buffer.Length : (files[fileName].Length - offset));

            Array.Copy(buffer, 0, files[fileName], offset, bytesWritten);

            return NtStatus.Success;
        }
    }
}
