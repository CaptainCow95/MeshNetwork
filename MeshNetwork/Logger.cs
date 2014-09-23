using System;
using System.IO;

namespace MeshNetwork
{
    internal static class Logger
    {
        private static string _filename;
        private static object _lockObject = new object();

        public static void Init(string filename)
        {
            _filename = filename;
        }

        public static void Write(string message)
        {
            if (!string.IsNullOrEmpty(_filename))
            {
                lock (_lockObject)
                {
                    using (StreamWriter file = new StreamWriter(_filename, true))
                    {
                        file.WriteLine(String.Concat(DateTime.UtcNow.ToString("M/d/yyyy HH:mm:ss"), ": ", message));
                    }
                }
            }
        }
    }
}