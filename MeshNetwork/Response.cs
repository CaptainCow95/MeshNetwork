namespace MeshNetwork
{
    public class Response
    {
        private readonly bool _messageSent;
        private readonly Message _responseMessage;

        public Response(bool messageSent, Message response)
        {
            _messageSent = messageSent;
            _responseMessage = response;
        }

        public bool MessageSent
        {
            get { return _messageSent; }
        }

        public Message ResponseMessage
        {
            get { return _responseMessage; }
        }
    }
}