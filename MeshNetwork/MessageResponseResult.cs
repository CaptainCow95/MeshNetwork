using System.Threading;

namespace MeshNetwork
{
    /// <summary>
    /// Represents the result of sending a message and waiting for a response.
    /// </summary>
    public class MessageResponseResult
    {
        /// <summary>
        /// An object to lock on.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// The progress of the message.
        /// </summary>
        private MessageResponseProgress _progress;

        /// <summary>
        /// The message that was sent in response.
        /// </summary>
        private Message _responseMessage;

        /// <summary>
        /// The results of waiting for the response.
        /// </summary>
        private ResponseResults _responseResult;

        /// <summary>
        /// The results of sending the message.
        /// </summary>
        private SendResults _sendResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageResponseResult" /> class.
        /// </summary>
        internal MessageResponseResult()
        {
            _progress = MessageResponseProgress.SendingMessage;
        }

        /// <summary>
        /// Gets the progress of the message.
        /// </summary>
        public MessageResponseProgress Progress
        {
            get
            {
                return _progress;
            }
        }

        /// <summary>
        /// Gets the message that was sent in response. This will block until the response is
        /// received or an error occurs during receiving, in which case this will be null.
        /// </summary>
        public Message ResponseMessage
        {
            get
            {
                BlockUntilProgress(MessageResponseProgress.Completed);
                return _responseMessage;
            }
        }

        /// <summary>
        /// Gets the result of waiting for the response. This will block until the response is
        /// received or an error occurs during receiving. If this is set to Success, the the message
        /// was successfully sent and a response was received.
        /// </summary>
        public ResponseResults ResponseResult
        {
            get
            {
                BlockUntilProgress(MessageResponseProgress.Completed);
                return _responseResult;
            }
        }

        /// <summary>
        /// Gets the result of sending the message. This will block until the message is sent.
        /// </summary>
        public SendResults SendResult
        {
            get
            {
                BlockUntilProgress(MessageResponseProgress.WaitingForResponse);
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
                if (result != SendResults.Success)
                {
                    _responseResult = ResponseResults.ConnectionFailure;
                    _progress = MessageResponseProgress.Completed;
                }
                else
                {
                    _progress = MessageResponseProgress.WaitingForResponse;
                }
            }
        }

        /// <summary>
        /// Updates the response and response result.
        /// </summary>
        /// <param name="result">The result of waiting for the response.</param>
        /// <param name="response">The response message.</param>
        internal void ResponseReceived(ResponseResults result, Message response)
        {
            lock (_lockObject)
            {
                _responseResult = result;
                _responseMessage = response;
                _progress = MessageResponseProgress.Completed;
            }
        }

        /// <summary>
        /// Blocks until the specified progress is reached.
        /// </summary>
        /// <param name="progressToBlockUntil">The progress to block until.</param>
        private void BlockUntilProgress(MessageResponseProgress progressToBlockUntil)
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