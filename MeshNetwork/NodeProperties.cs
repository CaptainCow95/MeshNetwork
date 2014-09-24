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
        private IPAddress _ip;

        /// <summary>
        /// The node's listening port.
        /// </summary>
        private int _port;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="networkAddress">The network address to parse in the format host:port.</param>
        public NodeProperties(string networkAddress)
        {
            string hostname = networkAddress.Split(':')[0];
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);
            _ip = hostEntry.AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (_ip == null)
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
            _ip = hostEntry.AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (_ip == null)
            {
                throw new Exception("Could not resolve hostname \"" + host + "\".");
            }

            _port = port;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="ip">The ip address.</param>
        /// <param name="port">The port.</param>
        public NodeProperties(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        /// <summary>
        /// Gets the ip address.
        /// </summary>
        public IPAddress IP { get { return _ip; } }

        /// <summary>
        /// Gets the port.
        /// </summary>
        public int Port { get { return _port; } }
    }
}