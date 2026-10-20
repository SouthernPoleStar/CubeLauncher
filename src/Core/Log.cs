using System;
using System.IO;
using System.Text;

namespace CubeLauncher.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _file;

        public static event Action<string> Message;

        public static string FilePath
        {
            get { return _file; }
        }

        public static void Initialize(string file)
        {
            _file = file;
            try
            {
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(file, "\r\n===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====" + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + (ex == null ? "" : " :: " + ex.Message));
            if (ex != null) Write("TRACE", ex.ToString());
        }

        private static void Write(string level, string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + level + "] " + message;
            lock (Gate)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_file)) File.AppendAllText(_file, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                }
            }

            Action<string> handler = Message;
            if (handler != null)
            {
                try { handler(line); }
                catch { }
            }
        }
    }
}
