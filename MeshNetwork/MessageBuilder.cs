using System.Text;

namespace MeshNetwork
{
    internal class MessageBuilder
    {
        private int _length = -1;
        private StringBuilder _message = new StringBuilder();

        public int Length
        {
            get { return _length; }
            set { _length = value; }
        }

        public StringBuilder Message
        {
            get { return _message; }
        }
    }
}