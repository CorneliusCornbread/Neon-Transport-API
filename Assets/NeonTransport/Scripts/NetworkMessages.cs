using System;
using NeonNetworking.Enums;
using OPS.Serialization.Attributes;
using UnityEngine;

namespace NeonNetworking.DataTypes
{
    [SerializeAbleClass]
    [Serializable]
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

    /* Client ID MSG
    [SerializeAbleClass]
    public class ClientIDMsg
    {
        [SerializeAbleField(0)]
        public string msg;

        [SerializeAbleField(1)]
        public string ID;
    }
    */

    [SerializeAbleClass]
    [Serializable]
    public class NetDestroyMsg
    {
        [SerializeAbleField(0)]
        public string IDToDestroy;
    }

    [SerializeAbleClass]
    [Serializable]
    public class MatchData
    {
        [SerializeAbleField(0)]
        public string MatchName;

        [SerializeAbleField(1)]
        public int PlayerCount;

        public System.Net.EndPoint sender;
    }

    [SerializeAbleClass]
    [Serializable]
    public class ServerMessage
    {
        [SerializeAbleField(0)]
        public ServerMsgType msgType;

        [SerializeAbleFieldOptional(1)]
        public string ID;
    }

    [SerializeAbleClass]
    [Serializable]
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
