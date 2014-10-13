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
        /// The highest level at which log messages are written.
        /// </summary>
        private static LogLevels _logLevel;

        /// <summary>
        /// Gets or sets the highest level at which log messages are written.
        /// </summary>
        public static LogLevels LogLevel
        {
            get
            {
                return _logLevel;
            }

            set
            {
                _logLevel = value;
            }
        }

        /// <summary>
        /// Initializes the file to be written to.
        /// </summary>
        /// <param name="filename">The filename to write to.</param>
        /// <param name="logLevel">The highest level at which log messages are written.</param>
        public static void Init(string filename, LogLevels logLevel)
        {
            _filename = filename;
            _logLevel = logLevel;
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="level">The level of severity of the log message.</param>
        public static void Write(string message, LogLevels level)
        {
            if (_logLevel < level)
            {
                // This message is more detailed than the user wants logged.
                return;
            }

            if (string.IsNullOrEmpty(_filename))
            {
                return;
            }

            lock (LockObject)
            {
                using (var file = new StreamWriter(_filename, true))
                {
                    file.WriteLine(string.Concat(DateTime.UtcNow.ToString("M/d/yyyy HH:mm:ss"), " Level ", Enum.GetName(typeof(LogLevels), level), ": ", message));
                }
            }
        }
    }
}