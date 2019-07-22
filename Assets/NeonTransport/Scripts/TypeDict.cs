using NeonNetworking.Enums;
using NeonNetworking.DataTypes;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace NeonNetworking
{
    public class TypeDict
    {
        public static Dictionary<MessageType, Type> dict = new Dictionary<MessageType, Type>()
        {
            { MessageType.String, typeof(string) },
            { MessageType.Int, typeof(int) },
            { MessageType.Float, typeof(float) },
            { MessageType.Bool, typeof(bool) },
            { MessageType.Bytes, typeof(byte[]) },
            { MessageType.Vector3, typeof(Vector3) },
            { MessageType.NetInstantiate, typeof(NetInstantiate) },
            { MessageType.PlayerData, typeof(PlayerData) },
            { MessageType.NetDestroy, typeof(NetDestroyMsg) },
            { MessageType.MatchData, typeof(MatchData) },
            { MessageType.ServerMessage, typeof(ServerMessage) }
        };
}
}
