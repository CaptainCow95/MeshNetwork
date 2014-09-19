using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MeshNetwork
{
    public class NetworkNode
    {
        internal static int ConnectionTimeout = 20;
        internal static int PingFrequency = 10;
        private Thread _connectionListenerThread;
        private bool _connectionListenerThreadRunning;
        private volatile Dictionary<NodeProperties, NetworkConnection> _connections = new Dictionary<NodeProperties, NetworkConnection>();
        private object _lockObject;
        private Logger _logger;
        private Thread _messageListenerThread;
        private bool _messageListenerThreadRunning;
        private Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();
        private volatile List<NodeProperties> _neighbors = new List<NodeProperties>();
        private Thread _pingThread;
        private bool _pingThreadRunning;
        private int _port;
        private Queue<Message> _recievedMessages = new Queue<Message>();
        private TcpListener _socketListener;

        public NetworkNode(string logLocation)
        {
            _logger = new Logger(logLocation);
            _lockObject = new Object();
        }

        public delegate void RecievedMessageEventHandler(object source, RecievedMessageEventArgs args);

        public event RecievedMessageEventHandler RecievedMessage;

        public void ConnectToNetwork(int listeningPort, params string[] nodes)
        {
            _port = listeningPort;

            _logger.Write("Connecting to a network: listening on " + listeningPort);
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

            _socketListener = new TcpListener(IPAddress.Any, listeningPort);
            _socketListener.Start();
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
                        _logger.Write("Attempting connection to " + neighbor.IP + ":" + neighbor.Port);
                        //TcpClient client = new TcpClient(new IPEndPoint(neighbor.IP, neighbor.Port));
                        client = new TcpClient(new IPEndPoint(IPAddress.Loopback, _port));
                        client.Connect(new IPEndPoint(neighbor.IP, neighbor.Port));
                        _connections[neighbor] = new NetworkConnection() { Client = client, LastPingRecieved = DateTime.UtcNow };
                        _logger.Write("Connection to " + neighbor.IP + ":" + neighbor.Port + " successful");
                    }
                    catch (Exception)
                    {
                        if (client != null)
                        {
                            client.Close();
                        }
                        _connections.Remove(neighbor);
                        _messages.Remove(neighbor);
                        _logger.Write("Connection to " + neighbor.IP + ":" + neighbor.Port + " failed");
                    }
                }
            }

            _pingThreadRunning = true;
            _pingThread = new Thread(PingThreadRun);
            _pingThread.Start();
        }

        public void Disconnect()
        {
            _logger.Write("Shutting down");
            _pingThreadRunning = false;
            _connectionListenerThreadRunning = false;
            _messageListenerThreadRunning = false;
            _socketListener.Stop();
        }

        public bool SendMessage(NodeProperties sendTo, string message)
        {
            return SendMessageInternal(sendTo, MessageType.User, message);
        }

        private void ConnectionListenerThreadRun()
        {
            while (_connectionListenerThreadRunning)
            {
                TcpClient incomingTcpClient = _socketListener.AcceptTcpClient();
                IPEndPoint ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                NodeProperties incomingNodeProperties = new NodeProperties(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                lock (_lockObject)
                {
                    _connections[incomingNodeProperties] = new NetworkConnection() { Client = incomingTcpClient, LastPingRecieved = DateTime.UtcNow };
                }
                _logger.Write("Connection recieved from " + incomingNodeProperties.IP + ":" + incomingNodeProperties.Port);
            }
        }

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

        private void MessageListenerThreadRun()
        {
            while (_messageListenerThreadRunning)
            {
                lock (_lockObject)
                {
                    foreach (var key in _connections.Keys)
                    {
                        if (!_messages.ContainsKey(key))
                        {
                            _messages[key] = new MessageBuilder();
                        }

                        int availableBytes = _connections[key].Client.Available;
                        if (availableBytes > 0)
                        {
                            byte[] buffer = new byte[availableBytes];
                            _connections[key].Client.GetStream().Read(buffer, 0, availableBytes);
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
                                    }
                                }
                            }

                            if (_messages[key].Length != -1 && _messages[key].Message.Length >= _messages[key].Length)
                            {
                                _recievedMessages.Enqueue(new Message(_messages[key].Message.ToString(0, _messages[key].Length), key));
                                _messages[key].Message.Remove(0, _messages[key].Length);
                            }
                        }
                    }
                }

                ProcessMessages();

                Thread.Sleep(0);
            }
        }

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

        private void ProcessMessages()
        {
            while (_recievedMessages.Count > 0)
            {
                Message message = _recievedMessages.Dequeue();
                _logger.Write("Message recieved of type " + Enum.GetName(typeof(MessageType), message.Type) + ": " + message.Data);
                switch (message.Type)
                {
                    case MessageType.Ping:
                        RecievedPing(message.Sender);
                        break;

                    case MessageType.User:
                        if (RecievedMessage != null)
                        {
                            RecievedMessage(this, new RecievedMessageEventArgs(message.Data, message.Sender));
                        }
                        break;

                    case MessageType.Unknown:
                        break;
                }
            }
        }

        private void RecievedPing(NodeProperties sender)
        {
            lock (_lockObject)
            {
                _connections[sender].LastPingRecieved = DateTime.UtcNow;
            }
        }

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

            int length = message.Length + 1;

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
                if (!_connections.ContainsKey(sendTo))
                {
                    TcpClient client = null;
                    try
                    {
                        client = new TcpClient(new IPEndPoint(IPAddress.Loopback, _port));
                        client.Connect(sendTo.IP, sendTo.Port);
                        _connections[sendTo] = new NetworkConnection() { Client = client, LastPingRecieved = DateTime.UtcNow };
                    }
                    catch
                    {
                        if (client != null)
                        {
                            client.Close();
                        }

                        return false;
                    }
                }

                byte[] buffer = Encoding.Default.GetBytes(length.ToString() + typeChar + message);
                try
                {
                    _connections[sendTo].Client.GetStream().Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    _connections[sendTo].Client.Close();
                    _connections.Remove(sendTo);
                    _messages.Remove(sendTo);
                    return false;
                }
            }

            return true;
        }
    }
}