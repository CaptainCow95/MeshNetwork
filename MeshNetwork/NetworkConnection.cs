using System;
using System.Net.Sockets;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a connection to another member of the network.
    /// </summary>
    internal class NetworkConnection
    {
        /// <summary>
        /// The client used to talk to the other member.
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// The last time the other member sent a ping.
        /// </summary>
        private DateTime _lastPingRecieved;

        /// <summary>
        /// Gets or sets the client used to talk to the other member.
        /// </summary>
        public TcpClient Client
        {
            get { return _client; }
            set { _client = value; }
        }

        /// <summary>
        /// Gets whether this client is still connected or not.
        /// </summary>
        public bool Connected { get { return _client.Connected && DateTime.UtcNow.Subtract(_lastPingRecieved).TotalSeconds > NetworkNode.ConnectionTimeout; } }

        /// <summary>
        /// Gets or sets the last time the other member sent a ping.
        /// </summary>
        public DateTime LastPingRecieved
        {
            get { return _lastPingRecieved; }
            set { _lastPingRecieved = value; }
        }
    }
}