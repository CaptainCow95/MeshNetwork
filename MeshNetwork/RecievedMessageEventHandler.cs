using System;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the event arguments for a recieved message event.
    /// </summary>
    public class RecievedMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The message that was recieved.
        /// </summary>
        private string _message;

        /// <summary>
        /// The sender of the message.
        /// </summary>
        private NodeProperties _sender;

        public RecievedMessageEventArgs(string message, NodeProperties sender)
        {
            _message = message;
            _sender = sender;
        }

        /// <summary>
        /// Gets the message that was recieved.
        /// </summary>
        public string Message
        {
            get { return _message; }
        }

        /// <summary>
        /// Gets the sender of the message.
        /// </summary>
        public NodeProperties Sender
        {
            get { return _sender; }
        }
    }
}