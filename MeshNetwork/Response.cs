namespace MeshNetwork
{
    /// <summary>
    /// Represents a response to a sent message.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// A value indicating whether the original message was sent successfully.
        /// </summary>
        private readonly bool _messageSent;

        /// <summary>
        /// The message that was sent in response.
        /// </summary>
        private readonly Message _responseMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="Response" /> class.
        /// </summary>
        /// <param name="messageSent">
        /// A value indicating whether the original message was sent successfully.
        /// </param>
        /// <param name="response">The message that was sent in response.</param>
        internal Response(bool messageSent, Message response)
        {
            _messageSent = messageSent;
            _responseMessage = response;
        }

        /// <summary>
        /// Gets a value indicating whether the original message was sent successfully.
        /// </summary>
        public bool MessageSent
        {
            get { return _messageSent; }
        }

        /// <summary>
        /// Gets the message that was sent in response.
        /// </summary>
        public Message ResponseMessage
        {
            get { return _responseMessage; }
        }
    }
}