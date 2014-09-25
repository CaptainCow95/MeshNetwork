using System;
using System.Linq;
using System.Net;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the properties of a node.
    /// </summary>
    public struct NodeProperties
    {
        /// <summary>
        /// The node's ip address.
        /// </summary>
        private readonly IPAddress _ipAddress;

        /// <summary>
        /// The node's listening port.
        /// </summary>
        private readonly int _port;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="networkAddress">The network address to parse in the format host:port.</param>
        public NodeProperties(string networkAddress)
        {
            string hostname = networkAddress.Split(':')[0];
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);
            _ipAddress = hostEntry.AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (_ipAddress == null)
            {
                throw new Exception("Could not resolve hostname \"" + hostname + "\".");
            }

            if (!int.TryParse(networkAddress.Split(':')[1], out _port))
            {
                throw new Exception("Malformed port.");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="host">The hostname or ip address.</param>
        /// <param name="port">The port.</param>
        public NodeProperties(string host, int port)
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(host);
            _ipAddress = hostEntry.AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (_ipAddress == null)
            {
                throw new Exception("Could not resolve hostname \"" + host + "\".");
            }

            _port = port;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="ipAddress">The ip address.</param>
        /// <param name="port">The port.</param>
        public NodeProperties(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        /// <summary>
        /// Gets the ip address.
        /// </summary>
        public IPAddress IpAddress { get { return _ipAddress; } }

        /// <summary>
        /// Gets the port.
        /// </summary>
        public int Port { get { return _port; } }
    }
}