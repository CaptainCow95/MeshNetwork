namespace MeshNetwork
{
    public class Message
    {
        private readonly bool _awaitingResponse;
        private readonly string _data;
        private readonly bool _inResponseToMessage;
        private readonly uint _messageId;
        private readonly NodeProperties _sender;

        internal Message(NodeProperties sender, string data, uint messageId, bool awaitingResponse, bool inResponseToMessage)
        {
            _sender = sender;
            _data = data;
            _messageId = messageId;
            _awaitingResponse = awaitingResponse;
            _inResponseToMessage = inResponseToMessage;
        }

        public bool AwaitingResponse
        {
            get { return _awaitingResponse; }
        }

        public string Data
        {
            get { return _data; }
        }

        public bool InResponseToMessage
        {
            get { return _inResponseToMessage; }
        }

        public NodeProperties Sender
        {
            get { return _sender; }
        }

        internal uint MessageId
        {
            get { return _messageId; }
        }
    }
}