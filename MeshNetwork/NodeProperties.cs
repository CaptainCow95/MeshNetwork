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
        /// The node's IP address.
        /// </summary>
        private readonly IPAddress _ipAddress;

        /// <summary>
        /// The node's listening port.
        /// </summary>
        private readonly int _port;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> class.
        /// </summary>
        /// <param name="networkAddress">The network address to parse in the format host:port.</param>
        public NodeProperties(string networkAddress)
        {
            string hostname = networkAddress.Split(':')[0];
            _ipAddress = GetIpAddress(hostname);

            if (!int.TryParse(networkAddress.Split(':')[1], out _port))
            {
                throw new Exception("Malformed port.");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> class.
        /// </summary>
        /// <param name="host">The hostname or IP address of the node.</param>
        /// <param name="port">The port of the node.</param>
        public NodeProperties(string host, int port)
        {
            _ipAddress = GetIpAddress(host);
            _port = port;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeProperties" /> class.
        /// </summary>
        /// <param name="ipAddress">The IP address of the node.</param>
        /// <param name="port">The port of the node.</param>
        public NodeProperties(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        /// <summary>
        /// Gets the IP address.
        /// </summary>
        public IPAddress IpAddress
        {
            get
            {
                return _ipAddress;
            }
        }

        /// <summary>
        /// Gets the port.
        /// </summary>
        public int Port
        {
            get
            {
                return _port;
            }
        }

        /// <inheritdoc></inheritdoc>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var np = obj as NodeProperties;
            if (np == null)
            {
                return false;
            }

            return np._ipAddress.Equals(_ipAddress) && np._port.Equals(_port);
        }

        /// <inheritdoc></inheritdoc>
        public override int GetHashCode()
        {
            return _ipAddress.GetHashCode() ^ _port.GetHashCode();
        }

        /// <inheritdoc></inheritdoc>
        public override string ToString()
        {
            return _ipAddress.ToString() + ':' + this._port;
        }

        /// <summary>
        /// Gets the IP address associated with the specified hostname.
        /// </summary>
        /// <param name="hostname">The hostname to lookup the IP address of.</param>
        /// <returns>The IP address associated with the specified hostname.</returns>
        private IPAddress GetIpAddress(string hostname)
        {
            IPAddress address =
                Dns.GetHostEntry(hostname)
                    .AddressList.FirstOrDefault(e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (address != null && address.Equals(IPAddress.Loopback))
            {
                address =
                    Dns.GetHostEntry(IPAddress.Loopback)
                        .AddressList.FirstOrDefault(
                            e => e.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }

            if (address == null)
            {
                throw new Exception("Could not resolve hostname \"" + hostname + "\".");
            }

            return address;
        }
    }
}