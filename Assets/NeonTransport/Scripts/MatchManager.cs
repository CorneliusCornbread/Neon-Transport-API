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

        public bool IsSearchingForMatches { get; private set; } = false;
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

            ServerMessage request = new ServerMessage { msgType = ServerMsgType.MatchRequestEvent };

            byte[] packet = man.prepSend(request);

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
                return;

            IsBroadcastingMatch = false;
            LANMatchSendThread.Abort();
            LANMatchSocket.Close();
        }

        /// <summary>
        /// Used to broadcast current match if there is one
        /// </summary>
        public void StartMatchBroadcast()
        {
            if (man.highDebug)
            {
                if (man.IsQuitting)
                {
                    Debug.LogWarning("Cannot broadcast a match when there is no match");
                    return;
                }

                else if (!man.IsServer)
                {
                    Debug.LogError("Cannot broadcast a match when you're a client");
                    return;
                }

                else if (IsBroadcastingMatch)
                {
                    Debug.LogError("We've already started broadcasting");
                    return;
                }

                else
                    Debug.LogWarning("Match broadcast");
            }

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
            if (man.IsServer)
            {
                Debug.LogError("Cannot recieve matches if we are a server");
                return;
            }

            else if (IsSearchingForMatches)
            {
                Debug.LogError("Cannot call StartLANMAtchRecieve when it's already running");
                return;
            }

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

            IsSearchingForMatches = true;

            LANMatchRecieveThread = new Thread(MatchRecieve);
            LANMatchRecieveThread.Start();
        }

        /// <summary>
        /// Function used to start recieving LAN matches
        /// </summary>
        public void StopLANMatchRecieve()
        {
            if (!IsSearchingForMatches)
            {
                return;
            }

            Log("Stop LAN listen");

            IsSearchingForMatches = false;
            LANMatchRecieveThread.Abort();
            LANMatchSocket.Close(1);
        }

        /// <summary>
        /// Internal method used to serialize and send match messages on another thread
        /// </summary>
        private void MatchSend()
        {
            MatchData match = new MatchData
            {
                matchName = man.ServerName,
                playerCount = man.ConnectedClients.Count
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
                return;
            }

            LANMatchSocket.SendTo(packet, target);

            Thread.Sleep(matchBroadcastInterval);
            MatchSend();
        }

        private void MatchRecieve()
        {
            if (Thread.CurrentThread.ManagedThreadId == man.MainThread.ManagedThreadId)
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
                catch (Exception e)
                {
                    Debug.LogError("Match making serialize error: " + e);
                }

            }

            MatchRecieve();
        }

        /// <summary>
        /// Internal log function
        /// </summary>
        /// <param name="msg"></param>
        private void Log(object msg)
        {
            if (man.highDebug)
                Debug.Log(msg);
        }
    }
}
