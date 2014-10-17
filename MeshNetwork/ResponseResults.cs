namespace MeshNetwork
{
    /// <summary>
    /// The results of waiting for a response.
    /// </summary>
    public enum ResponseResults
    {
        /// <summary>
        /// The response was received successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The response was not received due to a connection failure.
        /// </summary>
        ConnectionFailure,

        /// <summary>
        /// The response was not received due to a timeout.
        /// </summary>
        Timeout,
    }
}