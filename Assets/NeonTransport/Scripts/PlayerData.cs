using OPS.Serialization.Attributes;
using UnityEngine;

namespace NeonNetworking.DataTypes
{
    [SerializeAbleClass]
    public class PlayerData
    {
        [SerializeAbleField(0)]
        public string playerID;

        [SerializeAbleField(1)]
        public Vector3 pos;

        [SerializeAbleField(2)]
        public Quaternion rot;

        [SerializeAbleField(3)]
        public Vector3 scale;

        [SerializeAbleField(4)]
        public string instanceID;
    }
}
