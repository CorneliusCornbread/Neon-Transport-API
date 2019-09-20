using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NeonNetworking.DataTypes;
using System.Threading;

namespace NeonNetworking
{
    public class ReliableMessageManager
    {
        public Thread RMessageThread { get; private set; }

        /// <summary>
        /// Function to handle a recieved reliable message on a server
        /// </summary>
        public void HandleRecMessage(ref Client sender, object message)
        {
            sender.messagesFromTarget[sender.messagesFromTargetCount] = message;

            sender.messagesFromTargetCount++;
        }
    }
}
