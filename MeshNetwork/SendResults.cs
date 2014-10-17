namespace MeshNetwork
{
    /// <summary>
    /// The results of sending a message.
    /// </summary>
    public enum SendResults
    {
        /// <summary>
        /// The message was sent successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The message failed to send due to a connection problem.
        /// </summary>
        ConnectionFailure,

        /// <summary>
        /// The message failed to send because it was trying to be sent to itself.
        /// </summary>
        SendingToSelfFailure,
    }
}