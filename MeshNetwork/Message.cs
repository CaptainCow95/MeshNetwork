namespace MeshNetwork
{
    internal class Message
    {
        private string _data;
        private NodeProperties _sender;
        private MessageType _type;

        public Message(string rawMessage, NodeProperties sender)
        {
            _sender = sender;

            for (int i = 0; i < rawMessage.Length; ++i)
            {
                if (!char.IsDigit(rawMessage[i]))
                {
                    switch (rawMessage[i])
                    {
                        case 'p':
                            _type = MessageType.Ping;
                            break;

                        case 'u':
                            _type = MessageType.User;
                            break;

                        default:
                            _type = MessageType.Unknown;
                            break;
                    }

                    _data = rawMessage.Substring(i + 1, rawMessage.Length - (i + 1));
                    break;
                }
            }
        }

        public string Data
        {
            get { return _data; }
        }

        public NodeProperties Sender
        {
            get { return _sender; }
        }

        public MessageType Type
        {
            get { return _type; }
        }
    }
}