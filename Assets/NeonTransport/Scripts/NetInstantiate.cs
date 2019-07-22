using OPS.Serialization.Attributes;
using UnityEngine;

namespace NeonNetworking.DataTypes
{
    [SerializeAbleClass]
    public class NetInstantiate
    {
        [SerializeAbleField(0)]
        public string objName;

        [SerializeAbleField(1)]
        public int prefabID;

        [SerializeAbleField(2)]
        public Vector3 pos;

        [SerializeAbleField(3)]
        public string instanceID;

        [SerializeAbleField(4)]
        public string senderID;
    }
}
