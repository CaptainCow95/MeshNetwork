using System;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the event arguments for a received message event.
    /// </summary>
    public class ReceivedMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The message that was received.
        /// </summary>
        private readonly Message _message;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReceivedMessageEventArgs" /> class.
        /// </summary>
        /// <param name="message">The message that was received.</param>
        public ReceivedMessageEventArgs(Message message)
        {
            _message = message;
        }

        /// <summary>
        /// Gets the message that was received.
        /// </summary>
        public Message Message
        {
            get { return _message; }
        }
    }
}