using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a node on the network.
    /// </summary>
    public class NetworkNode
    {
        /// <summary>
        /// The <see cref="Logger" /> object that this node logs to.
        /// </summary>
        private readonly Logger _logger;

        /// <summary>
        /// A dictionary of all the messages currently being received.
        /// </summary>
        private readonly Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();

        /// <summary>
        /// A queue of the received full messages.
        /// </summary>
        private readonly Queue<Tuple<InternalMessage, NodeProperties>> _receivedMessages = new Queue<Tuple<InternalMessage, NodeProperties>>();

        /// <summary>
        /// The object to lock on for message receiving constructs.
        /// </summary>
        private readonly object _receivingLockObject = new object();

        /// <summary>
        /// A dictionary of the message ids and their corresponding response objects.
        /// </summary>
        private readonly Dictionary<uint, Message> _responses = new Dictionary<uint, Message>();

        /// <summary>
        /// The object to lock on for message response constructs.
        /// </summary>
        private readonly object _responsesLockObject = new object();

        /// <summary>
        /// The object to lock on for message sending constructs.
        /// </summary>
        private readonly object _sendingLockObject = new object();

        /// <summary>
        /// A value indicating whether the child threads are running.
        /// </summary>
        private bool _childThreadsRunning;

        /// <summary>
        /// Whether this node is connected to a network.
        /// </summary>
        private bool _connected = false;

        /// <summary>
        /// The object listening for new incoming connections.
        /// </summary>
        private TcpListener _connectionListener;

        /// <summary>
        /// The thread that is listening for new connections.
        /// </summary>
        private Thread _connectionListenerThread;

        /// <summary>
        /// The current IP addresses that map to this node.
        /// </summary>
        private IPAddress[] _currentIpAddresses = null;

        /// <summary>
        /// The current message id of this node.
        /// </summary>
        private int _messageIdCounter;

        /// <summary>
        /// The thread that is listening for new messages.
        /// </summary>
        private Thread _messageListenerThread;

        /// <summary>
        /// The thread that is sending out messages.
        /// </summary>
        private Thread _messageSenderThread;

        /// <summary>
        /// The queue of message to be sent out.
        /// </summary>
        private Queue<InternalMessage> _messagesToSend = new Queue<InternalMessage>();

        /// <summary>
        /// An object to lock on when managing the messages to be sent queue.
        /// </summary>
        private object _messagesToSendLockObject = new object();

        /// <summary>
        /// The number of seconds between pings.
        /// </summary>
        private int _pingFrequency = 10;

        /// <summary>
        /// The thread sending out pings.
        /// </summary>
        private Thread _pingThread;

        /// <summary>
        /// The port this node is currently running on.
        /// </summary>
        private int _port;

        /// <summary>
        /// A dictionary of the connections this node is receiving messages on.
        /// </summary>
        private volatile Dictionary<NodeProperties, NetworkConnection> _receivingConnections = new Dictionary<NodeProperties, NetworkConnection>();

        /// <summary>
        /// The number of seconds between reconnect attempts.
        /// </summary>
        private int _reconnectionFrequency = 30;

        /// <summary>
        /// A thread to manage trying to reconnect to various nodes.
        /// </summary>
        private Thread _reconnectionThread;

        /// <summary>
        /// The list of nodes to attempt to reconnect to.
        /// </summary>
        private List<NodeProperties> _reconnectNodes = new List<NodeProperties>();

        /// <summary>
        /// A dictionary of the connections this node is sending messages on.
        /// </summary>
        private volatile Dictionary<NodeProperties, NetworkConnection> _sendingConnections = new Dictionary<NodeProperties, NetworkConnection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode" /> class.
        /// </summary>
        public NetworkNode()
            : this(string.Empty, LogLevels.Error)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode" /> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        /// <param name="logLevel">The highest level at which log messages will be written.</param>
        public NetworkNode(string logLocation, LogLevels logLevel)
        {
            _logger = new Logger(logLocation, logLevel);
        }

        /// <summary>
        /// The event handler used when a message is received.
        /// </summary>
        /// <param name="source">This object.</param>
        /// <param name="args">The message that was received.</param>
        public delegate void ReceivedMessageEventHandler(object source, ReceivedMessageEventArgs args);

        /// <summary>
        /// The event to subscribe to in order to receive messages.
        /// </summary>
        public event ReceivedMessageEventHandler ReceivedMessage;

        /// <summary>
        /// Gets a value indicating whether this node is connected to a network.
        /// </summary>
        public bool IsConnectedToNetwork
        {
            get
            {
                return _connected;
            }
        }

        /// <summary>
        /// Gets the <see cref="Logger" /> object that this node logs to.
        /// </summary>
        public Logger Logger
        {
            get
            {
                return _logger;
            }
        }

        /// <summary>
        /// Gets or sets the number of seconds between pings.
        /// </summary>
        public int PingFrequency
        {
            get
            {
                return _pingFrequency;
            }

            set
            {
                _pingFrequency = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of seconds between reconnect attempts.
        /// </summary>
        public int ReconnectionFrequency
        {
            get
            {
                return _reconnectionFrequency;
            }

            set
            {
                _reconnectionFrequency = value;
            }
        }

        /// <summary>
        /// Connects this node to a network.
        /// </summary>
        /// <param name="listeningPort">The port to listen on.</param>
        /// <param name="initialNodes">The nodes to try to connect to.</param>
        /// <returns>A task to await on.</returns>
        public async Task<ConnectToNetworkResults> ConnectToNetworkAsync(int listeningPort, IReadOnlyCollection<NodeProperties> initialNodes)
        {
            ConnectToNetworkResults result = ConnectToNetworkResults.NewNetworkCreated;
            _connected = false;
            _childThreadsRunning = true;
            _port = listeningPort;

            _logger.Write("Connecting to a network: listening on " + listeningPort, LogLevels.Info);

            _messageListenerThread = new Thread(MessageListenerThreadRun);
            _messageListenerThread.Start();

            _messageSenderThread = new Thread(MessageSenderThreadRun);
            _messageSenderThread.Start();

            _connectionListener = new TcpListener(IPAddress.Any, listeningPort);
            _connectionListener.Start();
            _connectionListenerThread = new Thread(ConnectionListenerThreadRun);
            _connectionListenerThread.Start();

            foreach (var neighbor in initialNodes)
            {
                if (IsSelf(neighbor))
                {
                    continue;
                }

                NetworkConnection connection = await GetNetworkConnection(neighbor).ConfigureAwait(false);
                if (connection == null)
                {
                    continue;
                }

                this._logger.Write("Connection established to " + neighbor + ", getting rest of connected machines.", LogLevels.Info);

                var getNeighborsResult = GetRemoteNeighbors(this.GetNeighbors()[0]);
                if (getNeighborsResult.SendResult == SendResults.Success
                    && getNeighborsResult.ResponseResult == ResponseResults.Success)
                {
                    result = ConnectToNetworkResults.ConnectionSuccessful;
                    foreach (
                        var remoteNeighbor in
                            getNeighborsResult.ResponseMessage.Data.Split(
                                new[] { ';' },
                                StringSplitOptions.RemoveEmptyEntries).ToList().Select(e => new NodeProperties(e)))
                    {
                        if (IsSelf(remoteNeighbor))
                        {
                            continue;
                        }

                        this._logger.Write("Attempting connection to " + remoteNeighbor, LogLevels.Info);
                        await this.GetNetworkConnection(remoteNeighbor).ConfigureAwait(false);
                    }

                    break;
                }

                this._logger.Write("Failed to get the rest of the connected machines, trying the next node.", LogLevels.Info);
            }

            _pingThread = new Thread(PingThreadRun);
            _pingThread.Start();

            _reconnectNodes.AddRange(initialNodes);
            _reconnectionThread = new Thread(ReconnectionThreadRun);
            _reconnectionThread.Start();

            _connected = true;
            _logger.Write("Connected and ready", LogLevels.Info);

            return result;
        }

        /// <summary>
        /// Disconnect from the network.
        /// </summary>
        public void Disconnect()
        {
            _connected = false;
            _logger.Write("Shutting down", LogLevels.Info);
            _childThreadsRunning = false;
            _connectionListener.Stop();
        }

        /// <summary>
        /// Gets a list of the neighboring nodes.
        /// </summary>
        /// <returns>The nodes that this node is connected to.</returns>
        public List<NodeProperties> GetNeighbors()
        {
            lock (_sendingLockObject)
            {
                return _sendingConnections.Keys.Where(e => _sendingConnections[e] != null).ToList();
            }
        }

        /// <summary>
        /// Gets a list of the remote node's neighboring nodes.
        /// </summary>
        /// <param name="remoteNode">The remote node to retrieve the information from.</param>
        /// <returns>
        /// The nodes that the remote node is connected to, null if the call failed to reach the
        /// remote host.
        /// </returns>
        public MessageResponseResult GetRemoteNeighbors(NodeProperties remoteNode)
        {
            return SendMessageResponseInternal(remoteNode, MessageType.Neighbors, string.Empty);
        }

        /// <summary>
        /// Send a message to another node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public MessageSendResult SendMessage(NodeProperties sendTo, string message)
        {
            return SendMessageInternal(sendTo, MessageType.User, message);
        }

        /// <summary>
        /// Sends a message to another node and awaits a response.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>
        /// A response object that contains whether the message was successfully sent and the
        /// response from the receiver.
        /// </returns>
        public MessageResponseResult SendMessageResponse(NodeProperties sendTo, string message)
        {
            return SendMessageResponseInternal(sendTo, MessageType.User, message);
        }

        /// <summary>
        /// Sends a message response.
        /// </summary>
        /// <param name="responseTo">The message that is being responded to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public MessageSendResult SendResponse(Message responseTo, string message)
        {
            return SendResponseInternal(responseTo, MessageType.User, message);
        }

        /// <summary>
        /// The run function for the connection listener thread.
        /// </summary>
        private void ConnectionListenerThreadRun()
        {
            while (_childThreadsRunning)
            {
                var incomingTcpClient = _connectionListener.AcceptTcpClient();
                var ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                var incomingNodeProperties = new NodeProperties(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                lock (_receivingLockObject)
                {
                    _receivingConnections[incomingNodeProperties] = new NetworkConnection { Client = incomingTcpClient, LastPingReceived = DateTime.UtcNow };
                }

                _logger.Write("Connection received from " + incomingNodeProperties, LogLevels.Info);
            }
        }

        /// <summary>
        /// Gets a network connection to the specified node, creating the connection if need be.
        /// </summary>
        /// <param name="connectTo">The node to connect to.</param>
        /// <returns>
        /// The network connection associated with that node, or null if it could not be created.
        /// </returns>
        private async Task<NetworkConnection> GetNetworkConnection(NodeProperties connectTo)
        {
            bool reconnect;
            lock (_sendingLockObject)
            {
                reconnect = !_sendingConnections.ContainsKey(connectTo);

                if (reconnect)
                {
                    _sendingConnections[connectTo] = null;
                }
            }

            if (reconnect)
            {
                TcpClient client = null;
                try
                {
                    _logger.Write("Attempting connection to " + connectTo, LogLevels.Info);
                    client = new TcpClient();
                    await client.ConnectAsync(connectTo.IpAddress, connectTo.Port).ConfigureAwait(false);
                    NetworkConnection connection = new NetworkConnection
                    {
                        Client = client,
                        LastPingReceived = DateTime.UtcNow
                    };
                    lock (_sendingLockObject)
                    {
                        _sendingConnections[connectTo] = connection;
                    }

                    _logger.Write("Connection to " + connectTo + " successful", LogLevels.Info);
                    return connection;
                }
                catch
                {
                    _logger.Write("Connection to " + connectTo + " failed", LogLevels.Warning);
                    if (client != null)
                    {
                        client.Close();
                    }

                    lock (_sendingLockObject)
                    {
                        _sendingConnections.Remove(connectTo);
                    }

                    return null;
                }
            }

            bool waiting;
            lock (_sendingLockObject)
            {
                waiting = _sendingConnections[connectTo] == null;
            }

            while (waiting)
            {
                await Task.Delay(1).ConfigureAwait(false);
                lock (_sendingLockObject)
                {
                    waiting = _sendingConnections.ContainsKey(connectTo) && _sendingConnections[connectTo] == null;
                }
            }

            lock (_sendingLockObject)
            {
                return !_sendingConnections.ContainsKey(connectTo) ? null : _sendingConnections[connectTo];
            }
        }

        /// <summary>
        /// Determines whether a node represents the current node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>True if the node represents the current node.</returns>
        private bool IsSelf(NodeProperties node)
        {
            if (_currentIpAddresses == null)
            {
                _currentIpAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            }

            return _currentIpAddresses.Contains(node.IpAddress) && node.Port == _port;
        }

        /// <summary>
        /// The run function for the message listener thread.
        /// </summary>
        private async void MessageListenerThreadRun()
        {
            while (_childThreadsRunning)
            {
                lock (_receivingLockObject)
                {
                    foreach (var key in _receivingConnections.Keys)
                    {
                        if (!_messages.ContainsKey(key))
                        {
                            _messages[key] = new MessageBuilder();
                        }

                        var availableBytes = _receivingConnections[key].Client.Available;
                        if (availableBytes > 0)
                        {
                            var buffer = new byte[availableBytes];
                            _receivingConnections[key].Client.GetStream().Read(buffer, 0, availableBytes);
                            _messages[key].Message.Append(Encoding.Default.GetString(buffer));

                            if (_messages[key].Length == -1 && _messages[key].Message.Length > 0)
                            {
                                var messageLength = new StringBuilder();
                                for (var i = 0; i < _messages[key].Message.Length; ++i)
                                {
                                    if (char.IsDigit(_messages[key].Message[i]))
                                    {
                                        messageLength.Append(_messages[key].Message[i]);
                                    }
                                    else
                                    {
                                        _messages[key].Length = int.Parse(messageLength.ToString());
                                        break;
                                    }
                                }
                            }

                            if (_messages[key].Length != -1 && _messages[key].Message.Length >= _messages[key].Length)
                            {
                                _receivedMessages.Enqueue(new Tuple<InternalMessage, NodeProperties>(new InternalMessage(_messages[key].Message.ToString(0, _messages[key].Length), key), key));
                                _messages[key].Message.Remove(0, _messages[key].Length);
                                _messages[key].Length = -1;
                            }
                        }
                    }
                }

                await ProcessMessages().ConfigureAwait(false);

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The run function for the message sending thread.
        /// </summary>
        private async void MessageSenderThreadRun()
        {
            List<Task> runningTasks = new List<Task>();
            while (_childThreadsRunning)
            {
                int count;
                lock (_messagesToSendLockObject)
                {
                    count = _messagesToSend.Count;
                }

                while (count > 0)
                {
                    InternalMessage message;
                    lock (_messagesToSendLockObject)
                    {
                        message = _messagesToSend.Dequeue();
                    }

                    runningTasks.Add(SendMessageLowLevel(message));

                    lock (_messagesToSendLockObject)
                    {
                        count = _messagesToSend.Count;
                    }
                }

                await Task.Delay(1).ConfigureAwait(false);

                for (int i = 0; i < runningTasks.Count; ++i)
                {
                    if (runningTasks[i].IsCompleted)
                    {
                        runningTasks.RemoveAt(i);
                        --i;
                    }
                }
            }

            Task.WaitAll(runningTasks.ToArray());
        }

        /// <summary>
        /// The run function for the ping thread.
        /// </summary>
        private void PingThreadRun()
        {
            while (_childThreadsRunning)
            {
                // Get a copy to avoid using a lock and causing a deadlock
                foreach (var node in GetNeighbors())
                {
                    SendMessageInternal(node, MessageType.Ping, string.Empty);
                }

                Thread.Sleep(_pingFrequency * 1000);
            }
        }

        /// <summary>
        /// Processes all received messages.
        /// </summary>
        /// <returns>A task to await on.</returns>
        private async Task ProcessMessages()
        {
            while (_receivedMessages.Count > 0)
            {
                var messageObject = _receivedMessages.Dequeue();
                var message = new Message(
                                        messageObject.Item1.Sender,
                                        messageObject.Item1.Data,
                                        messageObject.Item1.MessageId,
                                        messageObject.Item1.WaitingForResponse,
                                        messageObject.Item1.InResponseToMessage);
                _logger.Write("Message received, " + messageObject.Item1, LogLevels.Debug);
                switch (messageObject.Item1.Type)
                {
                    case MessageType.Neighbors:
                        ReceivedNeighborsMessage(message);
                        break;

                    case MessageType.Ping:
                        await ReceivedPing(message).ConfigureAwait(false);
                        break;

                    case MessageType.User:
                        if (ReceivedMessage != null)
                        {
                            ReceivedMessage(this, new ReceivedMessageEventArgs(message));
                        }

                        break;

                    case MessageType.Unknown:
                        break;
                }
            }
        }

        /// <summary>
        /// Called when a neighbor message is received.
        /// </summary>
        /// <param name="message">The message received.</param>
        private void ReceivedNeighborsMessage(Message message)
        {
            if (message.AwaitingResponse)
            {
                // request for information
                var builder = new StringBuilder();
                foreach (var item in GetNeighbors())
                {
                    builder.Append(item.IpAddress);
                    builder.Append(":");
                    builder.Append(item.Port);
                    builder.Append(";");
                }

                if (builder.Length == 0)
                {
                    builder.Append(";");
                }

                SendResponseInternal(message, MessageType.Neighbors, builder.ToString());
            }
            else if (message.InResponseToMessage)
            {
                lock (_responsesLockObject)
                {
                    _responses[message.MessageId] = message;
                }
            }
        }

        /// <summary>
        /// Called when a ping message is received.
        /// </summary>
        /// <param name="message">The message received.</param>
        /// <returns>A task to await on.</returns>
        private async Task ReceivedPing(Message message)
        {
            NetworkConnection connection = await GetNetworkConnection(message.Sender).ConfigureAwait(false);
            if (connection != null)
            {
                connection.LastPingReceived = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// The run function for the reconnection thread.
        /// </summary>
        private async void ReconnectionThreadRun()
        {
            await Task.Delay(_reconnectionFrequency * 500).ConfigureAwait(false);
            while (_childThreadsRunning)
            {
                bool reconnected = true;
                while (reconnected)
                {
                    reconnected = false;

                    // Go through looking for other nodes that are connected but that we are not
                    // connected to.
                    foreach (var node in GetNeighbors())
                    {
                        _logger.Write("Attempting to make connections to the neighbors of " + node, LogLevels.Info);
                        var getNeighborsResult = GetRemoteNeighbors(node);
                        if (getNeighborsResult.SendResult == SendResults.Success
                            && getNeighborsResult.ResponseResult == ResponseResults.Success)
                        {
                            foreach (var neighbor in
                                getNeighborsResult.ResponseMessage.Data.Split(
                                    new[] { ';' },
                                    StringSplitOptions.RemoveEmptyEntries).ToList().Select(e => new NodeProperties(IPAddress.Parse(e.Split(':')[0]), int.Parse(e.Split(':')[1]))))
                            {
                                if (IsSelf(neighbor))
                                {
                                    continue;
                                }

                                if (!_sendingConnections.ContainsKey(neighbor))
                                {
                                    // Attempt a connection
                                    if (await this.GetNetworkConnection(neighbor).ConfigureAwait(false) != null)
                                    {
                                        reconnected = true;
                                    }
                                }
                            }
                        }
                    }

                    // Look for neighbors that we tried to connect to initially, but are no longer
                    // connected to.
                    if (_reconnectNodes != null)
                    {
                        foreach (var node in _reconnectNodes)
                        {
                            if (IsSelf(node))
                            {
                                continue;
                            }

                            if (!_sendingConnections.ContainsKey(node))
                            {
                                _logger.Write("Attempting to reconnect to " + node, LogLevels.Info);
                                var getNeighborsResult = GetRemoteNeighbors(node);
                                if (getNeighborsResult.SendResult == SendResults.Success
                                    && getNeighborsResult.ResponseResult == ResponseResults.Success)
                                {
                                    foreach (var neighbor in
                                        getNeighborsResult.ResponseMessage.Data.Split(
                                            new[] { ';' },
                                            StringSplitOptions.RemoveEmptyEntries)
                                            .ToList()
                                            .Select(e => new NodeProperties(e)))
                                    {
                                        if (IsSelf(node))
                                        {
                                            continue;
                                        }

                                        // Attempt a connection
                                        if (await GetNetworkConnection(neighbor).ConfigureAwait(false) != null)
                                        {
                                            reconnected = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                await Task.Delay(this._reconnectionFrequency * 1000).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends a message to a node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private MessageSendResult SendMessageInternal(NodeProperties sendTo, MessageType type, string message)
        {
            var result = new MessageSendResult();
            var composedMessage = InternalMessage.CreateSendMessage(
                sendTo,
                new NodeProperties("localhost", _port),
                type,
                message,
                result);

            lock (_messagesToSendLockObject)
            {
                _messagesToSend.Enqueue(composedMessage);
            }

            return result;
        }

        /// <summary>
        /// Sends a composed message to a node.
        /// </summary>
        /// <param name="message">The composed message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private async Task SendMessageLowLevel(InternalMessage message)
        {
            if (IsSelf(message.Destination))
            {
                message.SetMessageSendResult(SendResults.SendingToSelfFailure);
                if (message.WaitingForResponse)
                {
                    lock (_responsesLockObject)
                    {
                        _responses.Remove(message.MessageId);
                    }
                }

                return;
            }

            var connection = await GetNetworkConnection(message.Destination).ConfigureAwait(false);
            if (connection == null)
            {
                message.SetMessageSendResult(SendResults.ConnectionFailure);
                if (message.WaitingForResponse)
                {
                    lock (_responsesLockObject)
                    {
                        _responses.Remove(message.MessageId);
                    }
                }

                return;
            }

            var buffer = Encoding.Default.GetBytes(message.GetNetworkString());
            try
            {
                await connection.Client.GetStream().WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                _logger.Write("Message sending successful " + message, LogLevels.Debug);
            }
            catch
            {
                _logger.Write("Message sending failed " + message, LogLevels.Warning);
                connection.Client.Close();
                lock (_sendingLockObject)
                {
                    _sendingConnections.Remove(message.Destination);
                }

                lock (_receivingLockObject)
                {
                    _messages.Remove(message.Destination);
                }

                message.SetMessageSendResult(SendResults.ConnectionFailure);
                if (message.WaitingForResponse)
                {
                    lock (_responsesLockObject)
                    {
                        _responses.Remove(message.MessageId);
                    }
                }

                return;
            }

            if (message.WaitingForResponse)
            {
                bool wait;
                lock (_responsesLockObject)
                {
                    wait = _responses[message.MessageId] == null;
                }

                while (wait)
                {
                    // TODO: Check for a closed connection as well as a timeout if set.
                    await Task.Delay(1).ConfigureAwait(false);
                    lock (_responsesLockObject)
                    {
                        wait = _responses[message.MessageId] == null;
                    }
                }

                Message response;
                lock (_responsesLockObject)
                {
                    response = _responses[message.MessageId];
                    _responses.Remove(message.MessageId);
                }

                message.SetMessageResponseResult(ResponseResults.Success, response);
            }
        }

        /// <summary>
        /// Sends a message to a node and awaits a response.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private MessageResponseResult SendMessageResponseInternal(NodeProperties sendTo, MessageType type, string message)
        {
            uint id = (uint)Interlocked.Increment(ref _messageIdCounter);
            lock (_responsesLockObject)
            {
                _responses[id] = null;
            }

            var result = new MessageResponseResult();
            var composedMessage = InternalMessage.CreateSendResponseMessage(
                sendTo,
                new NodeProperties("localhost", _port),
                type,
                message,
                id,
                result);

            lock (_messagesToSendLockObject)
            {
                _messagesToSend.Enqueue(composedMessage);
            }

            return result;
        }

        /// <summary>
        /// Sends a response message to a node.
        /// </summary>
        /// <param name="responseTo">The message that this message is in response to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private MessageSendResult SendResponseInternal(Message responseTo, MessageType type, string message)
        {
            var result = new MessageSendResult();
            var composedMessage = InternalMessage.CreateResponseMessage(
                responseTo.Sender,
                new NodeProperties("localhost", _port),
                type,
                message,
                responseTo.MessageId,
                result);

            lock (_messagesToSendLockObject)
            {
                _messagesToSend.Enqueue(composedMessage);
            }

            return result;
        }
    }
}