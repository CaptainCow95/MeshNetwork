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
        private DateTime _lastPingReceived;

        /// <summary>
        /// Gets or sets the client used to talk to the other member.
        /// </summary>
        public TcpClient Client
        {
            get { return _client; }
            set { _client = value; }
        }

        /// <summary>
        /// Gets or sets the last time the other member sent a ping.
        /// </summary>
        public DateTime LastPingReceived
        {
            get { return _lastPingReceived; }
            set { _lastPingReceived = value; }
        }
    }
}