using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NeonNetworking.DataTypes;
using NeonNetworking.Enums;
using OPS.Serialization.IO;
using System.Collections.Concurrent;

namespace NeonNetworking
{
    public class MatchManager
    {
        private Thread LANMatchSendThread;
        private Thread LANMatchRecieveThread;
        public Socket LANMatchSocket { get; private set; }

        public ConcurrentQueue<MatchData> pendingMatchData = new ConcurrentQueue<MatchData>();

        /// <summary>
        /// Amount of time in milliseconds we wait 
        /// </summary>
        private const int matchBroadcastInterval = 500;

        private NetworkManager man
        {
            get
            {
                return NetworkManager.Instance;
            }
        }

        public bool SearchingForMatches { get; private set; } = false;
        public bool IsBroadcastingMatch { get; private set; } = false;

        #region Cleanup
        ~MatchManager()
        {
            if (LANMatchSendThread != null)
                LANMatchSendThread.Abort();

            if (LANMatchRecieveThread != null)
                LANMatchRecieveThread.Abort();

            if (LANMatchSocket != null)
                LANMatchSocket.Close();
        }
        #endregion

        /// <summary>
        /// Used when we want match data from available servers
        /// </summary>
        /// <param name="target">Target we want match data from</param>
        public void RecieveMatches(EndPoint[] targets)
        {
            if (LANMatchSocket == null)
            {
                LANMatchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }

            byte[] packet = man.prepSend("MATCHREQUEST");

            Parallel.ForEach(targets, e => MatchRequest(packet, e));
        }

        private void MatchRequest(byte[] packet, EndPoint e)
        {
            LANMatchSocket.SendTo(packet, e);
        }

        /// <summary>
        /// Used to stop the broadcast of the current match
        /// </summary>
        public void StopMatchBroadcast()
        {
            if (!IsBroadcastingMatch)
                throw new InvalidOperationException("Cannot stop match broadcast without starting match broadcast");

            IsBroadcastingMatch = false;
            LANMatchSendThread.Abort();
            LANMatchSocket.Dispose();
        }

        /// <summary>
        /// Used to broadcast current match if there is one
        /// </summary>
        public void StartMatchBroadcast()
        {
            if (man.isQuitting)
            {
                Debug.LogWarning("Cannot broadcast a match when there is no match");
                return;
            }

            else if (!man.isServer)
            {
                Debug.LogError("Cannot broadcast a match when you're a client");
                return;
            }

            else if (IsBroadcastingMatch)
            {
                Debug.LogError("We've already started broadcasting");
                return;
            }

            else if (man.highDebug)
                Debug.LogWarning("Match broadcast");


            LANMatchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            LANMatchSocket.EnableBroadcast = true;

            IsBroadcastingMatch = true;
            LANMatchSendThread = new Thread(MatchSend);
            LANMatchSendThread.Start();
        }

        /// <summary>
        /// Function used to start recieving LAN matches
        /// </summary>
        public void StartLANMatchRecieve()
        {
            if (LANMatchSocket == null)
                LANMatchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            LANMatchSocket.EnableBroadcast = true;

            IPEndPoint p = new IPEndPoint(IPAddress.Any, NetworkManager.LANBroadcastPort);

            try
            {
                LANMatchSocket.Bind(p);
            }

            catch (Exception e)
            {
                string error = "Unable to bind to recieve LAN broadcast messages, is there another instance of a broadcasting game running?: " + e;
                throw new InvalidOperationException(error);
            }

            SearchingForMatches = true;

            LANMatchRecieveThread = new Thread(MatchRecieve);
            LANMatchRecieveThread.Start();
        }

        /// <summary>
        /// Internal method used to serialize and send match messages on another thread
        /// </summary>
        private void MatchSend()
        {
            if (man.highDebug)
                Debug.LogWarning("Threaded match send start");

            MatchData match = new MatchData
            {
                MatchName = man.ServerName,
                PlayerCount = man.connectedClients.Count
            };

            IPEndPoint target = new IPEndPoint(IPAddress.Broadcast, NetworkManager.LANBroadcastPort);

            byte[] packet;

            try
            {
                packet = man.prepSend(match);
            }

            catch (Exception ex)
            {
                string message = "Recieved exception from prepSend: " + ex.ToString();
                Debug.LogError(message + ", MSG: " + match);
                throw new Exception(message);
            }

            LANMatchSocket.SendTo(packet, target);

            if (man.highDebug)
                Debug.LogWarning("Threaded match send end");

            Thread.Sleep(matchBroadcastInterval);
            MatchSend();
        }

        private void MatchRecieve()
        {
            if (Thread.CurrentThread.ManagedThreadId == man.mainThread.ManagedThreadId)
            {
                Debug.LogError("Match recieve tried to run on main thread");
                return;
            }

            byte[] data = new byte[1024];
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0); //Recieve from any 
            EndPoint tmpRemote = (EndPoint)(sender); //Convert IPEndPoint to EndPoint

            LANMatchSocket.ReceiveFrom(data, ref tmpRemote);

            int targetLength = data[1020] + data[1021] + data[1022] + data[1023];

            if (targetLength == 0)
            {
                Debug.LogWarning("Recieved an incomplete match message from: " + tmpRemote);
                MatchRecieve();
                return;
            }

            byte type = data[1019];
            Array.Resize(ref data, targetLength);

            if (type == (byte)MessageType.MatchData)
            {
                try
                {
                    MatchData match = Serializer.DeSerialize<MatchData>(data);
                    pendingMatchData.Enqueue(match);
                }
                catch (Exception)
                {
                    Debug.LogError("Serializer fail");
                }

            }

            MatchRecieve();
        }
    }
}
