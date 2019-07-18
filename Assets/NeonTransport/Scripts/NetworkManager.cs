using System.Collections.Generic;
using System;
using System.Threading;
using System.Net;
using System.Collections;
using System.Net.Sockets;
using UnityEngine;
using OPS.Serialization.IO;
using System.Globalization;
using System.Collections.Concurrent;
using NeonNetworking.DataTypes;
using NeonNetworking.Enums;
using System.Linq;

namespace NeonNetworking
{
    public class NetworkManager : MonoBehaviour
    {
        #region Prefabs
        public GameObject playerPrefab;

        #endregion

        public static NetworkManager Instance { get; private set; }
        public static MatchManager MatchManager { get; private set; } = new MatchManager();
        public Socket socket { get; private set; }
        public Thread socketRecieveThread { get; private set; }
        public Thread socketSendThread { get; private set; }
        public Thread socketBroadcastThread { get; private set; }
        public Thread mainThread { get; private set; }
        public EndPoint clientConnection;
        public float serverLastMsgTime;
        /// <summary>
        /// Connected clients list, only available for server
        /// </summary>
        public List<Client> connectedClients { get; private set; } 
        public List<NetworkObject> netObjects { get; private set; }
        public bool isServer { get; private set; } = false;
        public bool isQuitting { get; private set; } = true;
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

        [Tooltip("The amount of simulated delay in miliseconds")]
        [Range(0, 2000)]
        public int simulatedLag = 0;

        private float syncDelay = .15f;

        private volatile ConcurrentQueue<Client> disClientEvents;
        private volatile ConcurrentQueue<MsgEvent> currentMsgs;
        private volatile ConcurrentQueue<NetInstantiate> pendingObjs;
        private volatile ConcurrentQueue<ThreadedInstantiate> threadedInstantiate;
        private volatile ConcurrentQueue<Client> pendingClientDisconnects;
        private volatile ConcurrentQueue<EndPoint> pendingEndpointsDisconnects;


        public string IP = "localhost";
        [Tooltip("Port used in host / connection, CANNOT BE 24546 AS THIS IS THE LAN PORT")]
        public int port = 24545;
        public string ServerName = "Default Transport Server";
        public const int LANBroadcastPort = 24546;

        /// <summary>
        /// Debug bool used for in depth debugging
        /// </summary>
        [Tooltip("Debug bool used for in depth debugging.")]
        public bool highDebug = true;
        
        public Client serverData;

        //Debug stuff, will be removed later
        public Renderer platform;
        public Material noConn;
        public Material server;
        public Material client;

        private float startPingTime;
        private volatile float unscaledTimeThreaded;

        /// <summary>
        /// Time in miliseconds it takes to round trip ping
        /// </summary>
        public float Ping { get; private set; }

        public GameObject stringToPrefab(string objName)
        {
            if (highDebug)
                Debug.Log("STRING TO PREFAB");

            switch (objName)
            {
                case "playerPrefab":
                    if (playerPrefab == null)
                        throw new Exception("Prefab string: " + objName + " is not set");

                    return playerPrefab;

                default:
                    Debug.LogError("Prefab string recieved is not a valid type. Did you set it in the editor? Prefab string name: " + objName);
                    return null;
            }
        }

        public string prefabToString(GameObject prefab)
        {
            if (highDebug)
                Debug.Log("PREFAB TO STRING");


            switch (prefab)
            {
                case var expression when prefab == playerPrefab:
                    if (playerPrefab == null)
                        throw new Exception("Prefab: " + prefab.name + " is not set");

                    return "playerPrefab";

                default:
                    Debug.LogError("Prefab recieved is not a valid type. Did you set it in the editor? Prefab name: " + prefab.name);
                    return null;
            }
        }

        public byte[] prepSend(object msg)
        {
            if (msg == null)
                throw new ArgumentNullException("Cannot input null prepsend message");

            byte[] packet = new byte[1024];
            int size = 0;

            Type msgType = msg.GetType();

            if (highDebug)
                Debug.Log("SERIALIZING TYPE: " + msgType + " MSG: " + msg);

            //Reverse lookup message type enum
            MessageType type = TypeDict.dict.FirstOrDefault(x => x.Value == msgType).Key;

            packet = Serializer.Serialize(msg);
            size = packet.Length;
            Array.Resize(ref packet, 1024);
            packet[1019] = (byte)type;

            #region old switch
            /*
            switch (msg)
            {
                case var expression when msgType == typeof(string): //String serialiation
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.String;
                    break;

                case var expression when msgType == typeof(int): //Int serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.Int;
                    break;

                case var expression when msgType == typeof(float): //Float serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.Float;
                    break;

                case var expression when msgType == typeof(bool): //Bool serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.Bool;
                    break;

                case var expression when msgType == typeof(Vector3): //Vector3 type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.Vector3;
                    break;

                case var expression when msgType == typeof(NetInstantiate): //Instantiate network message serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.NetInstantiate;
                    break;

                case var expression when msgType == typeof(DisconnectEvent): //Disconnect event type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.DisconnectEvent;
                    break;

                case var expression when msgType == typeof(PlayerData): //Playerdata type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.PlayerData;
                    break;

                case var expression when msgType == typeof(ClientIDMsg): //Client ID type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.ClientID;
                    break;

                case var expression when msgType == typeof(NetDestroyMsg): //Netdestroy type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.NetDestroy;
                    break;

                case var expression when msgType == typeof(MatchData): //Match data type serialization
                    packet = Serializer.Serialize(msg);
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    packet[1019] = (byte)MessageType.MatchData;
                    break;

                case var expression when msgType == typeof(byte[]): //If we recieved a byte array
                    packet = (byte[])msg;
                    size = packet.Length;
                    Array.Resize(ref packet, 1024);
                    break;

                default:
                    packet = new byte[1024];
                    throw new InvalidOperationException("Given type is not supported");

            }
            */
            #endregion

            switch (size)
            {
                case var expression when size <= 255: //255 - 0
                    //Debug.Log("SIZE IS <256");
                    packet[1023] = (byte)size;
                    break;

                case var expression when size <= 510 && size > 255: //256 - 510
                    //Debug.Log("SIZE IS <511");
                    packet[1023] = 255;
                    packet[1022] = (byte)(size - 255);
                    break;

                case var expression when size <= 765 && size > 510: //511 - 765
                    //Debug.Log("SIZE IS <766");
                    packet[1023] = 255;
                    packet[1022] = 255;
                    packet[1021] = (byte)(size - 510);
                    break;

                case var expression when size <= 1019 && size > 765: //766 - 1019
                    //Debug.Log("SIZE IS <766");
                    packet[1023] = 255;
                    packet[1022] = 255;
                    packet[1021] = 255;
                    packet[1020] = (byte)(size - 765);
                    break;

                default:
                    string exception = "Packet size max is 1019, length is: " + size + ". Consider using compression if possible";
                    throw new Exception(exception);
            }

            return packet;
        }

        /// <summary>
        /// Convert string to a IPV4 or IPV6 endpoint.
        /// </summary>
        /// <param name="endPoint">Input string we want to convert</param>
        /// <returns></returns>
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

        #region Construction and cleanup
        public NetworkManager()
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
            }
        }

        /// <summary>
        /// Function to clean up all of our threads and socket, is automatically run OnAplicationQuit(), OnDisable(), OnDestroy() and Disconnect(), but can be ran manually
        /// </summary>
        public void NetExit()
        {
            Debug.LogWarning("Net Exit");

            isQuitting = true;

            isServer = false;

            if (socketRecieveThread != null)
                socketRecieveThread.Abort();

            if (socketSendThread != null)
                socketSendThread.Abort();

            if (socketBroadcastThread != null)
                socketBroadcastThread.Abort();

            if (socket != null)
                socket.Close();

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
            DontDestroyOnLoad(gameObject);

            mainThread = Thread.CurrentThread;
            socketRecieveThread = new Thread(Recieve);

            //We have to let the program run in the background as we can run into problems with our threads.
            //That and we're on multiplayer, you never want to pause a client whilst in multiplayer.
            Application.runInBackground = true;

            #if UNITY_SERVER
            Host();
            #endif
        }


        private void Update()
        {
            if (!isQuitting)
            {
                for (int i = 0; i < pendingObjs.Count; i++)
                {
                    NetInstantiate obj = new NetInstantiate();

                    if (pendingObjs.TryDequeue(out obj))
                    {
                        OnNetworkInstantiate(obj);
                    }
                }

                for (int i = 0; i < currentMsgs.Count; i++)
                {
                    MsgEvent msg = new MsgEvent();

                    if (currentMsgs.TryDequeue(out msg))
                    {
                        Debug.LogWarning("CURRENT MSG");

                        OnManagerRecieve(msg);

                        foreach (NetworkObject obj in netObjects)
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
                        foreach (NetworkObject obj in netObjects)
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
                    Client c = new Client();

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

            unscaledTimeThreaded = Time.unscaledTime;

            #if !UNITY_SERVER

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                float randX = UnityEngine.Random.Range(-10f, 10f);
                float randY = UnityEngine.Random.Range(-4f, 5f);
                float randZ = UnityEngine.Random.Range(-1f, 10f);

                Vector3 v3 = new Vector3(randX, randY, randZ);

                Debug.Log("INSTANTIATE RAN");

                NetworkInstantiate(playerPrefab, v3);
            }

            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                isQuitting = false;
                Connect(IP);
                platform.material = client;
            }

            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                isQuitting = false;
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

                foreach (Client c in connectedClients)
                {
                    DisconnectClient(c);
                }
            }

            else if (Input.GetKeyDown(KeyCode.Alpha5) && isServer)
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

            else if (Input.GetKeyDown(KeyCode.Alpha6) && !isServer)
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
            if (highDebug)
                Debug.Log("HOST");

            NetExit();

            if (port == LANBroadcastPort)
            {
                Debug.LogError("CANNOT HAVE PORT BE: " + port + ", CHOOSE ANOTHER PORT");
                return;
            }

            isQuitting = false;

            netObjects = new List<NetworkObject>();
            //pendingDisconnects = new List<Client>();
            connectedClients = new List<Client>();
            currentMsgs = new ConcurrentQueue<MsgEvent>();
            pendingClientDisconnects = new ConcurrentQueue<Client>();
            disClientEvents = new ConcurrentQueue<Client>();
            pendingEndpointsDisconnects = new ConcurrentQueue<EndPoint>();
            pendingObjs = new ConcurrentQueue<NetInstantiate>();
            threadedInstantiate = new ConcurrentQueue<ThreadedInstantiate>();

            Invoke("SyncStep", 0);

            //Setup socket
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);

            //Bind socket
            socket.Bind(ip);

            serverData = new Client
            {
                lastReplyRec = 0,
                endPoint = ip,
                ID = Guid.NewGuid().ToString()
            };

            localClientID = serverData.ID;

            isServer = true;

            socketRecieveThread = new Thread(Recieve);
            socketRecieveThread.Start();

            Invoke("PingFunc", 2);
        }

        /// <summary>
        /// Open a socket for our client to start sending and recieving information from a set IP
        /// </summary>
        /// <param name="ip">IP to connect to</param>
        public void Connect(string ip)
        {
            if (highDebug)
                Debug.Log("CONNECT");

            NetExit();

            if (port == LANBroadcastPort)
            {
                Debug.LogError("CANNOT HAVE PORT BE: " + port + ", CHOOSE ANOTHER PORT");
                return;
            }

            isQuitting = false;

            netObjects = new List<NetworkObject>();
            //pendingDisconnects = new List<Client>();
            connectedClients = new List<Client>();
            currentMsgs = new ConcurrentQueue<MsgEvent>();
            pendingClientDisconnects = new ConcurrentQueue<Client>();
            disClientEvents = new ConcurrentQueue<Client>();
            pendingEndpointsDisconnects = new ConcurrentQueue<EndPoint>();
            pendingObjs = new ConcurrentQueue<NetInstantiate>();
            threadedInstantiate = new ConcurrentQueue<ThreadedInstantiate>();

            string targetip = ip;

            if (ip == "localhost")
                targetip = "127.0.0.1";

            //Setup socket
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

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

            socketRecieveThread = new Thread(Recieve);
            socketRecieveThread.Start();

            //Send("HELLO SERVER", tmpRemote);

            Invoke("PingFunc", 2);
            Invoke("ClientStep", 1);
        }
        #endregion


        #region Send and Recieve data functions
        /// <summary>
        /// Send variable to a given target
        /// </summary>
        /// <param name="msg">Message you want to send</param>
        /// <param name="target">Target endpoint</param>
        /// <param name="method">Send method (asynchronous, synchronous, threaded), threaded by default</param>
        public void Send(object msg, EndPoint target, SendMethod method = SendMethod.Threaded)
        {
            if (isQuitting)
            {
                Debug.LogWarning("We're quitting so we're skipping this send");
                return;
            }

            pendingData = true;

            if (highDebug)
                Debug.Log("SENDING: " + msg);

            if (target == null)
            {
                Debug.LogError("Endpoint cannot be null");
                return;
            }

            byte[] packet;


            switch (method)
            {
                case SendMethod.Sync:
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

                    socket.SendTo(packet, target);
                    pendingData = false;
                    break;

                case SendMethod.Async:
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

                    //Arguments for async send
                    SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                    args.SetBuffer(packet, 0, packet.Length);
                    args.Completed += OnSend;
                    args.RemoteEndPoint = target;

                    socket.SendToAsync(args);
                    break;

                case SendMethod.Threaded:
                    socketSendThread = new Thread(() => ThreadedSerializeSend(msg, target));
                    socketSendThread.Start();
                    break;

                default:
                    string error = "Send input invalid enum selection: " + method;
                    throw new NotImplementedException(error);
            }
        }

        /// <summary>
        /// Internal method used to serialize and send messages on another thread
        /// </summary>
        /// <param name="msg">Message to send</param>
        /// <param name="target">Target to send message to</param>
        private void ThreadedSerializeSend(object msg, EndPoint target)
        {
            Debug.LogWarning("Threaded send start");
        
            byte[] packet;

            #if UNITY_EDITOR
            Thread.Sleep(simulatedLag);
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

            socket.SendTo(packet, target);

            pendingData = false;

            Debug.LogWarning("Threaded send end");
        }

        

        /// <summary>
        /// Broadcast function for server
        /// </summary>
        /// <param name="msg">Message to broadcast</param>
        public void Broadcast(object msg, SendMethod method = SendMethod.Threaded)
        {
            pendingData = true;

            if (msg == null)
                throw new ArgumentNullException("Cannot input null object");

            if (highDebug)
                Debug.Log("Broadcast: " + msg);

            switch (method)
            {
                case SendMethod.Sync:
                    byte[] packet = prepSend(msg);

                    foreach (Client client in connectedClients)
                    {
                        socket.SendTo(packet, client.endPoint);
                    }
                    break;

                case SendMethod.Async:
                    byte[] packetAsync = prepSend(msg);

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

                    //Arguments for async send
                    SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                    args.SetBuffer(packet, 0, packet.Length);
                    args.Completed += OnSend;

                    foreach (Client c in connectedClients)
                    {
                        args.RemoteEndPoint = c.endPoint;
                        socket.SendToAsync(args);
                    }
                    break;

                case SendMethod.Threaded:
                    socketBroadcastThread = new Thread(() => ThreadedBroadcast(msg));
                    socketBroadcastThread.Start();
                    break;
            }

            if (highDebug)
                Debug.Log("Broadcast completed");

            pendingData = false;
        }
        
        /// <summary>
        /// Internal threaded broadcast message
        /// </summary>
        /// <param name="msg">Object we want to serialize and send over the network</param>
        private void ThreadedBroadcast(object msg)
        {
            if (msg == null)
                throw new ArgumentNullException("Cannot broadcast a null object");

            byte[] packet = prepSend(msg);
            connectedClients.ForEach(item => socket.SendTo(packet, item.endPoint));
        }

        /// <summary>
        /// OnSend callback
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Args used</param>
        private void OnSend(object sender, SocketAsyncEventArgs e)
        {
            Debug.Log("Async send completed");
            pendingData = false;
        }

        /// <summary>
        /// On recieve async function, called when async recieve recieves data
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event Args</param>
        private void OnRecieve(object sender, SocketAsyncEventArgs e)
        {
            if (isQuitting)
            {
                Debug.Log("Skipping recieve, as we're quitting");
                return;
            }

            if (mainThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                Debug.LogWarning("ON RECIEVE IS ON MAIN THREAD");
            }

            Debug.LogWarning("ON REC START");

            //Skip var?
            _IsListeningVar = false;

            byte[] data = e.Buffer;
            object message = "none";
            bool eventRec = false;

            int targetLength = data[1020] + data[1021] + data[1022] + data[1023];

            if (targetLength == 0)
            {
                Debug.LogWarning("Recieved an incomplete message from: " + e.RemoteEndPoint);
                Debug.LogWarning("REC SKIP");
                Recieve();
                return;
            }

            byte type = data[1019];
            Array.Resize(ref data, targetLength);
            Type recType = TypeDict.dict[(MessageType)type];

            Debug.LogWarning("TYPE: " + recType);

            try
            {
                message = Serializer.DeSerialize(recType, data);

                if (highDebug)
                    Debug.Log("MESSAGE RECIEVED: (" + message + ")");
            }
            catch (Exception ex)
            {
                Debug.LogError("Caught exception with deserialize: " + ex);
                message = "CORRUPT";
            }

            switch((MessageType)type)
            {
                #region ServerMessage
                case MessageType.ServerMessage:
                    ServerMessage sMsg = (ServerMessage)message;
                    eventRec = true;

                    if (isServer)
                    {
                        switch (sMsg.msgType)
                        {
                            case ServerMsgType.ConnectRequestEvent:
                                AddConnection(e.RemoteEndPoint);
                                break;

                            case ServerMsgType.ConnectAcceptEvent:
                                Debug.LogWarning("Recieved connection accept event on server");
                                break;

                            case ServerMsgType.ClientDisconnectEvent:
                                Debug.LogWarning("CLIENT DISCONNECTED: DISCONNECT " + e.RemoteEndPoint.ToString());
                                DisconnectClient(e.RemoteEndPoint);
                                break;

                            case ServerMsgType.PingEvent:
                                ServerMessage m = new ServerMessage { msgType = ServerMsgType.PongEvent };
                                Send(m, e.RemoteEndPoint);
                                break;

                            case ServerMsgType.PongEvent:
                                Client c = connectedClients.Find(i => i.endPoint.ToString() == e.RemoteEndPoint.ToString());

                                if (c != null)
                                {
                                    c.ping = 1000 * (unscaledTimeThreaded - c.pingMsgStartTime);
                                }

                                else
                                    Debug.LogWarning("Recieved ping from non connected client");

                                eventRec = true;
                                break;

                            case ServerMsgType.CCEvent:
                                ServerMessage sm3 = new ServerMessage { msgType = ServerMsgType.CCAliveEvent };
                                Send(sm3, e.RemoteEndPoint);
                                break;

                            case ServerMsgType.CCAliveEvent:
                                Debug.Log("CONNECTION IS ALIVE");

                                Client targetClient = connectedClients.Find(i => i.endPoint.Equals(e.RemoteEndPoint));

                                if (targetClient != null)
                                {
                                    targetClient.lastReplyRec = 0;
                                }

                                eventRec = true;
                                break;

                            case ServerMsgType.MatchRequestEvent:
                                MatchData match = OnMatchRequest();

                                if (match != null)
                                    Send(match, e.RemoteEndPoint);

                                else
                                    Debug.LogWarning("Match data returned is null, sending no data");
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
                                Debug.LogWarning("Recieved match request on client");
                                break;

                            case ServerMsgType.ConnectAcceptEvent:
                                if (sMsg.ID == connectionDenied || string.IsNullOrEmpty(sMsg.ID))
                                {
                                    Debug.LogWarning("Connection has been denied or failed");
                                    break;
                                }

                                AddConnection(e.RemoteEndPoint);
                                Debug.LogWarning("Setting ID");
                                localClientID = sMsg.ID;
                                break;

                            case ServerMsgType.ClientDisconnectEvent:
                                Client client = new Client { ID = sMsg.ID};
                                OnDisconnectClient(client);
                                break;

                            case ServerMsgType.PingEvent:
                                ServerMessage sm2 = new ServerMessage { msgType = ServerMsgType.PongEvent };
                                Send(sm2, e.RemoteEndPoint);
                                break;

                            case ServerMsgType.PongEvent:
                                Ping = 1000 * (unscaledTimeThreaded - startPingTime);
                                break;

                            case ServerMsgType.CCEvent:
                                ServerMessage sm1 = new ServerMessage { msgType = ServerMsgType.CCAliveEvent };
                                Send(sm1, e.RemoteEndPoint);
                                break;

                            case ServerMsgType.CCAliveEvent:
                                serverLastMsgTime = 0;
                                break;

                            case ServerMsgType.MatchRequestEvent:
                                Debug.LogWarning("Recieved match request on client");
                                break;

                            case ServerMsgType.ConnectionDisconnectEvent:
                                Debug.Log("You've been disconnected by the server");
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

                    if (isServer)
                    {
                        Client targetClient = connectedClients.Find(i => i.endPoint.Equals(e.RemoteEndPoint));

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

                    else if (e.RemoteEndPoint.Equals(clientConnection))
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
                    Debug.Log("NETDESTROY ID RECIEVED: " + netDestroy.IDToDestroy);

                    if (isServer)
                    {
                        NetworkDestroy(netDestroy.IDToDestroy);
                    }

                    else
                    {
                        OnNetDestroy(netDestroy.IDToDestroy);
                    }
                    break;
                #endregion

                #region MatchData
                case MessageType.MatchData:
                    eventRec = true;
                    MatchData matchData = (MatchData)message;
                    Debug.Log("MATCH DATA RECIEVED: " + matchData.MatchName);

                    Debug.LogWarning("Recieved match data when we didn't expect it");
                    Recieve();
                    return;
                #endregion
            }

            #region old switch
            /*
            switch (type)
            {
                case (byte)MessageType.String:
                    try
                    {
                        message = Serializer.DeSerialize<string>(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }

                    if (highDebug)
                        Debug.Log("STRING RECIEVED: (" + (string)message + ")");

                    #region Message check
                    if (isServer)
                    {
                        switch ((string)message)
                        {
                            case "HELLO SERVER":
                                AddConnection(e.RemoteEndPoint); //We don't send a message back here as AddConnection() handles that
                                eventRec = true;
                                break;

                            case "HELLO?":
                                Send("ALIVE", e.RemoteEndPoint);
                                eventRec = true;
                                break;

                            case "DISCONNECT":
                                Debug.LogWarning("CLIENT DISCONNECTED: DISCONNECT " + e.RemoteEndPoint.ToString());
                                DisconnectClient(e.RemoteEndPoint);
                                eventRec = true;
                                break;

                            case "ALIVE":
                                Debug.Log("CONNECTION IS ALIVE");

                                foreach (Client c in connectedClients)
                                {
                                    if (c.endPoint == e.RemoteEndPoint)
                                        c.lastReplyRec = 0;
                                }
                                eventRec = true;
                                break;

                            case "PING":
                                Send("PONG", e.RemoteEndPoint);
                                eventRec = true;
                                break;

                            case "PONG":
                                Client targetC = connectedClients.Find(i => i.endPoint.ToString() == e.RemoteEndPoint.ToString());

                                if (targetC != null)
                                {
                                    targetC.ping = 1000 * (unscaledTimeThreaded - targetC.pingMsgStartTime);
                                }

                                else
                                    Debug.LogWarning("Recieved ping from non connected client");

                                eventRec = true;
                                break;

                            case "MATCHREQUEST":
                                MatchData match = OnMatchRequest();

                                if (match != null)
                                    Send(match, e.RemoteEndPoint);

                                else
                                    Debug.LogWarning("Match data returned is null, sending no data");

                                eventRec = true;
                                break;

                            default:
                                eventRec = false;

                                if (highDebug)
                                    Debug.Log("NO EVENT");
                                break;
                        }
                    }

                    else
                    {
                        switch ((string)message)
                        {
                            case "DISCONNECT":
                                Debug.Log("You've been disconnected by the server");
                                eventRec = true;
                                Disconnect();
                                break;

                            case "HELLO?":
                                Send("ALIVE", clientConnection);
                                eventRec = true;
                                break;

                            case "ALIVE":
                                Ping = Time.unscaledTime - startPingTime;
                                serverLastMsgTime = 0;
                                eventRec = true;
                                break;

                            case "PING":
                                Send("PONG", e.RemoteEndPoint);
                                eventRec = true;
                                break;

                            case "PONG":
                                Ping = 1000 * (unscaledTimeThreaded - startPingTime);
                                eventRec = true;
                                break;

                            case "MATCHREQUEST":
                                Debug.LogWarning("Recieved match request on client");
                                eventRec = true;
                                break;

                            default:
                                if (highDebug)
                                    Debug.LogWarning("no event");

                                eventRec = false;
                                break;
                        }
                    }
                    #endregion
                    break;

                case (byte)MessageType.Int:
                    try
                    {
                        message = Serializer.DeSerialize<int>(data);
                    }

                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }

                    if (highDebug)
                        Debug.Log("INT RECIEVED: " + (int)message);

                    break;

                case (byte)MessageType.Float:
                    try
                    {
                        message = Serializer.DeSerialize<float>(data);
                    }

                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }

                    if (highDebug)
                        Debug.Log("FLOAT RECIEVED: " + (float)message);

                    break;

                case (byte)MessageType.Bool:
                    try
                    {
                        message = Serializer.DeSerialize<bool>(data);
                    }

                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }

                    if (highDebug)
                        Debug.Log("BOOL RECIEVED: " + (bool)message);
                    break;

                case (byte)MessageType.Bytes:
                    message = data;

                    if (highDebug)
                        Debug.Log("RAW BYTES RECIEVED");
                    break;

                case (byte)MessageType.Vector3:
                    try
                    {
                        message = Serializer.DeSerialize<Vector3>(data);
                        Vector3 v3 = (Vector3)message;

                        if (highDebug)
                            Debug.Log("VECTOR 3 RECIEVED: " + v3);
                    }

                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }
                    break;

                case (byte)MessageType.NetInstantiate:
                    try
                    {
                        message = Serializer.DeSerialize<NetInstantiate>(data);
                        NetInstantiate netInstantiate = (NetInstantiate)message;
                        Debug.Log("INSTANTIATE MESSAGE RECIEVED, PREFAB: " + netInstantiate.prefabName);


                        if (isServer)
                        {
                            Client targetClient = connectedClients.Find(i => i.endPoint.ToString() == e.RemoteEndPoint.ToString());

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

                        else
                        {
                            pendingObjs.Enqueue(netInstantiate);
                        }
                    }

                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }

                    eventRec = true;
                    break;

                case (byte)MessageType.DisconnectEvent:
                    try
                    {
                        message = Serializer.DeSerialize<DisconnectEvent>(data);
                        DisconnectEvent DCEvent = (DisconnectEvent)message;
                        Debug.Log("DISCONNECT EVENT RECIEVED: " + DCEvent.client);

                        Client client = new Client { endPoint =  CreateIPEndPoint(DCEvent.client)};
                        OnDisconnectClient(client);
                        eventRec = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }
                    break;

                case (byte)MessageType.PlayerData:
                    try
                    {
                        message = Serializer.DeSerialize<PlayerData>(data);
                        PlayerData pData = (PlayerData)message;
                        Debug.Log("PLAYER DATA RECIEVED");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }
                    break;

                case (byte)MessageType.ClientID:
                    try
                    {
                        message = Serializer.DeSerialize<ClientIDMsg>(data);
                        ClientIDMsg clientID = (ClientIDMsg)message;
                        Debug.Log("CLIENT ID RECIEVED: " + clientID.ID);

                        if (!isServer && clientID.msg == "HELLO CLIENT")
                        {
                            AddConnection(e.RemoteEndPoint);
                            Debug.LogWarning("Setting ID");
                            localClientID = clientID.ID;
                        }

                        eventRec = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }
                    break;

                case (byte)MessageType.NetDestroy:
                    try
                    {
                        message = Serializer.DeSerialize<NetDestroyMsg>(data);
                        NetDestroyMsg netDestroy = (NetDestroyMsg)message;
                        Debug.Log("NETDESTROY ID RECIEVED: " + netDestroy.IDToDestroy);

                        if (isServer)
                        {
                            NetworkDestroy(netDestroy.IDToDestroy);
                        }

                        else
                        {
                            OnNetDestroy(netDestroy.IDToDestroy);
                        }

                        eventRec = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";
                    }
                    break;

                case (byte)MessageType.MatchData:
                    try
                    {
                        message = Serializer.DeSerialize<MatchData>(data);
                        MatchData matchData = (MatchData)message;
                        Debug.Log("MATCH DATA RECIEVED: " + matchData.MatchName);
                        eventRec = true;

                        Recieve();
                        Debug.LogWarning("Recieved match data when we didn't expect it");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Caught exception with deserialize: " + ex);
                        message = "CORRUPT";

                        Recieve();
                        Debug.LogWarning("Recieved match data when we didn't expect it");
                    }
                    break;

                default:
                    Debug.LogError("Object recieved has no valid type");
                    break;
            }
            */
            #endregion

            if (isServer)
            {
                Client targetClient = connectedClients.Find(i => i.endPoint.Equals(e.RemoteEndPoint));
                
                if (targetClient != null)
                    targetClient.lastReplyRec = 0;
            }

            //Don't bother updating our network objects if we've recieved an event, we'll already have functions to handle these events
            if (!eventRec)
            {
                Debug.LogWarning("Current msg set 1");
                MsgEvent m = new MsgEvent { msg = message, end = e.RemoteEndPoint };
                currentMsgs.Enqueue(m);
                Debug.LogWarning("Current msg set 2");
            }


            if (highDebug)
                Debug.Log("RECIEVE FINISH");

            Recieve();
        }

        /// <summary>
        /// Recieve function to continually recieve data, either for server or client
        /// </summary>
        public void Recieve()
        {
            Debug.Log("START RECIEVE");

            if (mainThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                Debug.LogWarning("RECIEVE IS ON MAIN THREAD");
            }

            //Setup variables to send information
            int count = 1024;
            int offset = 0;
            byte[] buffer = new byte[count];

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0); //Recieve from any 
            EndPoint tmpRemote = (EndPoint)(sender); //Convert IPEndPoint to EndPoint

            //Arguments for async recieve
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(buffer, offset, count);
            args.Completed += OnRecieve;
            args.RemoteEndPoint = tmpRemote;

            //We use a recieve async function to prevent it from freezing the main thread (and editor, lmao)
            socket.ReceiveFromAsync(args);

            //OnRecieve(null, args);

            _IsListeningVar = true;
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
            Debug.LogError("MATCH: " + m.MatchName);
        }

        public virtual MatchData OnMatchRequest()
        {
            MatchData m = new MatchData
            {
                MatchName = ServerName,
                PlayerCount = connectedClients.Count
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
            if (isServer)
            {
                Client conn = new Client();
                conn.lastReplyRec = 0;
                conn.endPoint = connection;
                conn.ID = Guid.NewGuid().ToString();

                connectedClients.Add(conn);

                //ClientIDMsg iDMsg = new ClientIDMsg { ID = conn.ID, msg = "HELLO CLIENT"};
                ServerMessage iDMsg = new ServerMessage { msgType = ServerMsgType.ConnectAcceptEvent, ID = conn.ID };
                Send(iDMsg, connection);

                List<string> sentIDs = new List<string>();

                foreach (NetworkObject netObj in netObjects)
                {
                    string id = sentIDs.Find(i => i == netObj.InstanceID);

                    if (!string.IsNullOrEmpty(id))
                        continue;

                    netObj.OnNewPlayer(conn);
                    sentIDs.Add(netObj.InstanceID);
                    Send(netObj.instanMsg, connection);
                }

                Debug.Log("CLIENT CONNECTION ADDED: " + connection);
            }

            else
            {
                clientConnection = connection;
                Debug.Log("SERVER CONNECTION SET: " + connection);

                NetworkInstantiate(playerPrefab);
            }
        }

        /// <summary>
        /// Disconnects client based on endpoint
        /// </summary>
        /// <param name="connection">Endpoint to disconnect</param>
        public void DisconnectClient(EndPoint connection)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread.ManagedThreadId)
            {
                Debug.LogWarning("Called disconnect client on non main thread, queing for main thread");
                pendingEndpointsDisconnects.Enqueue(connection);
                return;
            }

            Debug.Log("DISCONNECTING ENDPOINT");

            if (isServer)
            {
                Client c = connectedClients.Find(i => i.endPoint.ToString() == connection.ToString());

                if (c != null)
                {
                    Debug.LogWarning("Connection selected");
                    connectedClients.Remove(c);
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ConnectionDisconnectEvent };
                    m.ID = c.ID;
                    Send(m, connection);
                    OnDisconnectClient(c);
                }

                else
                {
                    Debug.LogWarning("Input endpoint that is not connected");
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
            if (Thread.CurrentThread.ManagedThreadId != mainThread.ManagedThreadId)
            {
                Debug.LogWarning("Called disconnect client on non main thread, queing for main thread");
                pendingClientDisconnects.Enqueue(client);
                return;
            }

            Debug.Log("DISCONNECTING CLIENT");

            if (isServer)
            {
                connectedClients.Remove(client);
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
        /// Internal function to disconnect client
        /// </summary>
        /// <param name="client">Client to disconnect</param>
        private void OnDisconnectClient(Client client)
        {
            if (isServer)
            {
                ServerMessage dc = new ServerMessage { msgType = ServerMsgType.ClientDisconnectEvent, ID = client.ID };
                Broadcast(dc); //Broadcast disconnect
            }

            if (Thread.CurrentThread.ManagedThreadId != mainThread.ManagedThreadId)
            {
                Debug.LogWarning("Called disconnect client on non main thread, queing for main thread");
                disClientEvents.Enqueue(client);
                return;
            }

            foreach (NetworkObject obj in netObjects)
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
            Debug.Log("DISCONNECT");

            if (isServer)
            {
                Debug.LogWarning("SERVER SHUTDOWN");

                CancelInvoke("SyncStep");
                CancelInvoke("Ping");

                if (!isQuitting)
                {
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ConnectionDisconnectEvent };
                    Broadcast(m);
                }

                if (pendingData)
                {
                    Debug.Log("DATA PENDING");
                    Invoke("Disconnect", 2);
                }

                else
                {
                    NetExit();

                    foreach (NetworkObject obj in netObjects)
                    {
                        if (obj.gameObject != null)
                            Destroy(obj.gameObject);
                    }

                    netObjects = null;
                    //pendingDisconnects = null;
                    pendingObjs = null;
                    connectedClients = null;
                    currentMsgs = null;
                    threadedInstantiate = null;
                }

                isQuitting = true;
            }

            else if (clientConnection != null)
            {
                Debug.LogWarning("CLIENT SHUTDOWN");

                CancelInvoke("ClientStep");
                CancelInvoke("Ping");

                if (!isQuitting)
                {
                    ServerMessage m = new ServerMessage { msgType = ServerMsgType.ClientDisconnectEvent };
                    Send(m, clientConnection);
                }

                if (pendingData)
                {
                    Invoke("Disconnect", 2);
                    Debug.Log("DATA PENDING");
                }

                else
                {
                    List<NetworkObject> objsToDestroy = netObjects;

                    foreach (NetworkObject obj in objsToDestroy)
                    {
                        Destroy(obj.gameObject);
                    }

                    objsToDestroy = null;

                    NetExit();

                    netObjects = null;
                    //pendingDisconnects = null;
                    pendingObjs = null;
                    connectedClients = null;
                    clientConnection = null;
                    currentMsgs = null;
                    threadedInstantiate = null;
                }

                isQuitting = true;
            }

            else
            {
                Debug.LogWarning("CALLED DISCONNECT() WITHOUT BEING CONNECTED");
                isQuitting = true;
                isServer = false;
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

            else if (socket == null)
            {
                Debug.LogError("Cannot call NetworkInstantiate() without being connected");
                return;
            }

            else if (Thread.CurrentThread.ManagedThreadId != mainThread.ManagedThreadId)
            {
                Debug.LogWarning("Called network instantiate on non main thread, queing for main thread");
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
                prefabName = prefabToString(obj),
                objName = obj.name,
                pos = pos
            };

            if (highDebug)
                Debug.Log("NET INSTAN: " + pos);


            if (isServer)
            {
                netObj.senderID = serverData.ID;
                netObj.instanceID = Guid.NewGuid().ToString();
                OnNetworkInstantiate(netObj);
            }

            else if (clientConnection != null)
                Send(netObj, clientConnection);
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

            if (isServer)
            {
                OnNetDestroy(instanceID);
                Broadcast(msg);
            }

            else
            {
                Send(msg, clientConnection);
            }
        }

        private void OnNetDestroy(string instanceID)
        {
            NetworkObject obj = netObjects.Find(i => i.InstanceID == instanceID);

            if (obj == null)
                Debug.LogWarning("No network object found with ID");

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

            if (isServer)
                Broadcast(obj);

            GameObject targetObject = stringToPrefab(obj.prefabName);
            GameObject spawnedObj = Instantiate(targetObject);

            spawnedObj.transform.position = obj.pos;

            NetworkObject[] scripts = spawnedObj.GetComponents<NetworkObject>();

            foreach (NetworkObject netObj in scripts)
            {
                Debug.Log("SETTING OWNER ID ON: " + netObj);
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
            if (isQuitting)
            {
                Debug.LogWarning("Skipping Sync Step as we're quitting");
                return;
            }

            if (!_IsListeningVar)
            {
                Debug.LogWarning("We're not listening, are we spending too much time OnRecieve?");
            }

            Debug.Log("SYNC STEP");

            //Client[] clients = connectedClients.ToArray();

            Debug.Log("CONNECTED CLIENTS: " + connectedClients.Count);

            foreach (Client c in connectedClients)
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

            foreach (NetworkObject obj in netObjects)
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
            if (isQuitting)
            {
                Debug.LogWarning("Skipping Client Step as we're quitting");
                return;
            }

            Debug.Log("CLIENT STEP");

            if (clientConnection == null)
            {
                Debug.LogWarning("NO CONNECTION, ATTEMPTING CONNECT");
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.ConnectRequestEvent };
                Send(msg, targetEnd);
                return;
            }

            serverLastMsgTime++;

            if (serverLastMsgTime > 10 && serverLastMsgTime <= 20)
            {
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.CCEvent };
                Send(msg, clientConnection);
            }

            else if (serverLastMsgTime > 20)
            {
                clientConnection = null;
                Debug.Log("DISCONNECT: TIMEOUT");
            }

            foreach (NetworkObject obj in netObjects)
            {
                if (obj.SyncObj() != null)
                {
                    Send(obj.SyncObj(), clientConnection);
                }
            }

            Invoke("ClientStep", 1);
        }

        /// <summary>
        /// Function used to measure ping
        /// </summary>
        void PingFunc()
        {
            if (isQuitting)
                return;

            Debug.Log("PING: " + Ping);

            if (isServer)
            {
                foreach (Client c in connectedClients)
                {
                    c.pingMsgStartTime = Time.unscaledTime;
                    ServerMessage msg = new ServerMessage { msgType = ServerMsgType.PingEvent };
                    Send(msg, c.endPoint);
                }
            }

            else if (clientConnection != null)
            {
                startPingTime = Time.unscaledTime;
                ServerMessage msg = new ServerMessage { msgType = ServerMsgType.PingEvent };
                Send(msg, clientConnection);
            }

            Invoke("PingFunc", 2);
        }
    }
}