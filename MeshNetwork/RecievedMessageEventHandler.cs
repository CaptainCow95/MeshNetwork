using System;

namespace MeshNetwork
{
    public class RecievedMessageEventArgs : EventArgs
    {
        private string _message;
        private NodeProperties _sender;

        public RecievedMessageEventArgs(string message, NodeProperties sender)
        {
            _message = message;
            _sender = sender;
        }

        public string Message
        {
            get { return _message; }
        }

        public NodeProperties Sender
        {
            get { return _sender; }
        }
    }
}