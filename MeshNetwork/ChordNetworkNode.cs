using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a mesh network where the nodes are connected in a ring.
    /// </summary>
    public class ChordNetworkNode : NetworkNode
    {
        /// <summary>
        /// The object to lock on when handling the _findSuccessorQueue object.
        /// </summary>
        private readonly object _findsuccessorLockObject = new object();

        /// <summary>
        /// A queue of the successor to find.
        /// </summary>
        private readonly Queue<Message> _findSuccessorQueue = new Queue<Message>();

        /// <summary>
        /// A list of the fingers and which nodes they represent.
        /// </summary>
        private readonly List<Tuple<NodeProperties, int>> _fingerTable = new List<Tuple<NodeProperties, int>>();

        /// <summary>
        /// The object to lock on when handling the finger table.
        /// </summary>
        private readonly object _fingerTableLockObject = new object();

        /// <summary>
        /// A queue of the nodes to run the Notify object on.
        /// </summary>
        private readonly Queue<NodeProperties> _notifiedNodes = new Queue<NodeProperties>();

        /// <summary>
        /// The object to lock on when handling the _notifyNodes object.
        /// </summary>
        private readonly object _notifyNodesLockObject = new object();

        /// <summary>
        /// The object to lock on when handling successor and predecessor objects.
        /// </summary>
        private readonly object _successorPredecessorLockObject = new object();

        /// <summary>
        /// The thread running the find successor thread.
        /// </summary>
        private Thread _findSuccessorThread;

        /// <summary>
        /// The id of the current node.
        /// </summary>
        private int _id;

        /// <summary>
        /// The thread running the notify functions.
        /// </summary>
        private Thread _notifyThread;

        /// <summary>
        /// The preceding node in the ring.
        /// </summary>
        private NodeProperties _predecessor;

        /// <summary>
        /// The preceding node's id.
        /// </summary>
        private int _predecessorId = -1;

        /// <summary>
        /// A value indicating whether the node is starting up.
        /// </summary>
        private bool _startup;

        /// <summary>
        /// The next node in the ring.
        /// </summary>
        private NodeProperties _successor;

        /// <summary>
        /// The next node's id.
        /// </summary>
        private int _successorId = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChordNetworkNode"/> class.
        /// </summary>
        public ChordNetworkNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChordNetworkNode"/> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        /// <param name="logLevel">The highest level at which log messages will be written.</param>
        public ChordNetworkNode(string logLocation, LogLevels logLevel)
            : base(logLocation, logLevel)
        {
        }

        /// <summary>
        /// Gets the id of the node.
        /// </summary>
        public int Id
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// Gets the preceding node in the ring.
        /// </summary>
        public NodeProperties Predecessor
        {
            get
            {
                return _predecessor;
            }
        }

        /// <summary>
        /// Gets the next node in the ring.
        /// </summary>
        public NodeProperties Successor
        {
            get
            {
                return _successor;
            }
        }

        /// <inheritdoc></inheritdoc>
        public override ConnectToNetworkResults ConnectToNetwork(int listeningPort, IEnumerable<NodeProperties> initialNodes)
        {
            Logger.Write("Connecting to a chord network: listening on " + listeningPort, LogLevels.Info);

            _id = new Random().Next();

            ConnectToNetworkResults result = ConnectToNetworkResults.NewNetworkCreated;

            BeforeConnectToNetwork(listeningPort);

            _notifyThread = new Thread(NotifyThreadRun);
            _notifyThread.Start();

            _findSuccessorThread = new Thread(FindSuccessorThreadRun);
            _findSuccessorThread.Start();

            for (int i = 0; i < 31; ++i)
            {
                _fingerTable.Add(null);
            }

            _startup = true;

            foreach (var neighbor in initialNodes)
            {
                if (IsSelf(neighbor))
                {
                    continue;
                }

                ReconnectNodes.Add(neighbor);

                Logger.Write("Establishing connection and getting approval.", LogLevels.Info);
                if (GetApproval(neighbor))
                {
                    result = ConnectToNetworkResults.ConnectionSuccessful;
                    break;
                }

                Logger.Write("Approval denied.", LogLevels.Info);
            }

            while (_startup && result != ConnectToNetworkResults.NewNetworkCreated)
            {
                Thread.Sleep(1);
            }

            AfterConnectToNetwork();

            Logger.Write("Connected and ready", LogLevels.Info);

            return result;
        }

        /// <summary>
        /// Gets all the fingers that have been sorted out.
        /// </summary>
        /// <returns>The fingers that have been sorted out.</returns>
        public List<NodeProperties> GetFingers()
        {
            lock (_fingerTableLockObject)
            {
                return new List<NodeProperties>(_fingerTable.Where(e => e != null).Select(e => e.Item1));
            }
        }

        /// <summary>
        /// Gets the node containing the id.
        /// </summary>
        /// <param name="id">The id to lookup.</param>
        /// <returns>The node containing the id, null if it is the current node.</returns>
        public NodeProperties GetNodeContainingId(int id)
        {
            var node = FindSuccessor(id);
            if (node == null)
            {
                return null;
            }

            return node.Item1;
        }

        /// <summary>
        /// Send a message to another node, looking up the node containing the id.
        /// </summary>
        /// <param name="id">The id used to lookup the node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A value indicating whether the message was successfully sent.</returns>
        public MessageSendResult SendChordMessage(int id, string message)
        {
            var node = GetNodeContainingId(id);
            if (node == null)
            {
                var result = new MessageSendResult();
                result.MessageSent(SendResults.SendingToSelfFailure);
                return result;
            }

            return SendMessage(node, message);
        }

        /// <summary>
        /// Sends a message to another node, looking up the node containing the id, and awaits a response.
        /// </summary>
        /// <param name="id">The id used to lookup the node to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>
        /// A response object that contains whether the message was successfully sent and the
        /// response from the receiver.
        /// </returns>
        public MessageResponseResult SendChordMessageResponse(int id, string message)
        {
            var node = GetNodeContainingId(id);
            if (node == null)
            {
                var result = new MessageResponseResult();
                result.MessageSent(SendResults.SendingToSelfFailure);
                return result;
            }

            return SendMessageResponse(node, message);
        }

        /// <inheritdoc></inheritdoc>
        protected override void ApprovalGranted(NodeProperties node)
        {
        }

        /// <inheritdoc></inheritdoc>
        protected override void ApprovalRequestGranted(NodeProperties node)
        {
            if (_startup)
            {
                lock (_successorPredecessorLockObject)
                {
                    var response = SendMessageResponseInternal(node, MessageType.System, "findsuccessor|" + _id, false);

                    if (response.SendResult == SendResults.Success && response.ResponseResult == ResponseResults.Success)
                    {
                        if (response.ResponseMessage.Data == string.Empty)
                        {
                            var idResponse = SendMessageResponseInternal(node, MessageType.System, "id", false);
                            if (idResponse.SendResult == SendResults.Success
                                && idResponse.ResponseResult == ResponseResults.Success)
                            {
                                _successor = node;
                                _successorId = int.Parse(idResponse.ResponseMessage.Data);
                            }
                        }
                        else
                        {
                            string[] result = response.ResponseMessage.Data.Split('|');
                            _successor = new NodeProperties(result[0]);
                            _successorId = int.Parse(result[1]);
                        }
                    }
                }

                _startup = false;
            }
        }

        /// <summary>
        /// Gets the closest preceding node for a id.
        /// </summary>
        /// <param name="id">The id to search for.</param>
        /// <returns>The closest preceding node for the id specified.</returns>
        protected Tuple<NodeProperties, int> ClosestPrecedingNode(int id)
        {
            lock (_fingerTableLockObject)
            {
                for (int i = _fingerTable.Count - 1; i >= 0; --i)
                {
                    if (_fingerTable[i] != null && IsBetween(_fingerTable[i].Item2, _id, id))
                    {
                        return _fingerTable[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the successor node for the specified id.
        /// </summary>
        /// <param name="id">The id to find the successor node for.</param>
        /// <returns>The successor node for the specified id.</returns>
        protected Tuple<NodeProperties, int> FindSuccessor(int id)
        {
            if (IsBetween(id, _id, _successorId))
            {
                if (_successor == null)
                {
                    return null;
                }

                return new Tuple<NodeProperties, int>(_successor, _successorId);
            }

            var closest = ClosestPrecedingNode(id);
            if (closest == null)
            {
                return null;
            }

            var response = SendMessageResponseInternal(
                closest.Item1,
                MessageType.System,
                "findsuccessor|" + id,
                false);

            if (response.SendResult == SendResults.Success && response.ResponseResult == ResponseResults.Success)
            {
                if (response.ResponseMessage.Data == string.Empty)
                {
                    return closest;
                }

                string[] result = response.ResponseMessage.Data.Split('|');
                return new Tuple<NodeProperties, int>(new NodeProperties(result[0]), int.Parse(result[1]));
            }

            return null;
        }

        /// <summary>
        /// Fixes the finger table.
        /// </summary>
        protected void FixFingers()
        {
            lock (_fingerTableLockObject)
            {
                for (int i = 0; i < 31; ++i)
                {
                    _fingerTable[i] = FindSuccessor(GetNextId(_id, i));
                }
            }
        }

        /// <inheritdoc></inheritdoc>
        protected override string GetNetworkType()
        {
            return "chord";
        }

        /// <summary>
        /// Gets the next id by adding 1^power to start and wrapping around all positive integers.
        /// </summary>
        /// <param name="start">The starting value.</param>
        /// <param name="power">The power to add.</param>
        /// <returns>
        /// The next id by adding 1^power to start and wrapping around all positive integers.
        /// </returns>
        protected int GetNextId(int start, int power)
        {
            uint temp = (uint)start;
            temp += (uint)(1 << power);
            if (temp > int.MaxValue)
            {
                temp -= int.MaxValue;
            }

            return (int)temp;
        }

        /// <summary>
        /// Checks whether a number is between two other numbers.
        /// </summary>
        /// <param name="i">The number to compare.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>True if the number is between the min and max values.</returns>
        /// <remarks>
        /// If the number to check is negative, this returns false. If either the min or the max is
        /// negative, this returns true.
        /// </remarks>
        protected bool IsBetween(int i, int min, int max)
        {
            if (i < 0)
            {
                return false;
            }

            if (min < 0 || max < 0)
            {
                return true;
            }

            if (min < max)
            {
                return i > min && i < max;
            }

            return i > min || i < max;
        }

        /// <summary>
        /// A node has just stabilized to be connected to this node.
        /// </summary>
        /// <param name="node">The node that just connected to this node.</param>
        protected void Notify(NodeProperties node)
        {
            lock (_successorPredecessorLockObject)
            {
                GetApproval(node);
                var result = SendMessageResponseInternal(node, MessageType.System, "id", true);
                if (result.SendResult == SendResults.Success && result.ResponseResult == ResponseResults.Success)
                {
                    int id = int.Parse(result.ResponseMessage.Data);
                    if (_predecessor == null || IsBetween(id, _predecessorId, _id))
                    {
                        _predecessor = node;
                        _predecessorId = id;
                    }
                }
            }
        }

        /// <inheritdoc></inheritdoc>
        protected override void ReceivedSystemMessage(Message message)
        {
            if (message.AwaitingResponse)
            {
                // messages needing a response.
                switch (message.Data.Split('|')[0])
                {
                    case "successor":
                        if (_successor == null)
                        {
                            SendResponseInternal(message, MessageType.System, string.Empty);
                        }
                        else
                        {
                            SendResponseInternal(message, MessageType.System, _successor.ToString());
                        }

                        break;

                    case "predecessor":
                        if (_predecessor == null)
                        {
                            SendResponseInternal(message, MessageType.System, string.Empty);
                        }
                        else
                        {
                            SendResponseInternal(message, MessageType.System, _predecessor.ToString());
                        }

                        break;

                    case "id":
                        SendResponseInternal(message, MessageType.System, _id.ToString(CultureInfo.InvariantCulture));
                        break;

                    case "findsuccessor":
                        lock (_findsuccessorLockObject)
                        {
                            _findSuccessorQueue.Enqueue(message);
                        }

                        break;
                }
            }
            else
            {
                // messages not needing a response.
                switch (message.Data)
                {
                    case "notify":
                        lock (_notifyNodesLockObject)
                        {
                            _notifiedNodes.Enqueue(message.Sender);
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Tries to stabilize the network.
        /// </summary>
        protected void Stabilize()
        {
            lock (_successorPredecessorLockObject)
            {
                if (_successor == null)
                {
                    if (IsBetween(_predecessorId, _id, _successorId))
                    {
                        _successor = _predecessor;
                        _successorId = _predecessorId;
                    }

                    // Only notify if we actually have someone to notify besides ourselves.
                    if (_successor != null)
                    {
                        GetApproval(_successor);
                        SendMessageInternal(_successor, MessageType.System, "notify", true);
                    }
                }
                else
                {
                    GetApproval(_successor);
                    var result = SendMessageResponseInternal(_successor, MessageType.System, "predecessor", true);
                    if (result.SendResult == SendResults.Success && result.ResponseResult == ResponseResults.Success)
                    {
                        string nodeString = result.ResponseMessage.Data;
                        if (nodeString != string.Empty)
                        {
                            var node = new NodeProperties(nodeString);
                            GetApproval(node);
                            var idResult = SendMessageResponseInternal(node, MessageType.System, "id", true);
                            if (idResult.SendResult == SendResults.Success
                                && idResult.ResponseResult == ResponseResults.Success)
                            {
                                int id = int.Parse(idResult.ResponseMessage.Data);
                                if (IsBetween(id, _id, _successorId))
                                {
                                    _successor = node;
                                    _successorId = id;
                                }
                            }
                        }
                    }

                    SendMessageInternal(_successor, MessageType.System, "notify", true);
                }
            }
        }

        /// <inheritdoc></inheritdoc>
        protected override void UpdateNetwork()
        {
            Stabilize();

            FixFingers();
        }

        /// <summary>
        /// The run function for the find successor thread.
        /// </summary>
        private void FindSuccessorThreadRun()
        {
            while (ChildThreadsRunning)
            {
                int count;
                lock (_findsuccessorLockObject)
                {
                    count = _findSuccessorQueue.Count;
                }

                while (count > 0)
                {
                    Message message;
                    lock (_findsuccessorLockObject)
                    {
                        message = _findSuccessorQueue.Dequeue();
                    }

                    var result = FindSuccessor(int.Parse(message.Data.Split('|')[1]));
                    if (result == null)
                    {
                        SendResponseInternal(message, MessageType.System, string.Empty);
                    }
                    else
                    {
                        SendResponseInternal(message, MessageType.System, result.Item1.ToString() + '|' + result.Item2);
                    }

                    lock (_findsuccessorLockObject)
                    {
                        count = _findSuccessorQueue.Count;
                    }
                }

                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// The run function for the notify thread.
        /// </summary>
        private void NotifyThreadRun()
        {
            while (ChildThreadsRunning)
            {
                int count;
                lock (_notifyNodesLockObject)
                {
                    count = _notifiedNodes.Count;
                }

                while (count > 0)
                {
                    NodeProperties node;
                    lock (_notifyNodesLockObject)
                    {
                        node = _notifiedNodes.Dequeue();
                    }

                    Notify(node);

                    lock (_notifyNodesLockObject)
                    {
                        count = _notifiedNodes.Count;
                    }
                }

                Thread.Sleep(1);
            }
        }
    }
}