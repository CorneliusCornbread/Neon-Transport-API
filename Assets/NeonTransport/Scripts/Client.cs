using System.Net;
using System.Diagnostics;
using System;

namespace NeonNetworking
{
    [Serializable]
    public class Client
    {
        public volatile float lastReplyRec = 0;
        public EndPoint endPoint;
        public string ID;
        public float ping;
        public Stopwatch pingTimer = new Stopwatch();

        public byte messagesFromTargetCount = 0;

        //We use a byte to make sending ID's easier, we easily overflow this value however
        //if we are sending a lot of data
        /// <summary>
        /// List of messages recieved from this client instance
        /// </summary>
        public object[] messagesFromTarget = new object[byte.MaxValue];

        public byte messagesToTargetCount = 0;

        /// <summary>
        /// List of messages sent to this client instance
        /// </summary>
        public object[] messagesToTarget = new object[byte.MaxValue];
    }
}
