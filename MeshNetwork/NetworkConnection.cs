using System;
using System.Net.Sockets;

namespace MeshNetwork
{
    internal class NetworkConnection
    {
        private TcpClient _client;
        private DateTime _lastPingRecieved;

        public TcpClient Client
        {
            get { return _client; }
            set { _client = value; }
        }

        public bool Connected { get { return DateTime.UtcNow.Subtract(_lastPingRecieved).TotalSeconds > NetworkNode.ConnectionTimeout; } }

        public DateTime LastPingRecieved
        {
            get { return _lastPingRecieved; }
            set { _lastPingRecieved = value; }
        }
    }
}