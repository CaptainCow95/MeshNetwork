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
        private readonly string _data;

        /// <summary>
        /// The destination of the message.
        /// </summary>
        private readonly NodeProperties _destination;

        /// <summary>
        /// The id belonging to this message.
        /// </summary>
        private readonly uint? _messageId;

        /// <summary>
        /// A value indicating whether the message has to be sent to an approved member of the network.
        /// </summary>
        private readonly bool _needsApprovedConnection;

        /// <summary>
        /// The result of the response message.
        /// </summary>
        private readonly MessageResponseResult _responseResult;

        /// <summary>
        /// The sender of the message.
        /// </summary>
        private readonly NodeProperties _sender;

        /// <summary>
        /// The result of the original message.
        /// </summary>
        private readonly MessageSendResult _sendResult;

        /// <summary>
        /// The type of the message.
        /// </summary>
        private readonly MessageType _type;

        /// <summary>
        /// A value indicating whether the sender is waiting for a response to this message.
        /// </summary>
        private readonly bool _waitingForResponse;

        /// <summary>
        /// The raw, unparsed message.
        /// </summary>
        private string _rawMessage;

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
                        case 'a':
                            _type = MessageType.Approval;
                            break;

                        case 'n':
                            _type = MessageType.Neighbors;
                            break;

                        case 'p':
                            _type = MessageType.Ping;
                            break;

                        case 's':
                            _type = MessageType.System;
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
        /// Initializes a new instance of the <see cref="InternalMessage" /> class.
        /// </summary>
        /// <param name="destination">The destination of the message.</param>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data to go along with the message.</param>
        /// <param name="waitingForResponse">Whether this message is waiting for a response.</param>
        /// <param name="messageId">The id of the message if it has one.</param>
        /// <param name="sendResult">The result object passed back after sending a message.</param>
        /// <param name="responseResult">
        /// The result object passed back after sending a message waiting for a response.
        /// </param>
        /// <param name="needsApprovedConnection">A value indicating whether the connection needs to have been approved.</param>
        /// <returns>The composed message to be sent over the wire to the receiving node.</returns>
        private InternalMessage(NodeProperties destination, NodeProperties sender, MessageType type, string data, bool waitingForResponse, uint? messageId, MessageSendResult sendResult, MessageResponseResult responseResult, bool needsApprovedConnection)
        {
            _destination = destination;
            _sender = sender;
            _type = type;
            _data = data;
            _waitingForResponse = waitingForResponse;
            _messageId = messageId;
            _sendResult = sendResult;
            _responseResult = responseResult;
            _needsApprovedConnection = needsApprovedConnection;
        }

        /// <summary>
        /// Gets the data that the message contains.
        /// </summary>
        public string Data
        {
            get
            {
                return _data;
            }
        }

        /// <summary>
        /// Gets the destination of the message.
        /// </summary>
        public NodeProperties Destination
        {
            get
            {
                return _destination;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this message was in response to another message.
        /// </summary>
        public bool InResponseToMessage
        {
            get
            {
                return _messageId.HasValue && _waitingForResponse == false;
            }
        }

        /// <summary>
        /// Gets the id of this message.
        /// </summary>
        public uint MessageId
        {
            get
            {
                return _messageId.HasValue ? _messageId.Value : 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the message has to be sent to an approved member of the network.
        /// </summary>
        public bool NeedsApprovedConnection
        {
            get
            {
                return _needsApprovedConnection;
            }
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
        /// Creates a message that is a response to another message.
        /// </summary>
        /// <param name="destination">The destination of the message.</param>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="type">The type of the message.</param>
        /// <param name="data">The data contained in the message.</param>
        /// <param name="messageId">The id of the message.</param>
        /// <param name="result">The object to put the results in.</param>
        /// <returns>A message that is a response to another message.</returns>
        public static InternalMessage CreateResponseMessage(
            NodeProperties destination,
            NodeProperties sender,
            MessageType type,
            string data,
            uint messageId,
            MessageSendResult result)
        {
            return new InternalMessage(destination, sender, type, data, false, messageId, result, null, false);
        }

        /// <summary>
        /// Creates a message to be sent.
        /// </summary>
        /// <param name="destination">The destination of the message.</param>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="type">The type of the message.</param>
        /// <param name="data">The data contained in the message.</param>
        /// <param name="result">The object to put the results in.</param>
        /// <param name="needsApprovedConnection">A value indicating whether the connection needs to have been approved.</param>
        /// <returns>A message to be sent.</returns>
        public static InternalMessage CreateSendMessage(
            NodeProperties destination,
            NodeProperties sender,
            MessageType type,
            string data,
            MessageSendResult result,
            bool needsApprovedConnection)
        {
            return new InternalMessage(destination, sender, type, data, false, null, result, null, needsApprovedConnection);
        }

        /// <summary>
        /// Creates a message to be sent and that is expecting a response.
        /// </summary>
        /// <param name="destination">The destination of the message.</param>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="type">The type of the message.</param>
        /// <param name="data">The data contained in the message.</param>
        /// <param name="messageId">The id of the message.</param>
        /// <param name="result">The object to put the results in.</param>
        /// <param name="needsApprovedConnection">A value indicating whether the connection needs to have been approved.</param>
        /// <returns>A message to be sent and that is expecting a response.</returns>
        public static InternalMessage CreateSendResponseMessage(
            NodeProperties destination,
            NodeProperties sender,
            MessageType type,
            string data,
            uint messageId,
            MessageResponseResult result,
            bool needsApprovedConnection)
        {
            return new InternalMessage(destination, sender, type, data, true, messageId, null, result, needsApprovedConnection);
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
                case MessageType.Approval:
                    typeChar = 'a';
                    break;

                case MessageType.Neighbors:
                    typeChar = 'n';
                    break;

                case MessageType.Ping:
                    typeChar = 'p';
                    break;

                case MessageType.System:
                    typeChar = 's';
                    break;

                case MessageType.User:
                    typeChar = 'u';
                    break;

                default:
                    throw new NotImplementedException();
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

        /// <summary>
        /// Sets the response result.
        /// </summary>
        /// <param name="result">The result to set it to.</param>
        /// <param name="response">The response message that was received.</param>
        public void SetMessageResponseResult(ResponseResults result, Message response)
        {
            if (_responseResult != null)
            {
                _responseResult.ResponseReceived(result, response);
            }
        }

        /// <summary>
        /// Sets the send result.
        /// </summary>
        /// <param name="result">The result to set it to.</param>
        public void SetMessageSendResult(SendResults result)
        {
            if (_sendResult != null)
            {
                _sendResult.MessageSent(result);
            }

            if (_responseResult != null)
            {
                _responseResult.MessageSent(result);
            }
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
            if (_waitingForResponse || _messageId.HasValue)
            {
                return type + ' ' + waitingForResponse + ' ' + messageId + ' ' + sender + ' ' + data + ' ' + rawMessage;
            }

            return type + ' ' + waitingForResponse + ' ' + sender + ' ' + data + ' ' + rawMessage;
        }
    }
}