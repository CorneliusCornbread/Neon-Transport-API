using OPS.Serialization.Attributes;

namespace NeonNetworking
{
    [SerializeAbleClass]
    public class DisconnectEvent
    {
        [SerializeAbleField(0)]
        public string client;
    }
}
