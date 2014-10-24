using System;
using System.IO;

namespace MeshNetwork
{
    /// <summary>
    /// A class to log things to a file.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// The object to lock on.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// The current file being logged to.
        /// </summary>
        private string _filename;

        /// <summary>
        /// The highest level at which log messages are written.
        /// </summary>
        private LogLevels _logLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger" /> class.
        /// </summary>
        /// <param name="filename">The filename to write to.</param>
        /// <param name="logLevel">The highest level at which log messages are written.</param>
        public Logger(string filename, LogLevels logLevel)
        {
            _filename = filename;
            _logLevel = logLevel;
        }

        /// <summary>
        /// Gets or sets the filename of the log file, an empty string turns off logging.
        /// </summary>
        public string Filename
        {
            get
            {
                return _filename;
            }

            set
            {
                lock (_lockObject)
                {
                    _filename = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the highest level at which log messages are written.
        /// </summary>
        public LogLevels LogLevel
        {
            get
            {
                return _logLevel;
            }

            set
            {
                lock (_lockObject)
                {
                    _logLevel = value;
                }
            }
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="level">The level of severity of the log message.</param>
        public void Write(string message, LogLevels level)
        {
            lock (_lockObject)
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

                using (var file = new StreamWriter(_filename, true))
                {
                    file.WriteLine(
                        string.Concat(
                            DateTime.UtcNow.ToString("M/d/yyyy HH:mm:ss"),
                            " Level ",
                            Enum.GetName(typeof(LogLevels), level),
                            ": ",
                            message));
                }
            }
        }
    }
}