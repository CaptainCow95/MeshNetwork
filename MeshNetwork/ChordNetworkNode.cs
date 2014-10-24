using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a mesh network where the nodes are connected in a ring.
    /// </summary>
    public class ChordNetworkNode : NetworkNode
    {
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
        /// The id of the current node.
        /// </summary>
        private string _id;

        /// <summary>
        /// The thread running the notify functions.
        /// </summary>
        private Thread _notifyThread;

        /// <summary>
        /// The preceding node in the ring.
        /// </summary>
        private NodeProperties _predecessor = null;

        /// <summary>
        /// The preceding node's id.
        /// </summary>
        private string _predecessorId = string.Empty;

        /// <summary>
        /// A value indicating whether the node is starting up.
        /// </summary>
        private bool _startup = false;

        /// <summary>
        /// The next node in the ring.
        /// </summary>
        private NodeProperties _successor = null;

        /// <summary>
        /// The next node's id.
        /// </summary>
        private string _successorId = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChordNetworkNode" /> class.
        /// </summary>
        public ChordNetworkNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChordNetworkNode" /> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        /// <param name="logLevel">The highest level at which log messages will be written.</param>
        public ChordNetworkNode(string logLocation, LogLevels logLevel)
            : base(logLocation, logLevel)
        {
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

            _id = NetworkInterface.GetAllNetworkInterfaces().First().GetPhysicalAddress().ToString() + listeningPort;

            ConnectToNetworkResults result = ConnectToNetworkResults.NewNetworkCreated;

            BeforeConnectToNetwork(listeningPort);

            _notifyThread = new Thread(NotifyThreadRun);
            _notifyThread.Start();

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
                    var result = SendMessageResponseInternal(node, MessageType.System, "successor", true);
                    if (result.SendResult == SendResults.Success && result.ResponseResult == ResponseResults.Success)
                    {
                        NodeProperties successorNode;
                        if (result.ResponseMessage.Data == string.Empty)
                        {
                            successorNode = node;
                        }
                        else
                        {
                            successorNode = new NodeProperties(result.ResponseMessage.Data);
                        }

                        GetApproval(successorNode);
                        var idResult = SendMessageResponseInternal(successorNode, MessageType.System, "id", true);
                        if (idResult.SendResult == SendResults.Success
                            && idResult.ResponseResult == ResponseResults.Success)
                        {
                            _successor = successorNode;
                            _successorId = idResult.ResponseMessage.Data;
                        }
                    }
                }

                _startup = false;
            }
        }

        /// <inheritdoc></inheritdoc>
        protected override string GetNetworkType()
        {
            return "chord";
        }

        /// <summary>
        /// Checks whether a string is between two other strings comparatively.
        /// </summary>
        /// <param name="s">The string to compare.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>True if the string is between the min and max values.</returns>
        protected bool IsBetween(string s, string min, string max)
        {
            if (s == string.Empty)
            {
                return false;
            }

            if (min == string.Empty || max == string.Empty)
            {
                return true;
            }

            if (string.CompareOrdinal(min, max) < 0)
            {
                return string.CompareOrdinal(s, min) > 0 && string.CompareOrdinal(s, max) < 0;
            }

            return string.CompareOrdinal(s, min) > 0 || string.CompareOrdinal(s, max) < 0;
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
                    string id = result.ResponseMessage.Data;
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
                switch (message.Data)
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
                        SendResponseInternal(message, MessageType.System, _id);
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
                                string id = idResult.ResponseMessage.Data;
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