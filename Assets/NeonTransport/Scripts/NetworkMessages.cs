using System;
using NeonNetworking.Enums;
using OPS.Serialization.Attributes;
using UnityEngine;

namespace NeonNetworking.DataTypes
{
    [ClassInheritance(typeof(VectorMessage), 0)]
    [ClassInheritance(typeof(NetDestroyMsg), 1)]
    [ClassInheritance(typeof(MatchData), 2)]
    [ClassInheritance(typeof(ServerMessage), 3)]
    [ClassInheritance(typeof(PlayerData), 4)]
    [SerializeAbleClass]
    [Serializable]
    public class MessageBase //You can have a class inheritance tag but the class doesn't HAVE to inherit from message base
    {
        [SerializeAbleField(0)]
        public bool reliable = false;

        [SerializeAbleField(1)]
        public byte messageNum = 0;
    }

    [SerializeAbleClass]
    [Serializable]
    public class VectorMessage : MessageBase
    {
        [SerializeAbleField(0)]
        public float x;

        [SerializeAbleField(1)]
        public float y;

        [SerializeAbleField(2)]
        public float z;
    }

    [SerializeAbleClass]
    [Serializable]
    public class NetDestroyMsg : MessageBase
    {
        [SerializeAbleField(0)]
        public string IDToDestroy;
    }

    [SerializeAbleClass]
    [Serializable]
    public class MatchData : MessageBase
    {
        [SerializeAbleField(0)]
        public string matchName;

        [SerializeAbleField(1)]
        public int playerCount;

        public System.Net.EndPoint sender;
    }

    [SerializeAbleClass]
    [Serializable]
    public class ServerMessage : MessageBase
    {
        [SerializeAbleField(0)]
        public ServerMsgType msgType;

        [SerializeAbleFieldOptional(1)]
        public string ID;
    }

    [SerializeAbleClass]
    [Serializable]
    public class PlayerData : MessageBase
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
