namespace MeshNetwork
{
    /// <summary>
    /// Represents the details of an approved node request.
    /// </summary>
    internal struct ApprovedNodeDetails
    {
        /// <summary>
        /// A value indicating whether approval was granted or received.
        /// </summary>
        private readonly bool _approvalGranted;

        /// <summary>
        /// The node that needs to be acted on.
        /// </summary>
        private readonly NodeProperties _node;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApprovedNodeDetails"/> structure.
        /// </summary>
        /// <param name="node">The node that needs to be acted on.</param>
        /// <param name="approvalGranted">A value indicating whether approval was granted or received.</param>
        public ApprovedNodeDetails(NodeProperties node, bool approvalGranted)
        {
            _node = node;
            _approvalGranted = approvalGranted;
        }

        /// <summary>
        /// Gets a value indicating whether approval was granted or received.
        /// </summary>
        public bool ApprovalGranted
        {
            get
            {
                return _approvalGranted;
            }
        }

        /// <summary>
        /// Gets the node that needs to be acted on.
        /// </summary>
        public NodeProperties Node
        {
            get
            {
                return _node;
            }
        }
    }
}