using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshNetwork
{
    /// <summary>
    /// Represents a mesh network where every node is connected to every other node.
    /// </summary>
    public class MeshNetworkNode : NetworkNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeshNetworkNode" /> class.
        /// </summary>
        public MeshNetworkNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MeshNetworkNode" /> class.
        /// </summary>
        /// <param name="logLocation">The location to log messages to.</param>
        /// <param name="logLevel">The highest level at which log messages will be written.</param>
        public MeshNetworkNode(string logLocation, LogLevels logLevel)
            : base(logLocation, logLevel)
        {
        }

        /// <inheritdoc></inheritdoc>
        public override ConnectToNetworkResults ConnectToNetwork(int listeningPort, IEnumerable<NodeProperties> initialNodes)
        {
            Logger.Write("Connecting to a mesh network: listening on " + listeningPort, LogLevels.Info);

            ConnectToNetworkResults result = ConnectToNetworkResults.NewNetworkCreated;

            BeforeConnectToNetwork(listeningPort);

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
                }
                else
                {
                    Logger.Write("Approval denied.", LogLevels.Info);
                }
            }

            AfterConnectToNetwork();

            Logger.Write("Connected and ready", LogLevels.Info);

            return result;
        }

        /// <inheritdoc></inheritdoc>
        protected override void ApprovalGranted(NodeProperties node)
        {
            ConnectToMembers(node);
        }

        /// <inheritdoc></inheritdoc>
        protected override void ApprovalRequestGranted(NodeProperties node)
        {
            ConnectToMembers(node);
        }

        /// <inheritdoc></inheritdoc>
        protected override string GetNetworkType()
        {
            return "mesh";
        }

        /// <summary>
        /// Connects to all members of the specified node.
        /// </summary>
        /// <param name="node">The node to connect to the members of.</param>
        private void ConnectToMembers(NodeProperties node)
        {
            var result = GetRemoteNeighbors(node);
            if (result.SendResult == SendResults.Success && result.ResponseResult == ResponseResults.Success)
            {
                Logger.Write(
                    "Attempting connection to the following neighbors: " + result.ResponseMessage.Data,
                    LogLevels.Debug);
                foreach (var neighbor in
                    result.ResponseMessage.Data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList()
                        .Select(e => new NodeProperties(e)))
                {
                    GetApproval(neighbor);
                }
            }
        }
    }
}