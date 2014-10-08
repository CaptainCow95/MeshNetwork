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

        private uint? _messageId;

        /// <summary>
        /// The sender of the message.
        /// </summary>
        private NodeProperties _sender;

        /// <summary>
        /// The type of the message.
        /// </summary>
        private MessageType _type;

        private bool _waitingForResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalMessage" /> class.
        /// </summary>
        private InternalMessage()
        {
        }

        /// <summary>
        /// Gets the data that the message contains.
        /// </summary>
        public string Data
        {
            get { return _data; }
        }

        public uint? MessageId
        {
            get { return _messageId; }
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

        public bool WaitingForResponse
        {
            get { return _waitingForResponse; }
        }

        /// <summary>
        /// Creates a message from the various parts.
        /// </summary>
        /// <param name="sendingPort">The port the message is being sent from.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data to go along with the message.</param>
        /// <param name="waitForResponse">Whether this message is waiting for a response.</param>
        /// <param name="messageId">The id of the message if it has one.</param>
        /// <returns>The compoosed message to be sent over the wire to the recieving node.</returns>
        public static string CreateMessage(int sendingPort, MessageType type, string data, bool waitForResponse = false, uint? messageId = null)
        {
            char typeChar;
            switch (type)
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
            if (waitForResponse)
            {
                responseString = "t" + messageId;
            }
            else
            {
                responseString = "f" + messageId;
            }

            string portString = sendingPort + ":";

            int length = data.Length + responseString.Length + 1 /* typeChar */ + portString.Length;

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

            return length.ToString(CultureInfo.InvariantCulture) + responseString + typeChar + portString + data;
        }

        /// <summary>
        /// Creates a message object from a message that was recieved from another node.
        /// </summary>
        /// <param name="rawMessage">The raw message as it was recieved.</param>
        /// <param name="sender">The sender of the message.</param>
        public static InternalMessage Parse(string rawMessage, NodeProperties sender)
        {
            var message = new InternalMessage();

            int index = 0;
            do
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    switch (rawMessage[index])
                    {
                        case 'f':
                            message._waitingForResponse = false;
                            message._messageId = 0;
                            break;

                        case 't':
                            message._waitingForResponse = true;
                            break;
                    }

                    break;
                }

                ++index;
            } while (index < rawMessage.Length);

            ++index;

            uint messageId = 0;
            do
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    message._messageId = messageId;
                    break;
                }

                messageId *= 10;
                messageId += (uint)char.GetNumericValue(rawMessage[index]);
                ++index;
            } while (index < rawMessage.Length);

            do
            {
                if (!char.IsDigit(rawMessage[index]))
                {
                    switch (rawMessage[index])
                    {
                        case 'n':
                            message._type = MessageType.Neighbors;
                            break;

                        case 'p':
                            message._type = MessageType.Ping;
                            break;

                        case 'u':
                            message._type = MessageType.User;
                            break;

                        default:
                            message._type = MessageType.Unknown;
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
                    message._sender = new NodeProperties(sender.IpAddress, senderPort);
                    message._data = rawMessage.Substring(index + 1, rawMessage.Length - (index + 1));
                    break;
                }

                senderPort *= 10;
                senderPort += (int)char.GetNumericValue(rawMessage[index]);
                ++index;
            } while (index < rawMessage.Length);

            return message;
        }

        /// <inheritdoc></inheritdoc>
        public override string ToString()
        {
            return "Type: " + Enum.GetName(typeof(MessageType), _type) + " Sender: " + _sender.IpAddress + ":" + _sender.Port + " Data: " + _data;
        }
    }
}