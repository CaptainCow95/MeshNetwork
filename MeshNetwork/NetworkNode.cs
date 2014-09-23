using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
        internal static int ConnectionTimeout = 20;

        /// <summary>
        /// The number of seconds between pings.
        /// </summary>
        internal static int PingFrequency = 10;

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

        /// <summary>
        /// The object to lock on.
        /// </summary>
        private object _lockObject = new object();

        /// <summary>
        /// The thread that is listening for new messages.
        /// </summary>
        private Thread _messageListenerThread;

        /// <summary>
        /// Whether the thread listening for new messages should be running.
        /// </summary>
        private bool _messageListenerThreadRunning;

        /// <summary>
        /// A dictionary of all the messages currently being recieved.
        /// </summary>
        private Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();

        /// <summary>
        /// A list of all of the neighbors.
        /// </summary>
        private volatile List<NodeProperties> _neighbors = new List<NodeProperties>();

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
        /// A queue of the recieved full messages.
        /// </summary>
        private Queue<Tuple<Message, NodeProperties>> _recievedMessages = new Queue<Tuple<Message, NodeProperties>>();

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
        /// <param name="nodes">The nodes to try to connect to.</param>
        public void ConnectToNetwork(int listeningPort, params string[] nodes)
        {
            _port = listeningPort;

            Logger.Write("Connecting to a network: listening on " + listeningPort);
            lock (_lockObject)
            {
                foreach (var address in nodes)
                {
                    _neighbors.Add(new NodeProperties(address));
                }
            }

            _messageListenerThreadRunning = true;
            _messageListenerThread = new Thread(MessageListenerThreadRun);
            _messageListenerThread.Start();

            _connectionListener = new TcpListener(IPAddress.Any, listeningPort);
            _connectionListener.Start();
            _connectionListenerThreadRunning = true;
            _connectionListenerThread = new Thread(ConnectionListenerThreadRun);
            _connectionListenerThread.Start();

            lock (_lockObject)
            {
                foreach (var neighbor in _neighbors)
                {
                    TcpClient client = null;
                    try
                    {
                        Logger.Write("Attempting connection to " + neighbor.IP + ":" + neighbor.Port);
                        client = new TcpClient();
                        client.Connect(new IPEndPoint(neighbor.IP, neighbor.Port));
                        _sendingConnections[neighbor] = new NetworkConnection() { Client = client, LastPingRecieved = DateTime.UtcNow };
                        Logger.Write("Connection to " + neighbor.IP + ":" + neighbor.Port + " successful");
                    }
                    catch (Exception)
                    {
                        if (client != null)
                        {
                            client.Close();
                        }
                        _sendingConnections.Remove(neighbor);
                        _messages.Remove(neighbor);
                        Logger.Write("Connection to " + neighbor.IP + ":" + neighbor.Port + " failed");
                    }
                }
            }

            _pingThreadRunning = true;
            _pingThread = new Thread(PingThreadRun);
            _pingThread.Start();
        }

        /// <summary>
        /// Disconnect from the network.
        /// </summary>
        public void Disconnect()
        {
            Logger.Write("Shutting down");
            _pingThreadRunning = false;
            _connectionListenerThreadRunning = false;
            _messageListenerThreadRunning = false;
            _connectionListener.Stop();
        }

        /// <summary>
        /// Send a message to another node.
        /// </summary>
        /// <param name="sendTo">The node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public bool SendMessage(NodeProperties sendTo, string message)
        {
            return SendMessageInternal(sendTo, MessageType.User, message);
        }

        /// <summary>
        /// The run function for the connection listener thread.
        /// </summary>
        private void ConnectionListenerThreadRun()
        {
            while (_connectionListenerThreadRunning)
            {
                TcpClient incomingTcpClient = _connectionListener.AcceptTcpClient();
                IPEndPoint ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                NodeProperties incomingNodeProperties = new NodeProperties(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                lock (_lockObject)
                {
                    _recievingConnections[incomingNodeProperties] = new NetworkConnection() { Client = incomingTcpClient, LastPingRecieved = DateTime.UtcNow };
                }
                Logger.Write("Connection recieved from " + incomingNodeProperties.IP + ":" + incomingNodeProperties.Port);
            }
        }

        /// <summary>
        /// Gets a list of the neighboring nodes.
        /// </summary>
        /// <returns>The nodes that this node is connected to.</returns>
        private List<NodeProperties> GetNeighbors()
        {
            List<NodeProperties> ret = new List<NodeProperties>();
            lock (_lockObject)
            {
                foreach (NodeProperties node in _neighbors)
                {
                    ret.Add(node);
                }
            }

            return ret;
        }

        /// <summary>
        /// The run function for the message listener thread.
        /// </summary>
        private void MessageListenerThreadRun()
        {
            while (_messageListenerThreadRunning)
            {
                lock (_lockObject)
                {
                    foreach (var key in _recievingConnections.Keys)
                    {
                        if (!_messages.ContainsKey(key))
                        {
                            _messages[key] = new MessageBuilder();
                        }

                        int availableBytes = _recievingConnections[key].Client.Available;
                        if (availableBytes > 0)
                        {
                            byte[] buffer = new byte[availableBytes];
                            _recievingConnections[key].Client.GetStream().Read(buffer, 0, availableBytes);
                            _messages[key].Message.Append(Encoding.Default.GetString(buffer));

                            if (_messages[key].Length == -1 && _messages[key].Message.Length > 0)
                            {
                                StringBuilder messageLength = new StringBuilder();
                                for (int i = 0; i < _messages[key].Message.Length; ++i)
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
                                _recievedMessages.Enqueue(new Tuple<Message, NodeProperties>(new Message(_messages[key].Message.ToString(0, _messages[key].Length), key), key));
                                _messages[key].Message.Remove(0, _messages[key].Length);
                            }
                        }
                    }
                }

                ProcessMessages();

                Thread.Sleep(0);
            }
        }

        /// <summary>
        /// The run function for the ping thread.
        /// </summary>
        private void PingThreadRun()
        {
            while (_pingThreadRunning)
            {
                // Get a copy to avoid using a lock and causing a deadlock
                foreach (NodeProperties node in GetNeighbors())
                {
                    SendMessageInternal(node, MessageType.Ping, string.Empty);
                }

                Thread.Sleep(NetworkNode.PingFrequency * 1000);
            }
        }

        /// <summary>
        /// Processes all recieved messages.
        /// </summary>
        private void ProcessMessages()
        {
            while (_recievedMessages.Count > 0)
            {
                var messageObject = _recievedMessages.Dequeue();
                Logger.Write("Message recieved, " + messageObject.Item1.ToString());
                switch (messageObject.Item1.Type)
                {
                    case MessageType.Ping:
                        RecievedPing(messageObject.Item1.Sender, messageObject.Item2);
                        break;

                    case MessageType.User:
                        if (RecievedMessage != null)
                        {
                            RecievedMessage(this, new RecievedMessageEventArgs(messageObject.Item1.Data, messageObject.Item1.Sender));
                        }
                        break;

                    case MessageType.Unknown:
                        break;
                }
            }
        }

        /// <summary>
        /// Called when a ping is recieved.
        /// </summary>
        /// <param name="sender">The ping sender.</param>
        /// <param name="recievingConnection">The connection that recieved the ping.</param>
        private void RecievedPing(NodeProperties sender, NodeProperties recievingConnection)
        {
            lock (_lockObject)
            {
                if (_sendingConnections.ContainsKey(sender))
                {
                    _sendingConnections[sender].LastPingRecieved = DateTime.UtcNow;
                }

                if (_recievingConnections.ContainsKey(recievingConnection))
                {
                    _recievingConnections[recievingConnection].LastPingRecieved = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Creates the message to send it to a node.
        /// </summary>
        /// <param name="sendTo">The node to send it to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        private bool SendMessageInternal(NodeProperties sendTo, MessageType type, string message)
        {
            char typeChar = ' ';
            switch (type)
            {
                case MessageType.Ping:
                    typeChar = 'p';
                    break;

                case MessageType.User:
                    typeChar = 'u';
                    break;

                default:
                    return false;
            }

            string portString = _port.ToString() + ":";

            int length = message.Length + 1 + portString.Length;

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

            lock (_lockObject)
            {
                if (!_sendingConnections.ContainsKey(sendTo))
                {
                    TcpClient client = null;
                    try
                    {
                        Logger.Write("Attempting connection to " + sendTo.IP + ":" + sendTo.Port);
                        client = new TcpClient();
                        client.Connect(sendTo.IP, sendTo.Port);
                        _sendingConnections[sendTo] = new NetworkConnection() { Client = client, LastPingRecieved = DateTime.UtcNow };
                        Logger.Write("Connection to " + sendTo.IP + ":" + sendTo.Port + " successful");
                    }
                    catch
                    {
                        Logger.Write("Connection to " + sendTo.IP + ":" + sendTo.Port + " failed");
                        if (client != null)
                        {
                            client.Close();
                        }

                        return false;
                    }
                }

                byte[] buffer = Encoding.Default.GetBytes(length.ToString() + typeChar + portString + message);
                try
                {
                    _sendingConnections[sendTo].Client.GetStream().Write(buffer, 0, buffer.Length);
                    Logger.Write("Message sending successful");
                }
                catch
                {
                    Logger.Write("Message sending failed");
                    _sendingConnections[sendTo].Client.Close();
                    _sendingConnections.Remove(sendTo);
                    _messages.Remove(sendTo);
                    return false;
                }
            }

            return true;
        }
    }
}