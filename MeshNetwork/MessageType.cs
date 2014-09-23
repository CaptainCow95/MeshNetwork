namespace MeshNetwork
{
    /// <summary>
    /// An enumeration of the different types of messages.
    /// </summary>
    internal enum MessageType
    {
        /// <summary>
        /// A message used as a ping.
        /// </summary>
        Ping,

        /// <summary>
        /// A message that the user has sent.
        /// </summary>
        User,

        /// <summary>
        /// An unknown type of message.
        /// </summary>
        Unknown,
    }
}