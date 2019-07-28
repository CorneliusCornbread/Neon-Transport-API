﻿using System.Net;
using System.Diagnostics;

namespace NeonNetworking
{
    public class Client
    {
        public volatile float lastReplyRec = 0;
        public EndPoint endPoint;
        public string ID;
        public float ping;
        //public float pingMsgStartTime;
        public Stopwatch pingTimer = new Stopwatch();
        //public List<GameObject> ownedObjects = new List<GameObject>();
    }
}
