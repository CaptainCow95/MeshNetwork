using System;
using System.IO;

namespace MeshNetwork
{
    /// <summary>
    /// A class to log things to a file.
    /// </summary>
    internal static class Logger
    {
        /// <summary>
        /// The object to lock on.
        /// </summary>
        private static readonly object LockObject = new object();

        /// <summary>
        /// The current file being logged to.
        /// </summary>
        private static string _filename;

        /// <summary>
        /// Initializes the file to be written to.
        /// </summary>
        /// <param name="filename">The filename to write to.</param>
        public static void Init(string filename)
        {
            _filename = filename;
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(_filename))
            {
                return;
            }

            lock (LockObject)
            {
                using (var file = new StreamWriter(_filename, true))
                {
                    file.WriteLine(string.Concat(DateTime.UtcNow.ToString("M/d/yyyy HH:mm:ss"), ": ", message));
                }
            }
        }
    }
}