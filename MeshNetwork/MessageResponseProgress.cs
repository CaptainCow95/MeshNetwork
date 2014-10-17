namespace MeshNetwork
{
    /// <summary>
    /// Represents the progress in sending a message and waiting for a response.
    /// </summary>
    public enum MessageResponseProgress
    {
        /// <summary>
        /// The message is being sent.
        /// </summary>
        SendingMessage = 0,

        /// <summary>
        /// The message has been sent and we are waiting for the response.
        /// </summary>
        WaitingForResponse = 1,

        /// <summary>
        /// The message and response have completed transmission, but this does not mean that they succeeded.
        /// </summary>
        Completed = 2
    }
}