using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
/*using System.Runtime.InteropServices;
using System.Diagnostics;*/

namespace MyVFS
{
    class Program
    {
        /*[DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);*/
        static void Main(string[] args)
        {
            try
            {
               /* IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
                ShowWindow(h, 0);*/
                MyVFS fs = new MyVFS();
                fs.Mount("r:\\", DokanOptions.DebugMode | DokanOptions.StderrOutput);

                Console.WriteLine(@"Success");
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
        }
    }
}
