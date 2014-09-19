using System;

namespace MeshNetwork
{
    internal class Message
    {
        private string _data;
        private NodeProperties _sender;
        private MessageType _type;

        public Message(string rawMessage, NodeProperties sender)
        {
            int index = 0;
            do
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    switch (rawMessage[index])
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

                    break;
                }

                ++index;
            } while (index < rawMessage.Length);

            ++index;

            int senderPort = 0;
            do
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    _sender = new NodeProperties(sender.IP, senderPort);
                    _data = rawMessage.Substring(index + 1, rawMessage.Length - (index + 1));
                    break;
                }
                else
                {
                    senderPort *= 10;
                    senderPort += (int)char.GetNumericValue(rawMessage[index]);
                }

                ++index;
            } while (index < rawMessage.Length);
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

        public override string ToString()
        {
            return "Type: " + Enum.GetName(typeof(MessageType), _type) + " Sender: " + _sender.IP + ":" + _sender.Port + " Data: " + _data;
        }
    }
}