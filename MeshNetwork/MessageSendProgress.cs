namespace MeshNetwork
{
    /// <summary>
    /// Represents the progress in sending a message.
    /// </summary>
    public enum MessageSendProgress
    {
        /// <summary>
        /// The message is being sent.
        /// </summary>
        SendingMessage = 0,

        /// <summary>
        /// The message has completed transmission, but this does not mean it succeeded.
        /// </summary>
        Completed = 1
    }
}