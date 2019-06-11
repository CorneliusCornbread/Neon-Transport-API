namespace NeonNetworking.Enums
{
    public enum SendTarget
    {
        Owner,
        Server, 
        Clients,
        All
    }

    public enum MessageType
    {
        String,
        Int,
        Float,
        Bool,
        Bytes,
        Vector3,
        NetInstantiate,
        DisconnectEvent,
        PlayerData,
        ClientID,
        NetDestroy,
        MatchData
    }

    public enum SendMethod
    {
        /// <summary>
        /// Send data asynchronously on the main thread
        /// </summary>
        Async,
        /// <summary>
        /// Send data synchronously on the main thread
        /// </summary>
        Sync,
        /// <summary>
        /// Send data on another thread
        /// </summary>
        Threaded
    }
}
