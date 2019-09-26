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

        public volatile byte messagesFromTargetCount = 0;

        //We use a byte to make sending ID's easier, we easily overflow this value however
        //if we are sending a lot of data
        /// <summary>
        /// List of messages recieved from this client instance
        /// </summary>
        public volatile object[] messagesFromTarget = new object[byte.MaxValue];

        public volatile byte messagesToTargetCount = 0;

        /// <summary>
        /// List of messages sent to this client instance
        /// </summary>
        private volatile object[] messagesToTargetVal = new object[byte.MaxValue];
        public volatile object lockObj2 = new object();
        public object[] messagesToTarget
        {
            get
            {
                lock (lockObj2)
                {
                    return messagesToTargetVal;
                }
            }

            set
            {
                lock (lockObj2)
                {
                    messagesToTargetVal = value;
                }
            }
        }
    }
}
