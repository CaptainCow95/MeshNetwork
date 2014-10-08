﻿using System;
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
        /// The number of seconds between pings before a client is considered disconnected.
        /// </summary>
        internal const int ConnectionTimeout = 20;

        /// <summary>
        /// The number of seconds between pings.
        /// </summary>
        internal const int PingFrequency = 10;

        /// <summary>
        /// A dictionary of all the messages currently being recieved.
        /// </summary>
        private readonly Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();

        /// <summary>
        /// A queue of the recieved full messages.
        /// </summary>
        private readonly Queue<Tuple<InternalMessage, NodeProperties>> _recievedMessages = new Queue<Tuple<InternalMessage, NodeProperties>>();

        private readonly object _recievingLockObject = new object();
        private readonly Dictionary<uint, Message> _responses = new Dictionary<uint, Message>();
        private readonly object _responsesLockObject = new object();
        private readonly object _sendingLockObject = new object();
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
        /// Whether the thread listening for new connections should be running.
        /// </summary>
        private bool _connectionListenerThreadRunning;

        private int _messageIdCounter;

        /// <summary>
        /// The thread that is listening for new messages.
        /// </summary>
        private Thread _messageListenerThread;

        /// <summary>
        /// Whether the thread listening for new messages should be running.
        /// </summary>
        private bool _messageListenerThreadRunning;

        /// <summary>
        /// The thread sending out pings.
        /// </summary>
        private Thread _pingThread;

        /// <summary>
        /// Whether the thread sending out pings should be running.
        /// </summary>
        private bool _pingThreadRunning;

        /// <summary>
        /// The port this node is currently running on.
        /// </summary>
        private int _port;

        /// <summary>
        /// A dictionary of the connections this node is recieving messages on.
        /// </summary>
        private volatile Dictionary<NodeProperties, NetworkConnection> _recievingConnections = new Dictionary<NodeProperties, NetworkConnection>();

        /// <summary>
        /// A dictionary of the connections this node is sending messages on.
        /// </summary>
        private volatile Dictionary<NodeProperties, NetworkConnection> _sendingConnections = new Dictionary<NodeProperties, NetworkConnection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode" /> class.
        /// </summary>
        public NetworkNode()
        {
            Logger.Init(string.Empty);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkNode" /> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        public NetworkNode(string logLocation)
        {
            Logger.Init(logLocation);
        }

        /// <summary>
        /// The event handler used when a message is recieved.
        /// </summary>
        /// <param name="source">This object.</param>
        /// <param name="args">The message that was recieved.</param>
        public delegate void RecievedMessageEventHandler(object source, RecievedMessageEventArgs args);

        /// <summary>
        /// The event to subscribe to in order to recieve messages.
        /// </summary>
        public event RecievedMessageEventHandler RecievedMessage;

        /// <summary>
        /// Connects this node to a network.
        /// </summary>
        /// <param name="listeningPort">The port to listen on.</param>
        /// <param name="initialNodes">The nodes to try to connect to.</param>
        public async Task ConnectToNetworkAsync(int listeningPort, IEnumerable<NodeProperties> initialNodes)
        {
            _connected = false;
            _port = listeningPort;

            Logger.Write("Connecting to a network: listening on " + listeningPort);

            _messageListenerThreadRunning = true;
            _messageListenerThread = new Thread(MessageListenerThreadRun);
            _messageListenerThread.Start();

            _connectionListener = new TcpListener(IPAddress.Any, listeningPort);
            _connectionListener.Start();
            _connectionListenerThreadRunning = true;
            _connectionListenerThread = new Thread(ConnectionListenerThreadRun);
            _connectionListenerThread.Start();

            var connected = false;
            foreach (var neighbor in initialNodes)
            {
                Logger.Write("Attempting connection to " + neighbor.IpAddress + ":" + neighbor.Port);
                NetworkConnection connection = await GetClientOrConnect(neighbor).ConfigureAwait(false);
                if (connection != null)
                {
                    connected = true;
                    break;
                }
            }

            if (connected)
            {
                foreach (var neighbor in await GetRemoteNeighborsAsync(GetNeighbors()[0]).ConfigureAwait(false))
                {
                    Logger.Write("Attempting connection to " + neighbor.IpAddress + ":" + neighbor.Port);
                    await GetClientOrConnect(neighbor).ConfigureAwait(false);
                }
            }

            _pingThreadRunning = true;
            _pingThread = new Thread(PingThreadRun);
            _pingThread.Start();

            _connected = true;
            Logger.Write("Connected and ready");
        }

        /// <summary>
        /// Disconnect from the network.
        /// </summary>
        public void Disconnect()
        {
            _connected = false;
            Logger.Write("Shutting down");
            _pingThreadRunning = false;
            _connectionListenerThreadRunning = false;
            _messageListenerThreadRunning = false;
            _connectionListener.Stop();
        }

        /// <summary>
        /// Gets a list of the neighboring nodes.
        /// </summary>
        /// <returns>The nodes that this node is connected to.</returns>
        public List<NodeProperties> GetNeighbors()
        {
            var ret = new List<NodeProperties>();
            lock (_sendingLockObject)
            {
                foreach (var node in _sendingConnections.Keys.Where(node => !ret.Contains(node)))
                {
                    ret.Add(node);
                }
            }

            return ret;
        }

        /// <summary>
        /// Gets a list of the remote node's neighboring nodes.
        /// </summary>
        /// <param name="remoteNode">The remote node to retrieve the information from.</param>
        /// <returns>
        /// The nodes that the remote node is connected to, null if the call failed to reach the
        /// remote host.
        /// </returns>
        public async Task<List<NodeProperties>> GetRemoteNeighborsAsync(NodeProperties remoteNode)
        {
            var response = await SendMessageResponseInternal(remoteNode, MessageType.Neighbors, string.Empty).ConfigureAwait(false);

            if (!response.MessageSent)
            {
                return null;
            }

            // recieved information
            var neighbors = response.ResponseMessage.Data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var nodes = neighbors.Select(item => new NodeProperties(item)).ToList();

            return nodes;
        }

        public bool IsConnectedToNetwork()
        {
            return _connected;
        }

        /// <summary>
        /// Send a message to another node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public async Task<bool> SendMessageAsync(NodeProperties sendTo, string message)
        {
            return await SendMessageInternal(sendTo, MessageType.User, message).ConfigureAwait(false);
        }

        public async Task<Response> SendMessageResponseAsync(NodeProperties sendTo, string message)
        {
            return await SendMessageResponseInternal(sendTo, MessageType.User, message).ConfigureAwait(false);
        }

        public async Task<bool> SendResponseAsync(Message responseTo, string message)
        {
            return await SendResponseInternal(responseTo, MessageType.User, message).ConfigureAwait(false);
        }

        /// <summary>
        /// The run function for the connection listener thread.
        /// </summary>
        private void ConnectionListenerThreadRun()
        {
            while (_connectionListenerThreadRunning)
            {
                var incomingTcpClient = _connectionListener.AcceptTcpClient();
                var ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                var incomingNodeProperties = new NodeProperties(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                lock (_recievingLockObject)
                {
                    _recievingConnections[incomingNodeProperties] = new NetworkConnection { Client = incomingTcpClient, LastPingRecieved = DateTime.UtcNow };
                }
                Logger.Write("Connection recieved from " + incomingNodeProperties.IpAddress + ":" + incomingNodeProperties.Port);
            }
        }

        private async Task<NetworkConnection> GetClientOrConnect(NodeProperties connectTo)
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
                    Logger.Write("Attempting connection to " + connectTo.IpAddress + ":" + connectTo.Port);
                    client = new TcpClient();
                    await client.ConnectAsync(connectTo.IpAddress, connectTo.Port).ConfigureAwait(false);
                    NetworkConnection connection = new NetworkConnection
                    {
                        Client = client,
                        LastPingRecieved = DateTime.UtcNow
                    };
                    lock (_sendingLockObject)
                    {
                        _sendingConnections[connectTo] = connection;
                    }
                    Logger.Write("Connection to " + connectTo.IpAddress + ":" + connectTo.Port + " successful");
                    return connection;
                }
                catch
                {
                    Logger.Write("Connection to " + connectTo.IpAddress + ":" + connectTo.Port + " failed");
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
        /// The run function for the message listener thread.
        /// </summary>
        private async void MessageListenerThreadRun()
        {
            while (_messageListenerThreadRunning)
            {
                lock (_recievingLockObject)
                {
                    foreach (var key in _recievingConnections.Keys)
                    {
                        if (!_messages.ContainsKey(key))
                        {
                            _messages[key] = new MessageBuilder();
                        }

                        var availableBytes = _recievingConnections[key].Client.Available;
                        if (availableBytes > 0)
                        {
                            var buffer = new byte[availableBytes];
                            _recievingConnections[key].Client.GetStream().Read(buffer, 0, availableBytes);
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
                                _recievedMessages.Enqueue(new Tuple<InternalMessage, NodeProperties>(InternalMessage.Parse(_messages[key].Message.ToString(0, _messages[key].Length), key), key));
                                _messages[key].Message.Remove(0, _messages[key].Length);
                            }
                        }
                    }
                }

                await ProcessMessages().ConfigureAwait(false);

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The run function for the ping thread.
        /// </summary>
        private async void PingThreadRun()
        {
            while (_pingThreadRunning)
            {
                // Get a copy to avoid using a lock and causing a deadlock
                foreach (var node in GetNeighbors())
                {
                    await SendMessageInternal(node, MessageType.Ping, string.Empty).ConfigureAwait(false);
                }

                await Task.Delay(PingFrequency * 1000).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Processes all recieved messages.
        /// </summary>
        private async Task ProcessMessages()
        {
            while (_recievedMessages.Count > 0)
            {
                var messageObject = _recievedMessages.Dequeue();
                var message = new Message(messageObject.Item1.Sender, messageObject.Item1.Data,
                                messageObject.Item1.MessageId.GetValueOrDefault(),
                                messageObject.Item1.WaitingForResponse, false);
                Logger.Write("Message recieved, " + messageObject.Item1);
                switch (messageObject.Item1.Type)
                {
                    case MessageType.Neighbors:
                        await RecievedNeighborsMessage(message).ConfigureAwait(false);
                        break;

                    case MessageType.Ping:
                        await RecievedPing(message).ConfigureAwait(false);
                        break;

                    case MessageType.User:
                        if (RecievedMessage != null)
                        {
                            RecievedMessage(this, new RecievedMessageEventArgs(message));
                        }
                        break;

                    case MessageType.Unknown:
                        break;
                }
            }
        }

        private async Task RecievedNeighborsMessage(Message message)
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

                await SendResponseInternal(message, MessageType.Neighbors, builder.ToString()).ConfigureAwait(false);
            }
        }

        private async Task RecievedPing(Message message)
        {
            NetworkConnection connection = await GetClientOrConnect(message.Sender).ConfigureAwait(false);
            connection.LastPingRecieved = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates the message to send it to a node.
        /// </summary>
        /// <param name="sendTo">The node to send it to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private async Task<bool> SendMessageInternal(NodeProperties sendTo, MessageType type, string message)
        {
            var composedMessage = InternalMessage.CreateMessage(_port, type, message);

            return await SendMessageLowLevel(sendTo, composedMessage).ConfigureAwait(false);
        }

        private async Task<bool> SendMessageLowLevel(NodeProperties sendTo, string composedMessage)
        {
            if (string.IsNullOrEmpty(composedMessage))
            {
                return false;
            }

            var connection = await GetClientOrConnect(sendTo).ConfigureAwait(false);
            if (connection == null)
            {
                return false;
            }

            var buffer = Encoding.Default.GetBytes(composedMessage);
            try
            {
                await connection.Client.GetStream().WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                Logger.Write("Message sending successful");
            }
            catch
            {
                Logger.Write("Message sending failed");
                connection.Client.Close();
                lock (_sendingLockObject)
                {
                    _sendingConnections.Remove(sendTo);
                }

                lock (_recievingLockObject)
                {
                    _messages.Remove(sendTo);
                }
                return false;
            }

            return true;
        }

        private async Task<Response> SendMessageResponseInternal(NodeProperties sendTo, MessageType type, string message)
        {
            uint id = (uint)Interlocked.Increment(ref _messageIdCounter);
            lock (_responsesLockObject)
            {
                _responses[id] = null;
            }
            var composedMessage = InternalMessage.CreateMessage(_port, type, message, true, id);
            bool sendMessageResult = await SendMessageLowLevel(sendTo, composedMessage).ConfigureAwait(false);

            if (!sendMessageResult)
            {
                lock (_responsesLockObject)
                {
                    _responses.Remove(id);
                }
                return new Response(false, null);
            }

            bool wait;
            lock (_responsesLockObject)
            {
                wait = _responses[id] == null;
            }

            while (wait)
            {
                await Task.Delay(1).ConfigureAwait(false);
                lock (_responsesLockObject)
                {
                    wait = _responses[id] == null;
                }
            }

            Message response;
            lock (_responsesLockObject)
            {
                response = _responses[id];
                _responses.Remove(id);
            }
            return new Response(true, response);
        }

        private async Task<bool> SendResponseInternal(Message responseTo, MessageType type, string message)
        {
            var composedMessage = InternalMessage.CreateMessage(_port, type, message, false, responseTo.MessageId);
            return await SendMessageLowLevel(responseTo.Sender, composedMessage).ConfigureAwait(false);
        }
    }
}