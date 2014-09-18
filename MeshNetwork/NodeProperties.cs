using System;

namespace MeshNetwork
{
    internal struct NodeProperties
    {
        private string _host;

        private int _port;

        public NodeProperties(string networkAddress)
        {
            try
            {
                _host = networkAddress.Split(':')[0];
                _port = int.Parse(networkAddress.Split(':')[1]);
            }
            catch (Exception)
            {
                throw new Exception("Malformed network address: " + networkAddress);
            }
        }

        public NodeProperties(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public string Host { get { return _host; } }

        public int Port { get { return _port; } }
    }
}