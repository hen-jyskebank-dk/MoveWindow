using System;
using System.IO;

namespace moveWindow
{
    class Logger
    {
        string f = "";

        public Logger(string logfile)
        {
            f = logfile;
        }

        public void log(string msg)
        {
            StreamWriter SW;
            SW = File.AppendText(f);
            try
            {
                SW.WriteLine(DateTime.Now.ToLongTimeString() + " :: " + msg);
            }
            finally
            {
                SW.Close();
            }
        }
    }
}
