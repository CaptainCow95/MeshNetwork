using System;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a message sent across the network.
    /// </summary>
    internal class Message
    {
        /// <summary>
        /// The data that the message contains.
        /// </summary>
        private string _data;

        /// <summary>
        /// The sender of the message.
        /// </summary>
        private NodeProperties _sender;

        /// <summary>
        /// The type of the message.
        /// </summary>
        private MessageType _type;

        /// <summary>
        /// Initializes a new instance of the <see cref="Message" /> class and parses a message into
        /// its parts.
        /// </summary>
        /// <param name="rawMessage">The raw message as it was recieved.</param>
        /// <param name="sender">The sender of the message.</param>
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

        /// <summary>
        /// Gets the data that the message contains.
        /// </summary>
        public string Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Gets the sender of the message.
        /// </summary>
        public NodeProperties Sender
        {
            get { return _sender; }
        }

        /// <summary>
        /// Gets the type of the message.
        /// </summary>
        public MessageType Type
        {
            get { return _type; }
        }

        /// <inheritdoc></inheritdoc>
        public override string ToString()
        {
            return "Type: " + Enum.GetName(typeof(MessageType), _type) + " Sender: " + _sender.IP + ":" + _sender.Port + " Data: " + _data;
        }
    }
}