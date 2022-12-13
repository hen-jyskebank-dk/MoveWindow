using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using System.Text;

namespace moveWindow
{
    class Program
    {
        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);
        
        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, uint Msg);


        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        const int SWF_SynchronousWindowPosition = 0x4000;
        const int SWF_DeferErase = 0x2000;
        const int SWF_DrawFrame = 0x0020;
        const int SWF_FrameChanged = 0x0020;
        const int SWF_HideWindow = 0x0080;
        const int SWF_DoNotActivate = 0x0010;
        const int SWF_DoNotCopyBits = 0x0100;
        const int SWF_IgnoreMove = 0x0002;
        const int SWF_DoNotChangeOwnerZOrder = 0x0200;
        const int SWF_DoNotRedraw = 0x0008;
        const int SWF_DoNotReposition = 0x0200;
        const int SWF_DoNotSendChangingEvent = 0x0400;
        const int SWF_IgnoreResize = 0x0001;
        const int SWF_IgnoreZOrder = 0x0004;
        const int SWF_ShowWindow = 0x0040;

        const int SW_Hide = 0;
        const int SW_ShowNormal = 1;
        const int SW_ShowMinimized = 2;
        const int SW_ShowMaximized = 3;
        const int SW_Maximize = 3;
        const int SW_ShowNormalNoActivate = 4;
        const int SW_Show = 5;
        const int SW_Minimize = 6;
        const int SW_ShowMinNoActivate = 7;
        const int SW_ShowNoActivate = 8;
        const int SW_Restore = 9;
        const int SW_ShowDefault = 10;
        const int SW_ForceMinimized = 11;

        static public Logger dlog = null;

        static void Main(string[] args)
        {
            dlog = new Logger(Environment.GetEnvironmentVariable("userprofile")+@"\movewindow.log");

            dlog.log(string.Format("Main: Commandline \"{0}\"", Environment.CommandLine));

            String[] windowNames;

            if (args.Length > 0)
            {
                string cmd;
                string activeProfile = "0";
                cmd = args[0].ToUpper();
                if (args.Length == 2)
                    activeProfile = args[1];
                
                windowNames = GetWindowNames(activeProfile);

                switch (cmd)
                {
                    case "ADD":    CommandAdd(activeProfile);
                        break;
                    case "DELETE": CommandDelete(windowNames, activeProfile);
                        break;
                    case "LIST":   CommandList(windowNames);
                        break;
                    case "QUERY":  CommandQuery();
                        break;
                    case "SAVE":   CommandSave(windowNames, activeProfile);
                        break;
                    case "RESTORE":CommandRestore(windowNames, activeProfile);
                        break;
                    case "FIXLID": CommandFixLid();
                        break;
                    case "TEST":   CommandTest();
                        break;
                    default:       CommandHelp();
                        break;
                }
            }
            else
            {
                Console.WriteLine("MOVEWINDOW HELP for instructions");
            }
           // Console.ReadKey();
        }

        private static void CommandTest()
        {
            OpenWindows.XMain();
        }

        private static void CommandFixLid()
        {
            throw new NotImplementedException();
            // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration\NOEDID
        }

        private static void CommandHelp()
        {
            Console.WriteLine("MOVEWINDOW [QUERY|ADD|DELETE|LIST|SAVE|RESTORE] <PROFILE>");
            Console.WriteLine("QUERY:   Shows a list if active windows, that has a title");
            Console.WriteLine("ADD:     Add a part of a Window Title to the list of managed windows titles. No position is stored");
            Console.WriteLine("DELETE:  Remove a Window Title from the list of managed windows");
            Console.WriteLine("LIST:    List Windows titles that are managed");
            Console.WriteLine("SAVE:    Saves the positions of all managed windows");
            Console.WriteLine("RESTORE: Restores the positions of all managed windows to the position of the last save operation");
            Console.WriteLine("");
            Console.WriteLine("PROFILE: Profilenumber (0=default)");
            Console.WriteLine("");
        }

        private static void CommandList(string[] windowNames)
        {
            Console.WriteLine("Managed window titles:");
            foreach (string windowName in windowNames)
                Console.WriteLine("   - \"{0}\"", windowName);
        }

        private static void CommandAdd(string activeProfile)
        {
            Console.Write("Enter part of title: ");
            String nameAdd = Console.ReadLine();
            AddNewWindowName(nameAdd, activeProfile);
        }

        private static void CommandDelete(string[] windowNames, string activeProfile)
        {
            foreach (string windowName in windowNames)
                Console.WriteLine("{0}", windowName);
            Console.Write("Enter window name to delete: ");
            string nameDelete = Console.ReadLine();
            foreach (string windowName in windowNames)
                if (windowName == nameDelete)
                {
                    DeleteWindowName(nameDelete, activeProfile);
                    Console.WriteLine("{0} deleted", nameDelete);
                }
        }

        private static void CommandRestore(string[] windowNames, string activeProfile)
        {
            foreach (string windowName in windowNames)
            {
                IntPtr handle = FindWindowHandle(windowName, activeProfile);
                if (handle!=IntPtr.Zero)
                {
                    ShowWindow(handle, SW_ShowNormal);
                    ShowWindow(handle, SW_Restore);
                    RestoreWindowPosition(windowName, handle, out int Top, out int Left, out int Bottom, out int Right, activeProfile);
                    Console.WriteLine("Restored {0} Coordinates {1},{2},{3},{4}", windowName, Top, Left, Bottom, Right);
                }
            }
        }

        private static String GetProcessFilename(Process p)  // Used instead of process.MainModule.FileName to avoid access denied
        {
            int capacity = 2000;
            StringBuilder builder = new StringBuilder(capacity);
            IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
            if (!QueryFullProcessImageName(ptr, 0, builder, ref capacity))
            {
                return String.Empty;
            }
            return builder.ToString();
        }

        private static IntPtr FindWindowHandle(string windowName, string profileNumber)
        {
            
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            key = key.OpenSubKey("MoveWindow - profile " + profileNumber, true);
            key = key.OpenSubKey(windowName, true);
            string fileName = key.GetValue("Filename").ToString();
            
            IntPtr handle = IntPtr.Zero;
            Process[] processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                string f = GetProcessFilename(process);
                if (f == fileName)
                    if (process.MainWindowHandle != IntPtr.Zero)
                        handle = process.MainWindowHandle;
                /*
                if (process.MainWindowTitle.Contains(windowName))
                    if (process.MainWindowHandle != IntPtr.Zero)
                        handle = process.MainWindowHandle;
                */
            }
            return handle;
        }

        private static void CommandSave(string[] windowNames, string activeProfile)
        {
            IntPtr handle;
            foreach (string windowName in windowNames)
            {
                string filename = FindWindowExecutable(windowName, out handle);
                if (filename != "")
                {
                    Rect WindowRect = new Rect();
                    GetWindowRect(handle, ref WindowRect);
                    WindowRect = SaveWindowPosition(windowName, filename, WindowRect, activeProfile);
                    Console.WriteLine("Saved {0} Coordinates {1},{2},{3},{4}\nFilename {5}", windowName, WindowRect.Top, WindowRect.Left, WindowRect.Bottom, WindowRect.Right, filename);
                }
            }
        }

        private static string FindWindowExecutable(string windowTitle, out IntPtr handle)
        {
            string FileName = "";
            handle = IntPtr.Zero;
            Process[] processes = Process.GetProcesses();
            foreach (var process in processes)
                if (process.MainWindowTitle.Contains(windowTitle))
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        FileName = process.MainModule.FileName;
                        handle = process.MainWindowHandle;
                    }
             return FileName;
        }
                
        private static void CommandQuery()
        {
            Process[] processes = Process.GetProcesses();
            foreach (var process in processes)
                if (process.MainWindowTitle.Trim().Length > 0)
                    Console.WriteLine("Windowtitle: {0} ", process.MainWindowTitle);
        }

        private static string[] GetWindowNames(string profileNumber)
        {
            string[] windows = new string[] { "" };
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey("Software", true);
                keys = keys.OpenSubKey("MoveWindow - profile " + profileNumber, true);
                windows = keys.GetSubKeyNames();
                dlog.log(string.Format("GetWindowNames: key \"{0}\"", keys.Name));
                foreach(string window in windows)
                    dlog.log(string.Format("GetWindowNames: window \"{0}\"", window));
            }
            catch (Exception ex)
            {
                dlog.log(string.Format("GetWindowNames: Exception \"{0}\"", ex.Message));
            }
            return windows;
        }

        private static void RestoreWindowPosition(string window, IntPtr handle, out int Top, out int Left, out int Bottom, out int Right, string profileNumber)
        {
            bool fail = false;
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            key = key.OpenSubKey("MoveWindow - profile " + profileNumber, true);
            key = key.OpenSubKey(window, true);
            dlog.log(string.Format("RestoreWindowPosition: key \"{0}\"", key.Name));
            try
            {
                Top = int.Parse(key.GetValue("Top").ToString());
                Left = int.Parse(key.GetValue("Left").ToString());
                Bottom = int.Parse(key.GetValue("Bottom").ToString());
                Right = int.Parse(key.GetValue("Right").ToString());
            }
            catch (Exception ex)
            {
                dlog.log(string.Format("RestoreWindowPosition: Exception \"{0}\"", ex.Message));
                Top = 0;
                Left = 0;
                Bottom = 0;
                Right = 0;
                fail = true;
            }
            if (!fail)
                SetWindowPos(handle, 0, Left, Top, Right-Left, Bottom-Top, SWF_IgnoreZOrder | SWF_ShowWindow);
            dlog.log(string.Format("RestoreWindowPosition: koordinates ({0},{1}),({2},{3})", Left.ToString(), Top.ToString(), Right.ToString(), Bottom.ToString()));
        }

        private static Rect SaveWindowPosition(string window, string filename, Rect WindowRect, string profileNumber)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            key.CreateSubKey("MoveWindow - profile " + profileNumber);
            key = key.OpenSubKey("MoveWindow - profile " + profileNumber, true);
            key.CreateSubKey(window);
            key = key.OpenSubKey(window, true);
            key.SetValue("Top", WindowRect.Top);
            key.SetValue("Left", WindowRect.Left);
            key.SetValue("Bottom", WindowRect.Bottom);
            key.SetValue("Right", WindowRect.Right);
            key.SetValue("Filename", filename);
            dlog.log(string.Format("SaveWindowPosition: key \"{0}\"", key.Name));
            return WindowRect;
        }

        private static void AddNewWindowName(string name, string profileNumber)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            key.CreateSubKey("MoveWindow - profile " + profileNumber);
            key = key.OpenSubKey("MoveWindow - profile " + profileNumber, true);
            key.CreateSubKey(name);
            key = key.OpenSubKey(name, true);
            dlog.log(string.Format("AddNewWindowName: key \"{0}\"",key.Name));
        }

        private static void DeleteWindowName(string name, string profileNumber)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            key = key.OpenSubKey("MoveWindow - profile " + profileNumber, true);
            key.DeleteSubKeyTree(name);
            dlog.log(string.Format("DeleteWindowName: key \"{0}\"", key.Name));
        }
    }
}



/*
 * https://stackoverflow.com/questions/7268302/get-the-titles-of-all-open-windows?utm_medium=organic&utm_source=google_rich_qa&utm_campaign=google_rich_qa
 * 
 * 
 * Virtual desktops:
 * https://stackoverflow.com/questions/32416843/programmatic-control-of-virtual-desktops-in-windows-10
 * https://github.com/Grabacr07/VirtualDesktop/tree/1e7f823076f090f90d35024e5592fbbff4614afc
 * 
 * 
 * 
 * 
 */
