using System;
using System.Linq;
using System.Net;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the properties of a node.
    /// </summary>
    public class NodeProperties
    {
        /// <summary>
        /// The node's listening port.
        /// </summary>
        private readonly int _port;

        /// <summary>
        /// The node's ip address.
        /// </summary>
        private IPAddress _ipAddress;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> structure.
        /// </summary>
        /// <param name="networkAddress">The network address to parse in the format host:port.</param>
        public NodeProperties(string networkAddress)
        {
            string hostname = networkAddress.Split(':')[0];
            GetIpAddress(hostname);

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
            GetIpAddress(host);
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

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            var np = obj as NodeProperties;
            if (np == null)
                return false;

            return np._ipAddress.Equals(_ipAddress) && np._port.Equals(_port);
        }

        public override int GetHashCode()
        {
            return _ipAddress.GetHashCode() ^ _port.GetHashCode();
        }

        private void GetIpAddress(string hostname)
        {
            _ipAddress =
                Dns.GetHostEntry(hostname)
                    .AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (_ipAddress != null && _ipAddress.Equals(IPAddress.Loopback))
            {
                _ipAddress =
                    Dns.GetHostEntry(IPAddress.Loopback)
                        .AddressList.FirstOrDefault(
                            e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }

            if (_ipAddress == null)
            {
                throw new Exception("Could not resolve hostname \"" + hostname + "\".");
            }
        }
    }
}