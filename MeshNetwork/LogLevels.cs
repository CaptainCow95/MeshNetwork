namespace MeshNetwork
{
    /// <summary>
    /// Represents the different levels of logging.
    /// </summary>
    public enum LogLevels
    {
        /// <summary>
        /// Any log message rated error will be logged.
        /// </summary>
        Error = 0,

        /// <summary>
        /// Any log message rated error or warning will be logged.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Any log message rated error, warning, or info will be logged.
        /// </summary>
        Info = 2,

        /// <summary>
        /// Any log message rated error, warning, info, or debug will be logged.
        /// </summary>
        Debug = 3
    }
}