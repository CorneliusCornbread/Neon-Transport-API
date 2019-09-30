using System.Collections.Generic;
using System;
using System.Threading;
using System.Net;
//using System.Collections;
using System.Net.Sockets;
using UnityEngine;
using OPS.Serialization.IO;
using System.Globalization;
using System.Collections.Concurrent;
using NeonNetworking.DataTypes;
using NeonNetworking.Enums;
using System.Linq;
//using System.IO;
//using System.Runtime.Serialization.Formatters.Binary;

namespace NeonNetworking
{
    public class NetworkManager : MonoBehaviour
    {
        #region TEST
        public GameObject playerPrefab; //Used for sending instantiate command in update()

        #endregion

        public static NetworkManager Instance { get; private set; }
        public static MatchManager MatchManager { get; private set; }
        public NetworkPrefabs Prefabs;
        public Socket Socket { get; private set; }
        public Thread SocketRecieveThread { get; private set; }
        public Thread SocketSendThread { get; private set; }
        public Thread SocketBroadcastThread { get; private set; }
        public Thread MainThread { get; private set; }
        /// <summary>
        /// Connected clients list, only available for server
        /// </summary>
        public List<Client> ConnectedClients
        {
            get
            {
                lock (conClientsLock)
                {
                    return prvConClients;
                }
            }

            private set
            {
                lock (conClientsLock)
                {
                    prvConClients = value;
                }
            }
        }
        private List<Client> prvConClients;
        private readonly object conClientsLock = new object();
        public List<NetworkObject> NetObjects { get; private set; }
        public bool IsServer { get; private set; } = false;
        public bool IsQuitting { get; private set; } = true;
        private volatile bool _IsListeningVar = false;
        private bool pendingData = false;
        private EndPoint targetEnd;

        private const string connectionDenied = "Conn Denied";

        //private bool recievingMatch = false;
        public float MatchRequestTimeout = 10;

        public bool IsMatchBroadcasting
        {
            get
            {
                return MatchManager.IsBroadcastingMatch;
            }
        }

        public string localClientID { get; private set; }

        #if UNITY_EDITOR
        [Tooltip("The amount of simulated delay in miliseconds")]
        [Range(0, 2000)]
        public int simulatedLag = 0;

        [Tooltip("Percent of simulated packet loss")]
        [Range(0, 100)]
        public int packetLoss = 0;
        #endif

        private byte lastPacketID = 0;
        private float syncDelay = .15f;

        private volatile ConcurrentQueue<Client> disClientEvents;
        private volatile ConcurrentQueue<MsgEvent> currentMsgs;
        private volatile ConcurrentQueue<NetInstantiate> pendingObjs;
        private volatile ConcurrentQueue<string> pendingDestroy;
        private volatile ConcurrentQueue<ThreadedInstantiate> threadedInstantiate;
        private volatile ConcurrentQueue<Client> pendingClientDisconnects;
        private volatile ConcurrentQueue<EndPoint> pendingEndpointsDisconnects;
        private volatile bool pendingLocalDisconnect = false;


        public string IP = "localhost";
        #if UNITY_EDITOR
        [Tooltip("Port used in host / connection, CANNOT BE 24546 AS THIS IS THE LAN PORT")] 
        #endif
        public int port = 24545;
        public string ServerName = "Default Transport Server";
        public const int LANBroadcastPort = 24546;

        #region Debug bools
        /// <summary>
        /// Debug bool used for in depth debugging
        /// </summary>
        [Tooltip("Debug bool used for in depth debugging.")]
        public bool highDebug = false;

        /// <summary>
        /// Used to time our message handling in milliseconds
        /// </summary>
        [Tooltip("Used to time our message handling in milliseconds")]
        public bool timeRecieve = false;

        /// <summary>
        /// Used to time our serialization in milliseconds
        /// </summary>
        [Tooltip("Used to time our serialization in milliseconds")]
        public bool timePrepSend = false;
        #endregion

        #if UNITY_EDITOR
        [Tooltip(
            "Client representation of the server and it's information. " +
            "On server this is used to store it's own ID. " +
            "On Client it's used to store ping, endpoint, message data, etc."
            )]
        #endif
        /// <summary>
        /// Client representation of the server and it's information.
        /// On server this is used to store it's own ID.
        /// On Client it's used to store ping, endpoint, message data, etc.
        /// </summary>
        public volatile Client serverData;

        //Debug stuff, will be removed later
        public Renderer platform;
        public Material noConn;
        public Material server;
        public Material client;

        #if UNITY_EDITOR
        public Client[] clArray;
        #endif

        public volatile System.Diagnostics.Stopwatch clientPingWatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Time in miliseconds it takes to round trip ping
        /// </summary>
        public float Ping
        {
            get
            {
                return serverData.ping;
            }

            private set
            {
                serverData.ping = value;
            }
        }

        public GameObject IDToPrefab(int id)
        {
            Log("ID TO PREFAB");

            try
            {
                GameObject obj = Prefabs.prefabDict[id];
                return obj;
            }

            catch
            {
                Debug.LogError("That id has no matching prefab: " + id);
                return null;
            }
        }

        public int PrefabToID(GameObject prefab)
        {
            Log("PREFAB TO ID");

            try
            {
                int p = Prefabs.prefabDict.FirstOrDefault(x => x.Value == prefab).Key;
                return p;
            }

            catch
            {
                Debug.LogError("That prefab has no matching id: " + prefab.name);
                return -1;
            }
        }

        /// <summary>
        /// Takes serializable object and turns it into a network message
        /// </summary>
        /// <param name="msg">Message object we want to serialize</param>
        /// <returns>Network message to send</returns>
        public byte[] prepSend(object msg)
        {
            if (msg == null)
                throw new ArgumentNullException("Cannot input null prepsend message");

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

            if (timePrepSend)
            {
                stopwatch.Start();
            }

            byte[] packet = new byte[1024];
            int size = 0;

            Type msgType = msg.GetType();

            Log("SERIALIZING TYPE: " + msgType + " MSG: " + msg);

            //Reverse lookup message type enum
            MessageType type = TypeDict.dict.FirstOrDefault(x => x.Value == msgType).Key;

            packet = Serializer.Serialize(msg);
            size = packet.Length;
            Array.Resize(ref packet, 1024);
            packet[1019] = (byte)type;

            switch (size)
            {
                case var expression when size <= 255: //255 - 0
                    packet[1023] = (byte)size;
                    break;

                case var expression when size <= 510 && size > 255: //256 - 510
                    packet[1023] = 255;
                    packet[1022] = (byte)(size - 255);
                    break;

                case var expression when size <= 765 && size > 510: //511 - 765
                    packet[1023] = 255;
                    packet[1022] = 255;
                    packet[1021] = (byte)(size - 510);
                    break;

                case var expression when size <= 1019 && size > 765: //766 - 1019
                    packet[1023] = 255;
                    packet[1022] = 255;
                    packet[1021] = 255;
                    packet[1020] = (byte)(size - 765);
                    break;

                default:
                    if (timePrepSend)
                    {
                        stopwatch.Stop();
                        Debug.Log("<color=#4295f5>prepSend: " + ((float)stopwatch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency) + "</color>");
                    }

                    string exception = "Packet size max is 1019, length is: " + size + ". Consider using compression if possible";
                    throw new Exception(exception);
            }

            if (timePrepSend)
            {
                stopwatch.Stop();
                Debug.Log("<color=#4295f5>prepSend: " + ((float)stopwatch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency) + "</color>");
            }

            return packet;
        }

        /// <summary>
        /// Convert string to a IPV4 or IPV6 endpoint.
        /// </summary>
        /// <param name="endPoint">Input string we want to convert</param>
        /// <returns>Returns IPEndPoint based on string</returns>
        public static IPEndPoint CreateIPEndPoint(string endPoint) //Stolen from stack overflow: https://stackoverflow.com/a/2727880
        {
            string[] ep = endPoint.Split(':');
            if (ep.Length < 2) throw new FormatException("Invalid endpoint format");
            IPAddress ip;
            if (ep.Length > 2)
            {
                if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            else
            {
                if (!IPAddress.TryParse(ep[0], out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            int port;
            if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
            {
                throw new FormatException("Invalid port");
            }
            return new IPEndPoint(ip, port);
        }

        #region Cleanup
        /// <summary>
        /// Function to clean up all of our threads and socket, is automatically run OnAplicationQuit(), OnDisable(), OnDestroy() and Disconnect(), but can be ran manually
        /// </summary>
        public void NetExit()
        {
            Debug.Log("<color=red>Net Exit</color>");

            IsQuitting = true;

            IsServer = false;

            pendingLocalDisconnect = false;

            if (SocketRecieveThread != null)
                SocketRecieveThread.Abort();

            if (SocketSendThread != null)
                SocketSendThread.Abort();

            if (SocketBroadcastThread != null)
                SocketBroadcastThread.Abort();

            if (Socket != null)
            {
                if (Socket.Connected)
                {
                    try
                    {
                        Socket.Disconnect(false);
                    }

                    catch
                    {
                        Socket.Close();
                    }
                }

                else
                {
                    Socket.Close();
                }
            }

            MatchManager.StopMatchBroadcast();

            CancelInvoke();
        }

        private void OnApplicationQuit()
        {
            NetExit();
        }

        private void OnDisable()
        {
            NetExit();
        }
        #endregion

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                Debug.LogError("Network Manager instance already exists, there's no reason to create another one");
                Destroy(this);
                return;
            }

            else
            {
                Instance = this;
                MatchManager = new MatchManager();
            }

            DontDestroyOnLoad(gameObject);

            VectorMessage v = new VectorMessage
            {
                reliable = true,
                messageNum = 12,
                x = 10,
                y = 12,
                z = 9
            };

            byte[] p = Serializer.Serialize(v);

            VectorMessage v2 = Serializer.DeSerialize<VectorMessage>(p);

            print(v.x + ", " + v.y + ", " + v.z);

            print("num " + v2.messageNum);
            print("reliable " + v2.reliable);

            /* comparing memory size between arrays and lists, fixed arrays save a lot of memory
            object[] msgsA = new object[1024];

            msgsA[0] = 9;
            msgsA[1] = new VectorMessage(10, 24, 10);
            msgsA[2] = 10;

            List<object> msgs = new List<object>(1024);

            msgs.Add(9);
            msgs.Add(new VectorMessage(10, 24, 10));
            msgs.Add(10);

            VectorMessage v = (VectorMessage)msgs[1];

            print("msgs 1 " + v.x);

            v = (VectorMessage)msgsA[1];

            print("msgs 2 " + v.y);

            long size1 = 0;
            using (Stream s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, msgs);
                size1 = s.Length;
            }

            long size2 = 0;
            using (Stream s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, msgsA);
                size2 = s.Length;
            }

            print("Size " + size1);
            print("Size 2 " + size2);
            */

            MainThread = Thread.CurrentThread;
            SocketRecieveThread = new Thread(Recieve);

            //We have to let the program run in the background as we can run into problems with our threads.
            //That and we're on multiplayer, you never want to pause a client whilst in multiplayer.
            Application.runInBackground = true;

            Prefabs.Initialize();


            #if UNITY_SERVER
            Host();
            #endif
        }


        private void Update()
        {
            if (!IsQuitting)
            {
                #if UNITY_EDITOR
                clArray = ConnectedClients.ToArray();
                #endif

                for (int i = 0; i < pendingObjs.Count; i++)
                {
                    NetInstantiate obj = new NetInstantiate();

                    if (pendingObjs.TryDequeue(out obj))
                    {
                        OnNetworkInstantiate(obj);
                    }
                }

                for (int i = 0; i < pendingDestroy.Count; i++)
                {
                    string obj;

                    if (pendingDestroy.TryDequeue(out obj))
                    {
                        OnNetDestroy(obj);
                    }
                }

                for (int i = 0; i < currentMsgs.Count; i++)
                {
                    MsgEvent msg = new MsgEvent();

                    if (currentMsgs.TryDequeue(out msg))
                    {
                        OnManagerRecieve(msg);

                        foreach (NetworkObject obj in NetObjects)
                        {
                            obj.rec(msg, msg.end);
                        }
                    }

                    else
                    {
                        Debug.LogWarning("Was unable to deque message");
                    }
                }

                for (int i = 0; i < disClientEvents.Count; i++)
                {
                    Client c = new Client();

                    if (disClientEvents.TryDequeue(out c))
                    {
                        foreach (NetworkObject obj in NetObjects)
                        {
                            obj.OnPlayerDisconnect(c);
                        }
                    }

                    else
                    {
                        Debug.LogWarning("Was unable to deque disconnect event");
                    }
                }

                for (int i = 0; i < pendingClientDisconnects.Count; i++)
                {
                    Client c;

                    if (pendingClientDisconnects.TryDequeue(out c))
                    {
                        DisconnectClient(c);
                    }

                    else
                    {
                        Debug.LogWarning("Was unable to deque disconnect client event");
                    }
                }

                for (int i = 0; i < pendingEndpointsDisconnects.Count; i++)
                {
                    EndPoint e; 

                    if (pendingEndpointsDisconnects.TryDequeue(out e))
                    {
                        DisconnectClient(e);
                    }

                    else
                    {
                        Debug.LogWarning("Was unable to deque disconnect client event");
                    }
                }

                for (int i = 0; i < threadedInstantiate.Count; i++)
                {
                    ThreadedInstantiate instan = new ThreadedInstantiate();

                    if (threadedInstantiate.TryDequeue(out instan))
                    {
                        NetworkInstantiate(instan.targetObj, instan.targetPos);
                    }

                    else
                    {
                        Debug.LogWarning("Was unable to deque threaded instantiate event");
                    }
                }
            }

            for (int i = 0; i < MatchManager.pendingMatchData.Count; i++)
            {
                MatchData match;

                if (MatchManager.pendingMatchData.TryDequeue(out match))
                {
                    OnMatchRecieve(match);
                }
            }

            #if !UNITY_SERVER

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                float randX = UnityEngine.Random.Range(-10f, 10f);
                float randY = UnityEngine.Random.Range(-4f, 5f);
                float randZ = UnityEngine.Random.Range(-1f, 10f);

                Vector3 v3 = new Vector3(randX, randY, randZ);

                Log("INSTANTIATE RAN");

                NetworkInstantiate(playerPrefab, v3);
            }

            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                IsQuitting = false;
                Connect(IP);
                platform.material = client;
            }

            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                IsQuitting = false;
                Host();
                platform.material = server;
            }

            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Disconnect();
                platform.material = noConn;
            }

            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Debug.Log("Disconnecting all clients");

                /*
                List<Client> temp = new List<Client>(connectedClients);

                foreach (Client c in temp)
                {
                    DisconnectClient(c);
                }

                temp = null;
                */

                DisconnectClients(ConnectedClients.ToArray());
            }

            else if (Input.GetKeyDown(KeyCode.Alpha5) && IsServer)
            {
                if (MatchManager.IsBroadcastingMatch)
                {
                    MatchManager.StopMatchBroadcast();
                }

                else
                {
                    MatchManager.StartMatchBroadcast();
                }
            }

            else if (Input.GetKeyDown(KeyCode.Alpha6) && !IsServer)
            {
                if (MatchManager.IsSearchingForMatches)
                {
                    MatchManager.StopLANMatchRecieve();
                }

                else
                {
                    MatchManager.StartLANMatchRecieve();
                }
            }
            #endif
        }

        #region Host and Connect functions
        /// <summary>
        /// Open a socket for our server to start sending and recieving information
        /// </summary>
        public void Host()
        {
            Log("HOST");

            NetExit();

            if (port == LANBroadcastPort)
            {
                Debug.LogError("CANNOT HAVE PORT BE: " + port + ", CHOOSE ANOTHER PORT");
                return;
            }

            IsQuitting = false;
            pendingLocalDisconnect = false;

            NetObjects = new List<NetworkObject>();
            //pendingDisconnects = new List<Client>();
            ConnectedClients = new List<Client>();
            currentMsgs = new ConcurrentQueue<MsgEvent>();
            pendingClientDisconnects = new ConcurrentQueue<Client>();
            disClientEvents = new ConcurrentQueue<Client>();
            pendingEndpointsDisconnects = new ConcurrentQueue<EndPoint>();
            pendingObjs = new ConcurrentQueue<NetInstantiate>();
            pendingDestroy = new ConcurrentQueue<string>();
            threadedInstantiate = new ConcurrentQueue<ThreadedInstantiate>();

            Invoke("SyncStep", 0);

            //Setup socket
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);

            //Bind socket
            Socket.Bind(ip);

            serverData = new Client
            {
                lastReplyRec = 0,
                endPoint = ip,
                ID = Guid.NewGuid().ToString()
            };

            localClientID = serverData.ID;

            IsServer = true;

            StartRecieve();

            Invoke("PingFunc", 2);
        }

        /// <summary>
        /// Open a socket for our client to start sending and recieving information from a set IP
        /// </summary>
        /// <param name="ip">IP to connect to</param>
        public void Connect(string ip)
        {
            Log("CONNECT");

            NetExit();

            if (port == LANBroadcastPort)
            {
                Debug.LogError("CANNOT HAVE PORT BE: " + port + ", CHOOSE ANOTHER PORT");
                return;
            }

            IsQuitting = false;
            pendingLocalDisconnect = false;

            NetObjects = new List<NetworkObject>();
            //pendingDisconnects = new List<Client>();
            ConnectedClients = new List<Client>();
            currentMsgs = new ConcurrentQueue<MsgEvent>();
            pendingClientDisconnects = new ConcurrentQueue<Client>();
            disClientEvents = new ConcurrentQueue<Client>();
            pendingEndpointsDisconnects = new ConcurrentQueue<EndPoint>();
            pendingObjs = new ConcurrentQueue<NetInstantiate>();
            pendingDestroy = new ConcurrentQueue<string>();
            threadedInstantiate = new ConcurrentQueue<ThreadedInstantiate>();

            string targetip = ip;

            if (ip == "localhost")
                targetip = "127.0.0.1";

            //Setup socket
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            serverData = new Client
            {
                lastReplyRec = 0,
            };

            IPAddress s;
            EndPoint tmpRemote = null;

            if (IPAddress.TryParse(targetip, out s))
            {
                IPEndPoint sender = new IPEndPoint(s, port);
                tmpRemote = (EndPoint)(sender); //Convert to IPEndPoint to EndPoint
            }

            else
            {
                Debug.LogError("Input IP address was invalid");
                return;
            }

            targetEnd = tmpRemote;

            StartRecieve();

            //socketRecieveThread = new Thread(Recieve);
            //socketRecieveThread.Start();

            //Send("HELLO SERVER", tmpRemote);

            Invoke("PingFunc", 2);
            Invoke("ClientStep", 1);
        }
        #endregion


        #region Send and Recieve data functions
        /// <summary>
        /// Send variable to a given target, used for custom messages
        /// </summary>
        /// <param name="msg">Message you want to send</param>
        /// <param name="target">Target endpoint</param>
        /// <param name="method">Send method (asynchronous, synchronous, threaded), threaded by default</param>
        public void Send<T>(T msg, EndPoint target) where T : MessageBase
        {
            if (IsQuitting)
            {
                Debug.LogWarning("We're quitting so we're skipping this send");
                return;
            }

            Log("SENDING: " + msg);

            if (target == null)
            {
                Debug.LogError("Endpoint cannot be null");
                return;
            }

            //Adds number to message
            //NOTE: If we send a generic message AT ALL, we track it but don't account for it leading to packet loss stat
            //no longer being accurate
            if (IsServer)
            {
                Client cl = ConnectedClients.Find(c => c.endPoint.Equals(target));
                msg.messageNum = cl.messagesFromTargetCount;
            }

            SocketSendThread = new Thread(() => ThreadedSerializeSend(msg, target));
            SocketSendThread.Start();
        }

        /// <summary>
        /// Send variable to a given target, used for custom messages
        /// </summary>
        /// <param name="msg">Message you want to send</param>
        /// <param name="target">Target endpoint</param>
        /// <param name="method">Send method (asynchronous, synchronous, threaded), threaded by default</param>
        public void Send(object msg, EndPoint target)
        {
            if (IsQuitting)
            {
                Debug.LogWarning("We're quitting so we're skipping this send");
                return;
            }

            Log("SENDING: " + msg);

            if (target == null)
            {
                Debug.LogError("Endpoint cannot be null");
                return;
            }

            SocketSendThread = new Thread(() => ThreadedSerializeSend(msg, target));
            SocketSendThread.Start();
        }

        /// <summary>
        /// Internal method used to serialize and send messages on another thread
        /// </summary>
        /// <param name="msg">Message to send</param>
        /// <param name="target">Target to send message to</param>
        private void ThreadedSerializeSend(object msg, EndPoint target)
        {
            Log("Threaded send start");
        
            byte[] packet;

            //Reliable stuff
            if (IsServer)
            {
                Client c = ConnectedClients.Find(i => i.endPoint.Equals(target));
                HandleMessageSent(c, msg);
            }

            else
            {
                HandleMessageSent(serverData, msg);
            }

            #if UNITY_EDITOR
            Thread.Sleep(simulatedLag);

            System.Random rng = new System.Random();

            if (packetLoss != 0 && rng.Next(0, 101) <= packetLoss)
            {
                Debug.LogWarning("Dropping message");
                return;
            }
#           endif

            try
            {
                packet = prepSend(msg);
            }

            catch (Exception ex)
            {
                string message = "Recieved exception from prepSend: " + ex.ToString();
                Debug.LogError(message + ", MSG: " + msg);
                throw new Exception(message);
            }

            Socket.SendTo(packet, target);

            pendingData = false;

            Log("Threaded send end");
        }

        #region Reliable message sending
        /*
        /// <summary>
        /// Function to handle a recieved message and track it
        /// </summary>
        private void HandleMessageRec(Client sender, object message)
        {
            sender.messagesFromTarget[sender.messagesFromTargetCount] = message;

            sender.messagesFromTargetCount++;
        }
        */

        private void HandleMessageSent(Client target, object message)
        {
            if (IsQuitting)
                return;

            try
            {
                if (target.messagesToTargetCount == byte.MaxValue - 1)
                {
                    target.messagesToTarget[target.messagesToTargetCount] = message;

                    target.messagesToTargetCount = 0;
                }

                else
                {
                    target.messagesToTarget[target.messagesToTargetCount] = message;

                    target.messagesToTargetCount++;
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("Reliable message error, did the client disconnect?");
            }
        }

        private void HandleMessageRec(Client target, object message)
        {
            if (IsQuitting)
                return;

            try
            {
                if (target.messagesFromTargetCount == byte.MaxValue - 1)
                {
                    target.messagesFromTarget[target.messagesFromTargetCount] = message;

                    target.messagesToTargetCount = 0;
                }

                else
                {
                    target.messagesFromTarget[target.messagesFromTargetCount] = message;

                    target.messagesFromTargetCount++;
                }

                try
                {
                    MessageBase m = (MessageBase)message;

                    Debug.Log("num " + m.messageNum);
                    Debug.Log("Percent " + (float)lastPacketID / m.messageNum * 100 + "%");
                }

                catch
                {
                    Debug.LogError("lakjdsflkasjglksadh");
                }

                lastPacketID = target.messagesFromTargetCount;
            }
            catch (Exception)
            {
                Debug.LogWarning("Reliable message error, did the client disconnect?");
            }
        }
        #endregion

        /// <summary>
        /// Broadcast function for server
        /// </summary>
        /// <param name="msg">Message to broadcast</param>
        public void Broadcast(object msg)
        {
            if (!IsServer)
            {
                Debug.LogError("Cannot broadcast on client");
                return;
            }

            pendingData = true;

            if (msg == null)
                throw new ArgumentNullException("Cannot input null object");

            Log("Broadcast: " + msg);

            SocketBroadcastThread = new Thread(() => ThreadedBroadcast(msg, ConnectedClients.ToArray()));
            SocketBroadcastThread.Start();

            Log("Broadcast completed");

            pendingData = false;
        }
        
        /// <summary>
        /// Internal threaded broadcast message
        /// </summary>
        /// <param name="msg">Object we want to serialize and send over the network</param>
        private void ThreadedBroadcast(object msg, Client[] clients)
        {
            if (msg == null)
                throw new ArgumentNullException("Cannot broadcast a null object");

            else if (!IsServer)
                throw new InvalidOperationException("Cannot call broadcast when we are not a server");

            byte[] packet;

            try
            {
                packet = prepSend(msg);
            }
            catch (Exception e)
            {
                Debug.LogError("THREADED BROADCAST CAUGHT PREPSEND ERROR: " + e);
                return;
            }

            //connectedClients.ForEach(item => socket.SendTo(packet, item.endPoint));

            foreach (Client c in clients)
            {
                HandleMessageSent(c, msg);

                #if UNITY_EDITOR
                Thread.Sleep(simulatedLag);

                System.Random rng = new System.Random();

                if (packetLoss != 0 && rng.Next(0, 101) <= packetLoss)
                {
                    Debug.LogWarning("Dropping message");
                    return;
                }
                #endif

                Socket.SendTo(packet, c.endPoint);
            }
        }

        /// <summary>
        /// OnSend callback
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Args used</param>
        private void OnSend(object sender, SocketAsyncEventArgs e)
        {
            Log("Async send completed");
            pendingData = false;
        }

        private void StartRecieve()
        {
            if (MainThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                LogWarning("START RECIEVE IS ON MAIN THREAD");
                SocketRecieveThread = new Thread(StartRecieve);
                SocketRecieveThread.Start();
                return;
            }

            while (!IsQuitting)
            {
                Recieve();
            }

            Debug.LogWarning("Recieve loop has ended");
        }

        /// <summary>
        /// Recieve function
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event Args</param>
        private void Recieve()
        {
            if (MainThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                Debug.LogWarning("RECIEVE IS ON MAIN THREAD");
                return;
            }

            Log("ON REC START");

            _IsListeningVar = true;

            byte[] data = new byte[1024];
            object message = "none";
            bool eventRec = false;

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0); //Recieve from any 
            EndPoint tmpRemote = (EndPoint)sender; //Convert IPEndPoint to EndPoint

            if (IsServer || Socket.Connected)
            {
                try
                {
                    Socket.ReceiveFrom(data, ref tmpRemote);
                }
                catch (Exception)
                {
                    Debug.LogWarning("A client was forcefully disconnected");
                    return;
                }
            }

            /* removed and replaced with or in first if statement
            else if (socket.Connected)
            {
                socket.ReceiveFrom(data, ref tmpRemote);
            }
            */

            else
            {
                return;
            }

            _IsListeningVar = false;

            System.Diagnostics.Stopwatch recWatch = new System.Diagnostics.Stopwatch();

            if (timeRecieve)
                recWatch.Start();

            int targetLength = data[1020] + data[1021] + data[1022] + data[1023];

            if (targetLength == 0)
            {
                Debug.LogWarning("<color=yellow>Recieved an incomplete message from: " + tmpRemote.ToString() + "</color>");

                if (timeRecieve)
                {
                    recWatch.Stop();
                    Debug.Log("<color=#4295f5>RECIEVE FINISH, TIME: " + 1000 * ((float)recWatch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency) + "</color>");
                    Debug.Log("<color=#4295f5>MESSAGE RECIEVED: " + message.GetType() + "</color>");
                }

                return;
            }

            byte type = data[1019];
            Array.Resize(ref data, targetLength);
            Type recType = TypeDict.dict[(MessageType)type];

            try
            {
                message = Serializer.DeSerialize(recType, data);

                Log("MESSAGE RECIEVED: (" + message + ")");
            }
            catch (Exception ex)
            {
                Debug.LogError("<color=red>Caught exception with deserialize: " + ex + "</color>");
                message = "CORRUPT";
            }

            switch((MessageType)type)
            {
                #region ServerMessage
                case MessageType.ServerMessage:
                    ServerMessage sMsg = (ServerMessage)message;
                    eventRec = true;

                    if (IsServer)
                    {
                        switch (sMsg.msgType)
                        {
                            case ServerMsgType.ConnectRequestEvent:
                                AddConnection(tmpRemote);
                                break;

                            case ServerMsgType.ConnectAcceptEvent:
                                Debug.LogWarning("<color=yellow>Recieved connection accept event on server</color>");
                                break;

                            case ServerMsgType.ClientDisconnectEvent:
                                Log("CLIENT DISCONNECTED: DISCONNECT " + tmpRemote.ToString());
                                DisconnectClient(tmpRemote);
                                break;

                            case ServerMsgType.PingEvent:
                                ServerMessage m = new ServerMessage { msgType = ServerMsgType.PongEvent };
                                Send(m, tmpRemote);
                                break;

                            case ServerMsgType.PongEvent:
                                Client c = ConnectedClients.Find(i => i.endPoint.Equals(tmpRemote));

                                if (c != null)
                                {
                                    c.pingTimer.Stop();
                                    c.ping = c.pingTimer.ElapsedMilliseconds;
                                }

                                else
                                    Debug.LogWarning("<color=yellow>Recieved ping from non connected client</color>");

                                eventRec = true;
                                break;

                            case ServerMsgType.CCEvent:
                                ServerMessage sm3 = new ServerMessage { msgType = ServerMsgType.CCAliveEvent };
                                Send(sm3, tmpRemote);
                                break;

                            case ServerMsgType.CCAliveEvent:
                                Log("CONNECTION IS ALIVE");

                                Client targetClient = ConnectedClients.Find(i => i.endPoint.Equals(tmpRemote));

                                if (targetClient != null)
                                {
                                    targetClient.lastReplyRec = 0;
                                }

                                eventRec = true;
                                break;

                            case ServerMsgType.MatchRequestEvent:
                                MatchData match = OnMatchRequest();

                                if (match != null)
                                    Send(match, tmpRemote);

                                else
                                    Debug.LogWarning("<color=yellow>Match data returned is null, sending no data</color>");
                                break;

                            default:
                                string error = "Recieved not implemented enumeration on server message: " + sMsg.msgType;
                                throw new NotImplementedException(error);
                        }
                    }

                    else
                    {
                        switch (sMsg.msgType)
                        {
                            case ServerMsgType.ConnectRequestEvent:
                                Debug.LogWarning("<color=yellow>Recieved match request on client</color>");
                                break;

                            case ServerMsgType.ConnectAcceptEvent:
                                if (sMsg.ID == connectionDenied || string.IsNullOrEmpty(sMsg.ID))
                                {
                                    Debug.LogError("Connection has been denied or failed");
                                    break;
                                }

                                AddConnection(tmpRemote);
                                Log("Setting ID");
                                localClientID = sMsg.ID;
                                serverData.ID = sMsg.ServerID;
                                break;

                            case ServerMsgType.ClientDisconnectEvent:
                                Client client = new Client { ID = sMsg.ID};
                                OnDisconnectClient(client);
                                break;

                            case ServerMsgType.PingEvent:
                                ServerMessage sm2 = new ServerMessage { msgType = ServerMsgType.PongEvent };
                                Send(sm2, tmpRemote);
                                break;

                            case ServerMsgType.PongEvent:
                                clientPingWatch.Stop();
                                Ping = clientPingWatch.ElapsedMilliseconds;
                                break;

                            case ServerMsgType.CCEvent:
                                ServerMessage sm1 = new ServerMessage { msgType = ServerMsgType.CCAliveEvent };
                                Send(sm1, tmpRemote);
                                break;

                            case ServerMsgType.CCAliveEvent:
                                serverData.lastReplyRec = 0;
                                break;

                            case ServerMsgType.MatchRequestEvent:
                                Debug.LogWarning("<color=yellow>Recieved match request on client</color>");
                                break;

                            case ServerMsgType.ConnectionDisconnectEvent:
                                Log("You've been disconnected by the server");
                                Disconnect();
                                break;

                            default:
                                string error = "Recieved not implemented enumeration on server message (client): " + sMsg.msgType;
                                throw new NotImplementedException(error);
                        }
                    }
                    break;
                #endregion

                #region NetInstantiate
                case MessageType.NetInstantiate:
                    eventRec = true;
                    NetInstantiate netInstantiate = (NetInstantiate)message;

                    if (IsServer)
                    {
                        Client targetClient = ConnectedClients.Find(i => i.endPoint.Equals(tmpRemote));

                        if (targetClient != null)
                        {
                            netInstantiate.senderID = targetClient.ID;
                            netInstantiate.instanceID = Guid.NewGuid().ToString();
                            pendingObjs.Enqueue(netInstantiate);
                        }

                        else
                        {
                            Debug.LogError("The client that sent this netInstantiate is not connected to the server, ignoring the message");
                        }
                    }

                    else if (tmpRemote.Equals(serverData.endPoint))
                    {
                        pendingObjs.Enqueue(netInstantiate);
                    }

                    else
                    {
                        Debug.LogError("Recieved instantiate message from unestabilished connection");
                    }
                    break;
                #endregion

                #region NetDestroy
                case MessageType.NetDestroy:
                    eventRec = true;
                    NetDestroyMsg netDestroy = (NetDestroyMsg)message;
                    Log("NETDESTROY ID RECIEVED: " + netDestroy.IDToDestroy);

                    if (IsServer)
                    {
                        NetworkDestroy(netDestroy.IDToDestroy);
                    }

                    else
                    {
                        OnNetDestroy(netDestroy.IDToDestroy); //Error
                    }
                    break;
                #endregion

                #region MatchData
                case MessageType.MatchData:
                    eventRec = true;
                    MatchData matchData = (MatchData)message;
                    Log("MATCH DATA RECIEVED: " + matchData.matchName);

                    Debug.LogWarning("<color=yellow>Recieved match data when we didn't expect it</color>");

                    if (timeRecieve)
                    {
                        recWatch.Stop();
                        Debug.Log("<color=#4295f5>RECIEVE FINISH, TIME: " + recWatch.ElapsedMilliseconds + "</color>");
                        Debug.Log("<color=#4295f5>MESSAGE RECIEVED: " + message.GetType() + "</color>");
                    }

                    return;
                #endregion
            }

            if (IsServer)
            {
                Client targetClient = ConnectedClients.Find(i => i.endPoint.Equals(tmpRemote));

                if (targetClient != null)
                {
                    targetClient.lastReplyRec = 0;
                    HandleMessageRec(targetClient, message);
                }
            }

            else
            {
                HandleMessageRec(serverData, message);
                serverData.lastReplyRec = 0;
            }

            //Don't bother updating our network objects if we've recieved an event, we'll already have functions to handle these events
            if (!eventRec)
            {
                MsgEvent m = new MsgEvent { msg = message, end = tmpRemote };
                currentMsgs.Enqueue(m);
            }

            if (timeRecieve)
            {
                recWatch.Stop();
                Debug.Log("<color=#4295f5>RECIEVE FINISH, TIME: " + 1000 * ((float)recWatch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency) + "</color>");
                Debug.Log("<color=#4295f5>MESSAGE RECIEVED: " + message.GetType() + "</color>");

                if ((MessageType)type == MessageType.ServerMessage)
                {
                    ServerMessage m = (ServerMessage)message;
                    Debug.Log("<color=#4295f5>SVR MSG: " + m.msgType + "</color>");
                }
            }
        }
        #endregion

        #region Virtual Functionality
        /// <summary>
        /// Function that's called when we recieve a message, can be used to handle server specific data
        /// </summary>
        /// <param name="data">Data recieved</param>
        public virtual void OnManagerRecieve(MsgEvent data)
        {

        }

        /// <summary>
        /// Function that's called when we recieve match data
        /// </summary>
        /// <param name="data">Data recieved</param>
        public virtual void OnMatchRecieve(MatchData m)
        {
            Debug.LogError("MATCH: " + m.matchName);
        }

        public virtual MatchData OnMatchRequest()
        {
            MatchData m = new MatchData
            {
                matchName = ServerName,
                playerCount = ConnectedClients.Count
            };

            return m;
        }
        #endregion

        #region Client Management
        /// <summary>
        /// Add server/client connection
        /// </summary>
        /// <param name="connection">Connection we want to add</param>
        void AddConnection(EndPoint connection)
        {
            if (IsServer)
            {
                Client conn = new Client();
                conn.lastReplyRec = 0;
                conn.endPoint = connection;
                conn.ID = Guid.NewGuid().ToString();

                if (ConnectedClients.Find(c => c.endPoint.Equals(connection)) != null)
                    Debug.LogError("Client is already connected");
                
                else
                    ConnectedClients.Add(conn);

                //ClientIDMsg iDMsg = new ClientIDMsg { ID = conn.ID, msg = "HELLO CLIENT"};
                ServerMessage iDMsg = new ServerMessage
                {
                    msgType = ServerMsgType.ConnectAcceptEvent,
                    ID = conn.ID,
                    ServerID = serverData.ID
                };
                Send(iDMsg, connection);

                List<string> sentIDs = new List<string>();

                foreach (NetworkObject netObj in NetObjects)
                {
                    string id = sentIDs.Find(i => i == netObj.InstanceID);

                    if (!string.IsNullOrEmpty(id))
                        continue;

                    netObj.OnNewPlayer(conn);
                    sentIDs.Add(netObj.InstanceID);
                    Send(netObj.instanMsg, connection);
                }

                Log("CLIENT CONNECTION ADDED: " + connection);
            }

            else
            {
                serverData.endPoint = connection;
                Log("SERVER CONNECTION SET: " + connection);

                NetworkInstantiate(playerPrefab);
            }
        }

        /// <summary>
        /// Disconnects client based on endpoint
        /// </summary>
        /// <param name="connection">Endpoint to disconnect</param>
        public void DisconnectClient(EndPoint connection)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called disconnect client on non main thread, queing for main thread");
                pendingEndpointsDisconnects.Enqueue(connection);
                return;
            }

            Log("DISCONNECTING ENDPOINT");

            if (IsServer)
            {
                Client c = ConnectedClients.Find(i => i.endPoint.Equals(connection));

                if (c != null)
                {
                    ConnectedClients.Remove(c);
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ConnectionDisconnectEvent };
                    m.ID = c.ID;
                    Send(m, connection);
                    OnDisconnectClient(c);
                }

                else
                {
                    LogWarning("Input endpoint that is not connected");
                }
            }

            else
            {
                Debug.LogError("Cannot call DisconnectClient() on a client");
            }
        }

        /// <summary>
        /// Disconnects client based on client
        /// </summary>
        /// <param name="client">Client to disconnect</param>
        public void DisconnectClient(Client client)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called disconnect client on non main thread, queing for main thread");
                pendingClientDisconnects.Enqueue(client);
                return;
            }

            Log("DISCONNECTING CLIENT");

            if (IsServer)
            {
                ConnectedClients.Remove(client);
                ServerMessage m = new ServerMessage { msgType = ServerMsgType.ConnectionDisconnectEvent };
                m.ID = client.ID;
                Send(m, client.endPoint);
                OnDisconnectClient(client);
            }

            else
            {
                Debug.LogError("Cannot call DisconnectClient() on a client");
            }
        }

        /// <summary>
        /// Disconnects multiple clients
        /// </summary>
        /// <param name="clients"></param>
        public void DisconnectClients(Client[] clients)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called disconnect clients on non main thread, queing for main thread");

                foreach (Client c in clients)
                {
                    pendingClientDisconnects.Enqueue(c);
                }

                return;
            }

            foreach (Client c in clients)
            {
                DisconnectClient(c);
            }
        }


        /// <summary>
        /// Internal function to disconnect client
        /// </summary>
        /// <param name="client">Client to disconnect</param>
        private void OnDisconnectClient(Client client)
        {
            if (IsServer)
            {
                ServerMessage dc = new ServerMessage { msgType = ServerMsgType.ClientDisconnectEvent, ID = client.ID };
                Broadcast(dc); //Broadcast disconnect
            }

            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called disconnect client on non main thread, queing for main thread");
                disClientEvents.Enqueue(client);
                return;
            }

            foreach (NetworkObject obj in NetObjects)
            {
                obj.OnPlayerDisconnect(client);
            }
        }

        /// <summary>
        /// Function to disconnect our client or close our server, automatically sends shutdown message.
        /// Will typically take time to complete, TODO: MAKE DISCONNECT() ASYNC AND HAVE CALLBACK
        /// </summary>
        public void Disconnect()
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called disconnect() on non main thread, queing for main thread");
                pendingLocalDisconnect = true;
                return;
            }

            Log("DISCONNECT");

            if (IsServer)
            {
                Debug.LogWarning("SERVER SHUTDOWN");

                CancelInvoke("SyncStep");
                CancelInvoke("Ping");

                if (!IsQuitting)
                {
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ConnectionDisconnectEvent };
                    Broadcast(m);
                }

                if (pendingData)
                {
                    Log("DATA PENDING");
                    Invoke("Disconnect", 2);
                }

                else
                {
                    NetExit();

                    foreach (NetworkObject obj in NetObjects)
                    {
                        if (obj.gameObject != null)
                            Destroy(obj.gameObject);
                    }

                    NetObjects = null;
                    pendingObjs = null;
                    pendingDestroy = null;
                    ConnectedClients = null;
                    currentMsgs = null;
                    threadedInstantiate = null;
                }

                IsQuitting = true;
            }

            else if (serverData.endPoint != null)
            {
                Debug.LogWarning("CLIENT SHUTDOWN");

                CancelInvoke("ClientStep");
                CancelInvoke("Ping");

                if (!IsQuitting)
                {
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ClientDisconnectEvent };
                    Send(m, serverData.endPoint);
                }

                if (pendingData)
                {
                    Invoke("Disconnect", 2);
                    Log("DATA PENDING");
                }

                else
                {
                    List<NetworkObject> objsToDestroy = NetObjects;

                    foreach (NetworkObject obj in objsToDestroy)
                    {
                        Destroy(obj.gameObject);
                    }

                    objsToDestroy = null;

                    NetExit();

                    NetObjects = null;
                    pendingObjs = null;
                    pendingDestroy = null;
                    ConnectedClients = null;
                    serverData.endPoint = null;
                    currentMsgs = null;
                    threadedInstantiate = null;
                }

                IsQuitting = true;
            }

            else
            {
                IsQuitting = true;
                IsServer = false;
                CancelInvoke();
            }
        }
        #endregion

        /// <summary>
        /// Network Instantiate function, instantiates input game object on the network at input position
        /// </summary>
        /// <param name="obj">Network Prefab</param>
        /// <param name="pos">Vector3 Position</param>
        public void NetworkInstantiate(GameObject obj, Vector3 pos = new Vector3())
        {
            if (obj == null)
                throw new ArgumentNullException("Cannot input null gameobject");

            else if (Socket == null)
            {
                Debug.LogError("Cannot call NetworkInstantiate() without being connected");
                return;
            }

            else if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                Log("Called network instantiate on non main thread, queing for main thread");
                ThreadedInstantiate instan = new ThreadedInstantiate
                {
                    targetObj = obj,
                    targetPos = pos,
                };

                threadedInstantiate.Enqueue(instan);
                return;
            }

            NetInstantiate netObj = new NetInstantiate
            {
                prefabID = PrefabToID(obj),
                objName = obj.name,
                pos = pos
            };

            Log("NET INSTAN: " + pos);


            if (IsServer)
            {
                netObj.senderID = serverData.ID;
                netObj.instanceID = Guid.NewGuid().ToString();
                OnNetworkInstantiate(netObj);
            }

            else if (serverData.endPoint != null)
                Send(netObj, serverData.endPoint);
        }

        /// <summary>
        /// Destroy a network object and sync it over the network
        /// </summary>
        /// <param name="obj">Network object to destroy</param>
        public void NetworkDestroy(NetworkObject obj)
        {
            NetworkDestroy(obj.InstanceID);
        }

        /// <summary>
        /// Destroy a network object and sync it over the network
        /// </summary>
        /// <param name="instanceID">Instance ID of network object</param>
        public void NetworkDestroy(string instanceID)
        {
            NetDestroyMsg msg = new NetDestroyMsg { IDToDestroy = instanceID };

            if (IsServer)
            {
                OnNetDestroy(instanceID);
                Broadcast(msg);
            }

            else
            {
                Send(msg, serverData.endPoint);
            }
        }

        private void OnNetDestroy(string instanceID)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread.ManagedThreadId)
            {
                pendingDestroy.Enqueue(instanceID);
                Log("On net destroy called on non main thread, queing for main thread");
                return;
            }

            NetworkObject obj = NetObjects.Find(i => i.InstanceID == instanceID);

            if (obj == null)
                Debug.LogError("No network object found with ID: " + instanceID);

            else
                Destroy(obj.gameObject);
        }

        /// <summary>
        /// Called when we recieve a network instantiate message
        /// </summary>
        /// <param name="obj">Network Instantiate message</param>
        private void OnNetworkInstantiate(NetInstantiate obj)
        {
            if (string.IsNullOrEmpty(obj.senderID) || string.IsNullOrEmpty(obj.instanceID))
                throw new ArgumentNullException("NetInstantiate.sender cannot be null/empty, this is supposed to be set on the server");

            if (IsServer)
                Broadcast(obj);

            GameObject targetObject = IDToPrefab(obj.prefabID);
            GameObject spawnedObj = Instantiate(targetObject);

            spawnedObj.transform.position = obj.pos;

            NetworkObject[] scripts = spawnedObj.GetComponents<NetworkObject>();

            foreach (NetworkObject netObj in scripts)
            {
                Log("SETTING OWNER ID ON: " + netObj);
                //netObj.Owner = CreateIPEndPoint(obj.sender);
                netObj.InstanceID = obj.instanceID;
                netObj.OwnerID = obj.senderID;
                netObj.instanMsg = obj;
            }
        }

        /// <summary>
        /// Sync Step function used to sync automatically syncing data and check connections.
        /// Will automatically reinvoke itself if isQuitting == false
        /// </summary>
        void SyncStep()
        {
            if (IsQuitting)
            {
                Debug.LogWarning("Skipping Sync Step as we're quitting");
                return;
            }

            else if (pendingLocalDisconnect)
            {
                Disconnect();
                //return;
            }

            if (!_IsListeningVar)
            {
                Debug.LogWarning("We're not listening, are we spending too much time OnRecieve?");
            }

            Log("SYNC STEP");

            List<Client> clients = new List<Client>(ConnectedClients);

            foreach (Client c in clients)
            {
                c.lastReplyRec++;

                if (c.lastReplyRec > 200)
                {
                    DisconnectClient(c);
                }

                else if (c.lastReplyRec > 100)
                {
                    ServerMessage msg = new ServerMessage { msgType = ServerMsgType.CCEvent };
                    Send(msg, c.endPoint);
                }
            }

            foreach (NetworkObject obj in NetObjects)
            {
                object syncObj = obj.SyncObj();

                if (syncObj != null)
                {
                    Broadcast(syncObj);
                }
            }

            Invoke("SyncStep", syncDelay);
        }

        /// <summary>
        /// Client step, used to sync automatically syncing variables and check connections.
        /// Will automatically reinvoke itself if isQuitting == false
        /// </summary>
        void ClientStep()
        {
            if (IsQuitting)
            {
                Debug.LogWarning("Skipping Client Step as we're quitting");
                return;
            }

            else if (pendingLocalDisconnect)
            {
                Disconnect();
                //return;
            }

            if (!_IsListeningVar)
            {
                Debug.LogWarning("We're not listening, are we spending too much time OnRecieve?");
            }

            Log("CLIENT STEP");

            if (serverData.endPoint == null)
            {
                Log("NO CONNECTION, ATTEMPTING CONNECT");
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.ConnectRequestEvent };
                Socket.Connect(targetEnd);
                Send(msg, targetEnd);
                Invoke("ClientStep", .25f);
                return;
            }

            serverData.lastReplyRec++;

            if (serverData.lastReplyRec > 10 && serverData.lastReplyRec <= 20)
            {
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.CCEvent };
                Send(msg, serverData.endPoint);
            }

            else if (serverData.lastReplyRec > 20)
            {
                serverData.endPoint = null;

                Log("DISCONNECT: TIMEOUT");
            }

            foreach (NetworkObject obj in NetObjects)
            {
                object data = obj.SyncObj();

                if (data != null)
                {
                    Send(data, serverData.endPoint);
                }
            }

            Invoke("ClientStep", 1);
        }

        /// <summary>
        /// Function used to measure ping
        /// </summary>
        void PingFunc()
        {
            if (IsQuitting)
                return;

            if (IsServer)
            {
                for (int i = 0; i < ConnectedClients.Count; i++)
                {
                    ConnectedClients[i].pingTimer.Restart();
                    ServerMessage msg = new ServerMessage { msgType = ServerMsgType.PingEvent };
                    Send(msg, ConnectedClients[i].endPoint);
                }
            }

            else if (serverData.endPoint != null)
            {
                clientPingWatch.Restart();
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.PingEvent };
                Send(msg, serverData.endPoint);
            }

            Invoke("PingFunc", 2);
        }

        /// <summary>
        /// Internal log function
        /// </summary>
        /// <param name="msg">Message to debug</param>
        private void Log(object msg)
        {
            if (highDebug)
                Debug.Log(msg);
        }

        /// <summary>
        /// Internal log warning function
        /// </summary>
        /// <param name="msg">Message to debug</param>
        private void LogWarning(object msg)
        {
            if (highDebug)
                Debug.LogWarning(msg);
        }
    }
}