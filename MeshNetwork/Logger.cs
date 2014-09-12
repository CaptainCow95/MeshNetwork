using System;
using System.IO;

namespace MeshNetwork
{
    internal class Logger
    {
        private string _filename;

        public Logger(string filename)
        {
            _filename = filename;
        }

        public void Write(string message)
        {
            using (StreamWriter file = new StreamWriter(_filename, true))
            {
                file.WriteLine(String.Concat(DateTime.UtcNow.ToString("M/d/yyyy HH:mm:ss"), ": ", message));
            }
        }
    }
}