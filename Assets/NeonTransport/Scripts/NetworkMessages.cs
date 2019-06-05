using System;
using OPS.Serialization.Attributes;

namespace NeonNetworking.DataTypes
{
    [SerializeAbleClass]
    public class VectorMessage
    {
        public VectorMessage(float xin = 0, float yin = 0, float zin = 0)
        {
            x = xin;
            y = yin;
            z = zin;
        }

        [SerializeAbleField(0)]
        public float x;

        [SerializeAbleField(1)]
        public float y;

        [SerializeAbleField(2)]
        public float z;
    }

    [SerializeAbleClass]
    public class ClientIDMsg
    {
        [SerializeAbleField(0)]
        public string msg;

        [SerializeAbleField(1)]
        public string ID;
    }

    [SerializeAbleClass]
    public class NetDestroyMsg
    {
        [SerializeAbleField(0)]
        public string IDToDestroy;
    }
}
