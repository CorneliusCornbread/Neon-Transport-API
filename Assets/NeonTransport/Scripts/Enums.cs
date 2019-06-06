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
        Async,
        Sync,
        Threaded
    }
}
