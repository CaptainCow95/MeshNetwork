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
    public abstract class NetworkNode
    {
        /// <summary>
        /// The <see cref="Logger"/> object that this node logs to.
        /// </summary>
        private readonly Logger _logger;

        /// <summary>
        /// A dictionary of all the messages currently being received.
        /// </summary>
        private readonly Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();

        /// <summary>
        /// The queue of message to be sent out.
        /// </summary>
        private readonly Queue<InternalMessage> _messagesToSend = new Queue<InternalMessage>();

        /// <summary>
        /// An object to lock on when managing the messages to be sent queue.
        /// </summary>
        private readonly object _messagesToSendLockObject = new object();

        /// <summary>
        /// A queue of the received full messages.
        /// </summary>
        private readonly Queue<Tuple<InternalMessage, NodeProperties>> _receivedMessages = new Queue<Tuple<InternalMessage, NodeProperties>>();

        /// <summary>
        /// The object to lock on for message receiving constructs.
        /// </summary>
        private readonly object _receivingLockObject = new object();

        /// <summary>
        /// A queue of the objects that have gotten approval but need to be processed.
        /// </summary>
        private readonly Queue<ApprovedNodeDetails> _recentlyApprovedNodes = new Queue<ApprovedNodeDetails>();

        /// <summary>
        /// The object to lock on for added nodes that have been approved but still need to be processed.
        /// </summary>
        private readonly object _recentlyApprovedNodesLockObject = new object();

        /// <summary>
        /// The list of nodes to attempt to reconnect to.
        /// </summary>
        private readonly List<NodeProperties> _reconnectNodes = new List<NodeProperties>();

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
        /// The thread approval processing occurs on.
        /// </summary>
        private Thread _approvedNodeThread;

        /// <summary>
        /// A value indicating whether the child threads are running.
        /// </summary>
        private bool _childThreadsRunning;

        /// <summary>
        /// Whether this node is connected to a network.
        /// </summary>
        private bool _connected;

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
        private IPAddress[] _currentIpAddresses;

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
        /// A dictionary of the connections this node is sending messages on.
        /// </summary>
        private volatile Dictionary<NodeProperties, NetworkConnection> _sendingConnections = new Dictionary<NodeProperties, NetworkConnection>();

        /// <summary>
        /// The number of seconds between update attempts.
        /// </summary>
        private int _updateNetworkFrequency = 30;

        /// <summary>
        /// A thread to manage trying to reconnect to various nodes.
        /// </summary>
        private Thread _updateNetworkThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode"/> class.
        /// </summary>
        protected NetworkNode()
            : this(string.Empty, LogLevels.Error)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode"/> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        /// <param name="logLevel">The highest level at which log messages will be written.</param>
        protected NetworkNode(string logLocation, LogLevels logLevel)
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
        /// Gets the <see cref="Logger"/> object that this node logs to.
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
        /// Gets the port this node is currently running on.
        /// </summary>
        public int Port
        {
            get
            {
                return _port;
            }
        }

        /// <summary>
        /// Gets or sets the number of seconds between update attempts.
        /// </summary>
        public int UpdateNetworkFrequency
        {
            get
            {
                return _updateNetworkFrequency;
            }

            set
            {
                _updateNetworkFrequency = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the child threads are running.
        /// </summary>
        protected bool ChildThreadsRunning
        {
            get
            {
                return _childThreadsRunning;
            }
        }

        /// <summary>
        /// Gets the list of nodes to try to reconnect to.
        /// </summary>
        protected List<NodeProperties> ReconnectNodes
        {
            get
            {
                return _reconnectNodes;
            }
        }

        /// <summary>
        /// Called after the network has been connected.
        /// </summary>
        public void AfterConnectToNetwork()
        {
            _pingThread = new Thread(PingThreadRun);
            _pingThread.Start();

            _updateNetworkThread = new Thread(UpdateNetworkThreadRun);
            _updateNetworkThread.Start();

            _connected = true;
        }

        /// <summary>
        /// Called before the network tries to connect.
        /// </summary>
        /// <param name="listeningPort">The port the node will be listening on.</param>
        public void BeforeConnectToNetwork(int listeningPort)
        {
            _connected = false;
            _childThreadsRunning = true;
            _port = listeningPort;

            _messageListenerThread = new Thread(MessageListenerThreadRun);
            _messageListenerThread.Start();

            _approvedNodeThread = new Thread(ApprovedNodeThreadRun);
            _approvedNodeThread.Start();

            _messageSenderThread = new Thread(MessageSenderThreadRun);
            _messageSenderThread.Start();

            _connectionListener = new TcpListener(IPAddress.Any, listeningPort);
            _connectionListener.Start();
            _connectionListenerThread = new Thread(ConnectionListenerThreadRun);
            _connectionListenerThread.Start();
        }

        /// <summary>
        /// Connects this node to a network.
        /// </summary>
        /// <param name="listeningPort">The port to listen on.</param>
        /// <param name="initialNodes">The nodes to try to connect to.</param>
        /// <returns>The results of connection to the network.</returns>
        public abstract ConnectToNetworkResults ConnectToNetwork(
                    int listeningPort,
                    IEnumerable<NodeProperties> initialNodes);

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
                return _sendingConnections.Keys.Where(e => _sendingConnections[e] != null && _sendingConnections[e].Approved).ToList();
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
            return SendMessageResponseInternal(remoteNode, MessageType.Neighbors, string.Empty, false);
        }

        /// <summary>
        /// Send a message to another node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public MessageSendResult SendMessage(NodeProperties sendTo, string message)
        {
            return SendMessageInternal(sendTo, MessageType.User, message, false);
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
            return SendMessageResponseInternal(sendTo, MessageType.User, message, false);
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
        /// A function to be called on the network member once a node has been approved access.
        /// </summary>
        /// <param name="node">The node the approval request was granted from.</param>
        protected abstract void ApprovalGranted(NodeProperties node);

        /// <summary>
        /// A function to be called on the approved node once a node has been approved access.
        /// </summary>
        /// <param name="node">The node the approval request was granted to.</param>
        protected abstract void ApprovalRequestGranted(NodeProperties node);

        /// <summary>
        /// Gets the approval of the node to join the network.
        /// </summary>
        /// <param name="node">The node to get the approval of.</param>
        /// <returns>A value indicating whether approval was granted.</returns>
        protected bool GetApproval(NodeProperties node)
        {
            lock (_sendingLockObject)
            {
                if (_sendingConnections.ContainsKey(node) && _sendingConnections[node] != null
                    && _sendingConnections[node].Approved)
                {
                    return true;
                }
            }

            var result = SendMessageResponseInternal(node, MessageType.Approval, GetNetworkType(), false);
            if (result.SendResult == SendResults.Success && result.ResponseResult == ResponseResults.Success)
            {
                if (result.ResponseMessage.Data == "approved")
                {
                    lock (_sendingLockObject)
                    {
                        var connection = _sendingConnections[node];
                        if (connection != null)
                        {
                            connection.Approved = true;
                        }
                    }

                    lock (_recentlyApprovedNodesLockObject)
                    {
                        _recentlyApprovedNodes.Enqueue(new ApprovedNodeDetails(node, false));
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a connection to the specified node as long as that node has been approved to be
        /// connected to the network.
        /// </summary>
        /// <param name="connectTo">The node to connect to.</param>
        /// <returns>
        /// The network connection associated with that node, or null if it could not be found.
        /// </returns>
        protected NetworkConnection GetApprovedNetworkConnection(NodeProperties connectTo)
        {
            lock (_sendingLockObject)
            {
                if (_sendingConnections.ContainsKey(connectTo) && _sendingConnections[connectTo] != null
                    && _sendingConnections[connectTo].Approved)
                {
                    return _sendingConnections[connectTo];
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a string representing the type of the network.
        /// </summary>
        /// <returns>A string representing the type of the network.</returns>
        protected abstract string GetNetworkType();

        /// <summary>
        /// Gets a network connection to the specified node, creating the connection if need be.
        /// </summary>
        /// <param name="connectTo">The node to connect to.</param>
        /// <returns>
        /// The network connection associated with that node, or null if it could not be created.
        /// </returns>
        protected async Task<NetworkConnection> GetUnapprovedNetworkConnection(NodeProperties connectTo)
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
                    client.NoDelay = true;
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
        protected bool IsSelf(NodeProperties node)
        {
            if (_currentIpAddresses == null)
            {
                _currentIpAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            }

            return _currentIpAddresses.Contains(node.IpAddress) && node.Port == _port;
        }

        /// <summary>
        /// Called when a system message is received.
        /// </summary>
        /// <param name="message">The message that was received.</param>
        protected abstract void ReceivedSystemMessage(Message message);

        /// <summary>
        /// Sends a message to a node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="needsApprovedConnection">
        /// A value indicating whether the connection needs to have been approved.
        /// </param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        protected MessageSendResult SendMessageInternal(NodeProperties sendTo, MessageType type, string message, bool needsApprovedConnection)
        {
            var result = new MessageSendResult();
            var composedMessage = InternalMessage.CreateSendMessage(
                sendTo,
                new NodeProperties("localhost", _port),
                type,
                message,
                result,
                needsApprovedConnection);

            lock (_messagesToSendLockObject)
            {
                _messagesToSend.Enqueue(composedMessage);
            }

            return result;
        }

        /// <summary>
        /// Sends a message to a node and awaits a response.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="needsApprovedConnection">
        /// A value indicating whether the connection needs to have been approved.
        /// </param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        protected MessageResponseResult SendMessageResponseInternal(NodeProperties sendTo, MessageType type, string message, bool needsApprovedConnection)
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
                result,
                needsApprovedConnection);

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
        protected MessageSendResult SendResponseInternal(Message responseTo, MessageType type, string message)
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

        /// <summary>
        /// Updates the network.
        /// </summary>
        protected abstract void UpdateNetwork();

        /// <summary>
        /// The run function for the approved node thread.
        /// </summary>
        private void ApprovedNodeThreadRun()
        {
            while (_childThreadsRunning)
            {
                int count;
                lock (_recentlyApprovedNodesLockObject)
                {
                    count = _recentlyApprovedNodes.Count;
                }

                while (count > 0)
                {
                    ApprovedNodeDetails details;
                    lock (_recentlyApprovedNodesLockObject)
                    {
                        details = _recentlyApprovedNodes.Dequeue();
                    }

                    if (details.ApprovalGranted)
                    {
                        ApprovalGranted(details.Node);
                    }
                    else
                    {
                        ApprovalRequestGranted(details.Node);
                    }

                    lock (_recentlyApprovedNodesLockObject)
                    {
                        count = _recentlyApprovedNodes.Count;
                    }
                }

                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// The run function for the connection listener thread.
        /// </summary>
        private void ConnectionListenerThreadRun()
        {
            while (_childThreadsRunning)
            {
                try
                {
                    var incomingTcpClient = _connectionListener.AcceptTcpClient();
                    var ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                    var incomingNodeProperties = new NodeProperties(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                    _logger.Write("Connection received from " + incomingNodeProperties, LogLevels.Info);

                    lock (_receivingLockObject)
                    {
                        _receivingConnections[incomingNodeProperties] = new NetworkConnection
                        {
                            Client = incomingTcpClient,
                            LastPingReceived = DateTime.UtcNow
                        };
                    }
                }
                catch (Exception)
                {
                    // Assuming the error was because we are stopping accepting connections or it
                    // was a connection error in which case it can be retried.
                }
            }
        }

        /// <summary>
        /// The run function for the message listener thread.
        /// </summary>
        private void MessageListenerThreadRun()
        {
            const int messageBufferSize = 1024;
            var messageBuffer = new byte[messageBufferSize];
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

                        NetworkStream stream = _receivingConnections[key].Client.GetStream();
                        if (stream.DataAvailable)
                        {
                            int bytesRead = stream.Read(messageBuffer, 0, messageBufferSize);
                            _messages[key].Message.Append(Encoding.Default.GetString(messageBuffer, 0, bytesRead));

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

                ProcessMessages();

                Thread.Sleep(1);
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
                    SendMessageInternal(node, MessageType.Ping, string.Empty, true);
                }

                Thread.Sleep(_pingFrequency * 1000);
            }
        }

        /// <summary>
        /// Processes all received messages.
        /// </summary>
        private void ProcessMessages()
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

                if (message.InResponseToMessage)
                {
                    lock (_responsesLockObject)
                    {
                        _responses[message.MessageId] = message;
                    }
                }

                switch (messageObject.Item1.Type)
                {
                    case MessageType.Approval:
                        ReceivedApprovalMessage(message);
                        break;

                    case MessageType.Neighbors:
                        ReceivedNeighborsMessage(message);
                        break;

                    case MessageType.Ping:
                        ReceivedPing(message);
                        break;

                    case MessageType.System:
                        ReceivedSystemMessage(message);
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
        /// Called when an approval message is received.
        /// </summary>
        /// <param name="message">The message that was received.</param>
        private void ReceivedApprovalMessage(Message message)
        {
            if (message.AwaitingResponse)
            {
                if (message.Data == GetNetworkType())
                {
                    // Setup a connection to the sender so that we can approve it.
                    Task.WaitAll(GetUnapprovedNetworkConnection(message.Sender));
                    lock (_sendingLockObject)
                    {
                        if (_sendingConnections.ContainsKey(message.Sender) && _sendingConnections[message.Sender] != null)
                        {
                            _sendingConnections[message.Sender].Approved = true;
                        }
                    }

                    SendResponseInternal(message, MessageType.Approval, "approved");

                    lock (_recentlyApprovedNodesLockObject)
                    {
                        _recentlyApprovedNodes.Enqueue(new ApprovedNodeDetails(message.Sender, true));
                    }
                }
                else
                {
                    SendResponseInternal(message, MessageType.Approval, "failure");
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
        }

        /// <summary>
        /// Called when a ping message is received.
        /// </summary>
        /// <param name="message">The message received.</param>
        private void ReceivedPing(Message message)
        {
            NetworkConnection connection = GetApprovedNetworkConnection(message.Sender);
            if (connection != null)
            {
                connection.LastPingReceived = DateTime.UtcNow;
            }
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

            NetworkConnection connection;
            if (message.NeedsApprovedConnection)
            {
                connection = GetApprovedNetworkConnection(message.Destination);
            }
            else
            {
                connection = await GetUnapprovedNetworkConnection(message.Destination);
            }

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
                message.SetMessageSendResult(SendResults.Success);
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
        /// The run function for the reconnection thread.
        /// </summary>
        private async void UpdateNetworkThreadRun()
        {
            await Task.Delay(_updateNetworkFrequency * 500).ConfigureAwait(false);
            while (ChildThreadsRunning)
            {
                Logger.Write("Updating network", LogLevels.Debug);
                UpdateNetwork();

                await Task.Delay(_updateNetworkFrequency * 1000).ConfigureAwait(false);
            }
        }
    }
}