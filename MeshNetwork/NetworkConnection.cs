using System;
using System.Net.Sockets;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a connection to another member of the network.
    /// </summary>
    public class NetworkConnection
    {
        /// <summary>
        /// A value indicating whether the connection has been approved to be on the network.
        /// </summary>
        private bool _approved = false;

        /// <summary>
        /// The client used to talk to the other member.
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// The last time the other member sent a ping.
        /// </summary>
        private DateTime _lastPingReceived;

        /// <summary>
        /// Gets or sets a value indicating whether the connection has been approved to be on the network.
        /// </summary>
        public bool Approved
        {
            get
            {
                return _approved;
            }

            set
            {
                _approved = value;
            }
        }

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