namespace MeshNetwork
{
    /// <summary>
    /// The results of connecting to a network.
    /// </summary>
    public enum ConnectToNetworkResults
    {
        /// <summary>
        /// A connection was successfully established.
        /// </summary>
        ConnectionSuccessful,

        /// <summary>
        /// A connection could not be made, so a new network was created.
        /// </summary>
        NewNetworkCreated
    }
}