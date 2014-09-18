using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MeshNetwork
{
    public class NetworkNode
    {
        private Dictionary<NodeProperties, TcpClient> _connections = new Dictionary<NodeProperties, TcpClient>();
        private Thread _listenerThread;
        private bool _listenerThreadRunning;
        private Logger _logger;
        private List<NodeProperties> _neighbors = new List<NodeProperties>();
        private TcpListener _socketListener;

        public NetworkNode(string logLocation)
        {
            _logger = new Logger(logLocation);
        }

        public void ConnectToNetwork(int listeningPort, params string[] nodes)
        {
            _logger.Write("Connecting to a network: listening on " + listeningPort);
            foreach (var address in nodes)
            {
                _neighbors.Add(new NodeProperties(address));
            }

            _socketListener = new TcpListener(System.Net.IPAddress.Any, listeningPort);
            _socketListener.Start();
            _listenerThreadRunning = true;
            _listenerThread = new Thread(ListenerThreadRun);
            _listenerThread.Start();

            foreach (var neighbor in _neighbors)
            {
                try
                {
                    _logger.Write("Attempting connection to " + neighbor.Host + ":" + neighbor.Port);
                    TcpClient client = new TcpClient(neighbor.Host, neighbor.Port);
                    _connections[neighbor] = client;
                    _logger.Write("Connection to " + neighbor.Host + ":" + neighbor.Port + " successful");
                }
                catch (Exception)
                {
                    _connections.Remove(neighbor);
                    _logger.Write("Connection to " + neighbor.Host + ":" + neighbor.Port + " failed");
                }
            }
        }

        public void Disconnect()
        {
            _logger.Write("Shutting down");
            _listenerThreadRunning = false;
            _socketListener.Stop();
            _listenerThread.Join();
        }

        private void ListenerThreadRun()
        {
            while (_listenerThreadRunning)
            {
                TcpClient incomingTcpClient = _socketListener.AcceptTcpClient();
                IPEndPoint ipEndPoint = (IPEndPoint)incomingTcpClient.Client.RemoteEndPoint;
                NodeProperties incomingNodeProperties = new NodeProperties(ipEndPoint.Address.ToString(), ipEndPoint.Port);
                _connections[incomingNodeProperties] = incomingTcpClient;
                _logger.Write("Connection recieved from " + incomingNodeProperties.Host + ":" + incomingNodeProperties.Port);
            }
        }
    }
}