using System.Threading;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the result of sending a message.
    /// </summary>
    public class MessageSendResult
    {
        /// <summary>
        /// An object to lock on.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// The progress of the message.
        /// </summary>
        private MessageSendProgress _progress;

        /// <summary>
        /// The results of sending the message.
        /// </summary>
        private SendResults _sendResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageSendResult" /> class.
        /// </summary>
        internal MessageSendResult()
        {
            _progress = MessageSendProgress.SendingMessage;
        }

        /// <summary>
        /// Gets the progress of the message.
        /// </summary>
        public MessageSendProgress Progress
        {
            get
            {
                return _progress;
            }
        }

        /// <summary>
        /// Gets the result of sending the message. This will block until the message is sent.
        /// </summary>
        public SendResults SendResult
        {
            get
            {
                BlockUntilProgress(MessageSendProgress.Completed);
                return _sendResult;
            }
        }

        /// <summary>
        /// Updates the send result.
        /// </summary>
        /// <param name="result">The result of sending the message.</param>
        internal void MessageSent(SendResults result)
        {
            lock (_lockObject)
            {
                _sendResult = result;
                _progress = MessageSendProgress.Completed;
            }
        }

        /// <summary>
        /// Blocks until the specified progress is reached.
        /// </summary>
        /// <param name="progressToBlockUntil">The progress to block until.</param>
        private void BlockUntilProgress(MessageSendProgress progressToBlockUntil)
        {
            bool block;
            lock (_lockObject)
            {
                block = _progress < progressToBlockUntil;
            }

            while (block)
            {
                Thread.Sleep(1);
                lock (_lockObject)
                {
                    block = _progress < progressToBlockUntil;
                }
            }
        }
    }
}