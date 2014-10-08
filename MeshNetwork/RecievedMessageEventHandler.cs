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
        private readonly Message _message;

        public RecievedMessageEventArgs(Message message)
        {
            _message = message;
        }

        /// <summary>
        /// Gets the message that was recieved.
        /// </summary>
        public Message Message
        {
            get { return _message; }
        }
    }
}