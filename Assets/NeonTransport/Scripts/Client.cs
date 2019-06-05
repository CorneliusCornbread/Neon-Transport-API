using System.Net;

namespace NeonNetworking
{
    public class Client
    {
        public float lastReplyRec = 0;
        public EndPoint endPoint;
        public string ID;
        public float ping;
        public float pingMsgStartTime;
        //public List<GameObject> ownedObjects = new List<GameObject>();
    }
}
