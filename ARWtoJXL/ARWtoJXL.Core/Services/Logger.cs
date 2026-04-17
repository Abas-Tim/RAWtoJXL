using System;
using System.IO;

namespace ARWtoJXL.Core.Services
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ARWtoJXL.log");
        private static readonly object LockObj = new();

        public static void Write(string message)
        {
            lock (LockObj)
            {
                try
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
                catch { }
            }
        }

        public static void Clear()
        {
            lock (LockObj)
            {
                try { File.Delete(LogPath); } catch { }
            }
        }
    }
}
