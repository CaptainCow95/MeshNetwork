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
        private object _lockObject = new object();
        private Thread _messageListenerThread;
        private bool _messageListenerThreadRunning;
        private Dictionary<NodeProperties, MessageBuilder> _messages = new Dictionary<NodeProperties, MessageBuilder>();
        private volatile List<NodeProperties> _neighbors = new List<NodeProperties>();
        private Thread _pingThread;
        private bool _pingThreadRunning;
        private int _port;
        private Queue<Tuple<Message, NodeProperties>> _recievedMessages = new Queue<Tuple<Message, NodeProperties>>();
        private volatile Dictionary<NodeProperties, NetworkConnection> _recievingConnections = new Dictionary<NodeProperties, NetworkConnection>();
        private volatile Dictionary<NodeProperties, NetworkConnection> _sendingConnections = new Dictionary<NodeProperties, NetworkConnection>();
        private TcpListener _socketListener;

        public NetworkNode()
        {
            Logger.Init(string.Empty);
        }

        public NetworkNode(string logLocation)
        {
            Logger.Init(logLocation);
        }

        public delegate void RecievedMessageEventHandler(object source, RecievedMessageEventArgs args);

        public event RecievedMessageEventHandler RecievedMessage;

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

        public void Disconnect()
        {
            Logger.Write("Shutting down");
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
                    _recievingConnections[incomingNodeProperties] = new NetworkConnection() { Client = incomingTcpClient, LastPingRecieved = DateTime.UtcNow };
                }
                Logger.Write("Connection recieved from " + incomingNodeProperties.IP + ":" + incomingNodeProperties.Port);
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