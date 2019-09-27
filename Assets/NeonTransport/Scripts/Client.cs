using System.Net;
using System.Diagnostics;
using System;

namespace NeonNetworking
{
    [Serializable]
    public class Client
    {
        public volatile float lastReplyRec = 0;
        public volatile EndPoint endPoint;
        public volatile string ID;
        public volatile float ping;
        public volatile Stopwatch pingTimer = new Stopwatch();

        public volatile byte messagesFromTargetCount = 0;
        public object lockObj1 { get; private set; } = new object();
        private volatile object[] messagesFromTargetVal = new object[byte.MaxValue];
        //We use a byte to make sending ID's easier, we easily overflow this value however
        //if we are sending a lot of data
        /// <summary>
        /// List of messages recieved from this client instance
        /// </summary>
        public object[] messagesFromTarget
        {
            get
            {
                lock (lockObj1)
                {
                    return messagesFromTargetVal;
                }
            }

            set
            {
                lock (lockObj1)
                {
                    messagesFromTargetVal = value;
                }
            }
        }

        public volatile byte messagesToTargetCount = 0;
        public object lockObj2 { get; private set; } = new object();
        private volatile object[] messagesToTargetVal = new object[byte.MaxValue];
        /// <summary>
        /// List of messages sent to this client instance
        /// </summary>
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
