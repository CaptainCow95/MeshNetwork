using System.Text;

namespace MeshNetwork
{
    /// <summary>
    /// An object that is used to help build up messages as they are recieved.
    /// </summary>
    internal class MessageBuilder
    {
        /// <summary>
        /// The length of the final message.
        /// </summary>
        private int _length = -1;

        /// <summary>
        /// The current contents of the message that have been recieved.
        /// </summary>
        private StringBuilder _message = new StringBuilder();

        /// <summary>
        /// Gets or sets the length of the final message.
        /// </summary>
        public int Length
        {
            get { return _length; }
            set { _length = value; }
        }

        /// <summary>
        /// Gets the current contents of the message.
        /// </summary>
        public StringBuilder Message
        {
            get { return _message; }
        }
    }
}