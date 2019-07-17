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
        MatchData,
        ServerMessage
    }

    public enum ServerMsgType
    {
        ConnectRequestEvent,
        ConnectAcceptEvent,
        /// <summary>
        /// Event When Another Client Disconnects
        /// </summary>
        ClientDisconnectEvent,
        PingEvent,
        PongEvent,
        /// <summary>
        /// Connection Check Event Checking Server Connection
        /// </summary>
        CCEvent,
        /// <summary>
        /// Response To CCEvent Confirming Connection
        /// </summary>
        CCAliveEvent,
        /// <summary>
        /// Match Data Request Event
        /// </summary>
        MatchRequestEvent,
        /// <summary>
        /// For When We're Disconnected
        /// </summary>
        ConnectionDisconnectEvent
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
