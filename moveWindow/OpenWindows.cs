using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;



namespace moveWindow
{
    class OpenWindows
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetShellWindow();

        [DllImport("USER32.DLL")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        public static class OpenWindowsGetter
        {
            /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
            /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
            public static IDictionary<IntPtr, string> GetOpenedWindows()
            {
                IntPtr shellWindow = GetShellWindow();
                Dictionary<IntPtr, string> windows = new Dictionary<IntPtr, string>();

                EnumWindows(new EnumWindowsProc(delegate (IntPtr hWnd, int lParam)
                {
                    if (hWnd == shellWindow) return true;
                    if (!IsWindowVisible(hWnd)) return true;
                    int length = GetWindowTextLength(hWnd);
                    if (length == 0) return true;
                    StringBuilder builder = new StringBuilder(length);
                    GetWindowText(hWnd, builder, length + 1);
                    windows[hWnd] = builder.ToString();
                    return true;
                }), 0);
                return windows;
            }
        }

        public static void XMain()
        {
            foreach (KeyValuePair<IntPtr, string> window in OpenWindowsGetter.GetOpenedWindows())
            {
                IntPtr handle = window.Key;
                string title = window.Value;
                Console.WriteLine("{0}: {1}", handle, title);
            }
            Console.ReadKey();
        }
    }
}
