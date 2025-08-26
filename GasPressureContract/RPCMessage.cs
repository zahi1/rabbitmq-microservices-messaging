namespace Services
{
    /// <summary>
    /// Wrapper for RPC calls and responses in the Gas Pressure system.
    /// </summary>
    public class RPCMessage
    {
        /// <summary>
        /// Specifies the action type for the RPC call or response.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Contains the data associated with the action, serialized as a JSON string.
        /// </summary>
        public string Data { get; set; }
    }
}
