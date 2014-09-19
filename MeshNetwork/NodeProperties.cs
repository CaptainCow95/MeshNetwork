using System;
using System.Linq;
using System.Net;

namespace MeshNetwork
{
    public struct NodeProperties
    {
        private IPAddress _ip;

        private int _port;

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

        public NodeProperties(string host, int port)
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(host);
            if (hostEntry.AddressList.Length > 0)
            {
                _ip = hostEntry.AddressList[0];
            }
            else
            {
                throw new Exception("Could not resolve hostname \"" + host + "\".");
            }
            _port = port;
        }

        public NodeProperties(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public IPAddress IP { get { return _ip; } }

        public int Port { get { return _port; } }
    }
}