namespace MeshNetwork
{
    /// <summary>
    /// Represents a received message.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// A value indicating whether the sender is awaiting a response.
        /// </summary>
        private readonly bool _awaitingResponse;

        /// <summary>
        /// The data contained in this message.
        /// </summary>
        private readonly string _data;

        /// <summary>
        /// A value indicating whether this message is in response to another.
        /// </summary>
        private readonly bool _inResponseToMessage;

        /// <summary>
        /// The id of this message.
        /// </summary>
        private readonly uint _messageId;

        /// <summary>
        /// The sender of this message.
        /// </summary>
        private readonly NodeProperties _sender;

        /// <summary>
        /// Initializes a new instance of the <see cref="Message" /> class.
        /// </summary>
        /// <param name="sender">The sender of this message.</param>
        /// <param name="data">The data contained in this message.</param>
        /// <param name="messageId">The id of this message.</param>
        /// <param name="awaitingResponse">
        /// A value indicating whether the sender is awaiting a response.
        /// </param>
        /// <param name="inResponseToMessage">
        /// A value indicating whether this message is in response to another.
        /// </param>
        internal Message(NodeProperties sender, string data, uint messageId, bool awaitingResponse, bool inResponseToMessage)
        {
            _sender = sender;
            _data = data;
            _messageId = messageId;
            _awaitingResponse = awaitingResponse;
            _inResponseToMessage = inResponseToMessage;
        }

        /// <summary>
        /// Gets a value indicating whether the sender is awaiting a response.
        /// </summary>
        public bool AwaitingResponse
        {
            get { return _awaitingResponse; }
        }

        /// <summary>
        /// Gets the data contained in this message.
        /// </summary>
        public string Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Gets a value indicating whether this message is in response to another.
        /// </summary>
        public bool InResponseToMessage
        {
            get { return _inResponseToMessage; }
        }

        /// <summary>
        /// Gets the sender of this message.
        /// </summary>
        public NodeProperties Sender
        {
            get { return _sender; }
        }

        /// <summary>
        /// Gets the id of this message.
        /// </summary>
        internal uint MessageId
        {
            get { return _messageId; }
        }
    }
}