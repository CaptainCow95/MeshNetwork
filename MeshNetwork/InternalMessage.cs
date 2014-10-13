using System;
using System.Globalization;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a message sent across the network.
    /// </summary>
    internal class InternalMessage
    {
        /// <summary>
        /// The data that the message contains.
        /// </summary>
        private string _data;

        /// <summary>
        /// The id belonging to this message.
        /// </summary>
        private uint? _messageId;

        /// <summary>
        /// The raw, unparsed message.
        /// </summary>
        private string _rawMessage;

        /// <summary>
        /// The sender of the message.
        /// </summary>
        private NodeProperties _sender;

        /// <summary>
        /// The type of the message.
        /// </summary>
        private MessageType _type;

        /// <summary>
        /// A value indicating whether the sender is waiting for a response to this message.
        /// </summary>
        private bool _waitingForResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalMessage" /> class.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data to go along with the message.</param>
        /// <param name="waitingForResponse">Whether this message is waiting for a response.</param>
        /// <param name="messageId">The id of the message if it has one.</param>
        /// <returns>The composed message to be sent over the wire to the receiving node.</returns>
        public InternalMessage(NodeProperties sender, MessageType type, string data, bool waitingForResponse = false, uint? messageId = null)
        {
            _type = type;
            _sender = sender;
            _data = data;
            _waitingForResponse = waitingForResponse;
            _messageId = messageId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalMessage" /> class.
        /// </summary>
        /// <param name="rawMessage">The message that will be parsed.</param>
        /// <param name="sender">The sender of the message.</param>
        public InternalMessage(string rawMessage, NodeProperties sender)
        {
            _rawMessage = rawMessage;

            int index = 0;
            while (index < rawMessage.Length)
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    switch (rawMessage[index])
                    {
                        case 'f':
                            _waitingForResponse = false;
                            _messageId = 0;
                            break;

                        case 't':
                            _waitingForResponse = true;
                            break;
                    }

                    break;
                }

                ++index;
            }

            ++index;

            uint messageId = 0;
            while (index < rawMessage.Length)
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    _messageId = messageId;
                    break;
                }

                messageId *= 10;
                messageId += (uint)char.GetNumericValue(rawMessage[index]);
                ++index;
            }

            while (index < rawMessage.Length)
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    switch (rawMessage[index])
                    {
                        case 'n':
                            _type = MessageType.Neighbors;
                            break;

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
            }

            ++index;

            int senderPort = 0;
            while (index < rawMessage.Length)
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    _sender = new NodeProperties(sender.IpAddress, senderPort);
                    _data = rawMessage.Substring(index + 1, rawMessage.Length - (index + 1));
                    break;
                }

                senderPort *= 10;
                senderPort += (int)char.GetNumericValue(rawMessage[index]);
                ++index;
            }
        }

        /// <summary>
        /// Gets the data that the message contains.
        /// </summary>
        public string Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Gets the id of this message.
        /// </summary>
        public uint? MessageId
        {
            get { return _messageId; }
        }

        /// <summary>
        /// Gets the raw, unparsed message.
        /// </summary>
        public string RawMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_rawMessage))
                {
                    // Updates the field in the method.
                    GetNetworkString();
                }

                return _rawMessage;
            }
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

        /// <summary>
        /// Gets a value indicating whether the sender is waiting for a response to this message.
        /// </summary>
        public bool WaitingForResponse
        {
            get { return _waitingForResponse; }
        }

        /// <summary>
        /// Gets this message as a string that can be sent over the wire.
        /// </summary>
        /// <returns>This message as a string that can be sent over the wire.</returns>
        public string GetNetworkString()
        {
            char typeChar;
            switch (_type)
            {
                case MessageType.Neighbors:
                    typeChar = 'n';
                    break;

                case MessageType.Ping:
                    typeChar = 'p';
                    break;

                case MessageType.User:
                    typeChar = 'u';
                    break;

                default:
                    return null;
            }

            string responseString;
            if (_waitingForResponse)
            {
                responseString = "t" + _messageId;
            }
            else
            {
                responseString = "f" + _messageId;
            }

            string portString = _sender.Port + ":";

            int length = _data.Length + responseString.Length + 1 /* typeChar */ + portString.Length;

            int magnitude = 0;
            int tempLength = length;
            while (tempLength > 0)
            {
                tempLength /= 10;
                ++magnitude;
            }

            length += magnitude;

            int magnitude2 = 0;
            tempLength = length;
            while (tempLength > 0)
            {
                tempLength /= 10;
                ++magnitude2;
            }

            if (magnitude2 > magnitude)
            {
                ++length;
            }

            _rawMessage = length.ToString(CultureInfo.InvariantCulture) + responseString + typeChar + portString + _data;
            return _rawMessage;
        }

        /// <inheritdoc></inheritdoc>
        public override string ToString()
        {
            string type = "Type: " + Enum.GetName(typeof(MessageType), _type);
            string messageId = "Message ID: " + _messageId;
            string sender = "Sender: " + _sender;
            string data = "Data: " + _data;
            string waitingForResponse = "Waiting for Response: " + _waitingForResponse.ToString();
            string rawMessage = "Raw Message: " + _rawMessage;
            if (_waitingForResponse)
            {
                return type + ' ' + waitingForResponse + ' ' + messageId + ' ' + sender + ' ' + data + ' ' + rawMessage;
            }

            return type + ' ' + waitingForResponse + ' ' + sender + ' ' + data + ' ' + rawMessage;
        }
    }
}