namespace MeshNetwork
{
    /// <summary>
    /// An enumeration of the different types of messages.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// A message used to gain approval to the network.
        /// </summary>
        Approval,

        /// <summary>
        /// A message used to get all of a nodes neighbors.
        /// </summary>
        Neighbors,

        /// <summary>
        /// A message used as a ping.
        /// </summary>
        Ping,

        /// <summary>
        /// A message used to manage the system.
        /// </summary>
        System,

        /// <summary>
        /// A message that the user has sent.
        /// </summary>
        User,

        /// <summary>
        /// An unknown type of message.
        /// </summary>
        Unknown,
    }
}