using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace FMSolution.FMWebSocket
{
    public enum FMWebSocketNetworkType { Room, WebSocket }
    public enum FMWebSocketSendType { All, Server, Others, Target, RoomMaster }
    public enum FMWebSocketConnectionStatus { Disconnected, WebSocketReady, FMWebSocketConnected }
    public enum FMSslProtocols
    {
        Default = 0xF0,
        None = 0x0,
        Ssl2 = 0xC,
        Ssl3 = 0x30,
        Tls = 0xC0,
#if UNITY_2019_1_OR_NEWER
        Tls11 = 0x300,
        Tls12 = 0xC00
#endif
    }

    [System.Serializable]
    public class ConnectedFMWebSocketClient { public string wsid = ""; }

    [AddComponentMenu("FMETP/Network/FMWebSocketManager")]
    public class FMWebSocketManager : MonoBehaviour
    {
        #region EditorProps
        public bool EditorShowNetworking = true;
        public bool EditorShowSyncTransformation = true;
        public bool EditorShowEvents = true;
        public bool EditorShowDebug = true;

        public bool EditorShowWebSocketSettings = false;

        public bool EditorShowNetworkObjects = false;

        public bool EditorShowReceiverEvents = false;
        public bool EditorShowConnectionEvents = false;

        public bool EditorShowNetworkViews = true;
        #endregion

        public bool ShowLog = true;
        internal void DebugLog(string _value) { if (ShowLog) Debug.Log("FMLog: " + _value); }
        internal void DebugLogWarning(string _value) { if (ShowLog) Debug.LogWarning("FMLog: " + _value); }

        private string[] urlStartStrings = new string[] { "http://", "https://", "ws://", "wss://" };
        private void CheckSettingsIP()
        {
            if (Settings.IP == null) return;
            Settings.IP = Settings.IP.TrimEnd(' ');
            Settings.IP = Settings.IP.TrimEnd('/');
            for (int i = 0; i < urlStartStrings.Length; i++)
            {
                if (Settings.IP.StartsWith(urlStartStrings[i])) Settings.IP = Settings.IP.Substring(urlStartStrings[i].Length);
            }
        }
        /// <summary name="Action_SetIP()">
        /// Assign new IP address for connection
        /// </summary>
        /// <param name="_ip">ip address of server, "127.0.0.1" by default</param>
        public void Action_SetIP(string _ip) { Settings.IP = _ip; CheckSettingsIP(); }
        /// <summary>
        /// Assign new port number for connection
        /// </summary>
        /// <param name="_port">port of server, (string)3000 -> (int)3000 by default</param>
        public void Action_SetPort(string _port) { Settings.port = int.Parse(_port); }
        /// <summary>
        /// Turn on/off Ssl support
        /// </summary>
        /// <param name="_value">true: enable Ssl; false: disable Ssl</param>
        public void Action_SetSslEnabled(bool _value) { Settings.sslEnabled = _value; }
        /// <summary>
        /// Turn on/off "portRequired"
        /// </summary>
        /// <param name="_value">true: require port; false: not require port</param>
        public void Action_SetPortRequired(bool _value) { Settings.portRequired = _value; }
        /// <summary name="Action_SetIP()">
        /// Assign new IP address for connection
        /// </summary>
        /// <param name="_ip">ip address of server, "127.0.0.1" by default</param>
        public void Action_SetRoomName(string _roomName) { RoomName = _roomName; }

        [System.Serializable]
        public class FMWebSocketSettings
        {
            public string IP = "127.0.0.1";
            public int port = 3000;
            public bool sslEnabled = false;
            public FMSslProtocols sslProtocols = FMSslProtocols.Default;

            public bool portRequired = true;

            [Tooltip("(( suggested for low-end mobile, but not recommend for streaming large data ))")]
            public bool UseMainThreadSender = true;

            public FMWebSocketConnectionStatus ConnectionStatus = FMWebSocketConnectionStatus.Disconnected;
            public string wsid = "";
            public bool autoReconnect = true;

            public int pingMS = 0;
            public int latencyMS = 0;
            public bool wsJoinedRoom = false;
            public bool wsRoomMaster = false;
        }

        public List<ConnectedFMWebSocketClient> ConnectedClients { get { return Initialised ? fmwebsocket.ConnectedClients : null; } }
        public static FMWebSocketManager instance;
        private void Awake()
        {
            Application.runInBackground = true;
            if (instance == null) instance = this;

            Initialised = false;
            Settings.ConnectionStatus = FMWebSocketConnectionStatus.Disconnected;
        }

        private void Start() { if (AutoInit) Init(NetworkType); }
        public bool AutoInit = true;
        public bool Initialised = false;
        public FMWebSocketNetworkType NetworkType = FMWebSocketNetworkType.Room;
        public string RoomName = "MyRoomTest";
        public FMWebSocketSettings Settings = new FMWebSocketSettings();

        [HideInInspector] public FMWebSocketCore fmwebsocket;

        public UnityEventByteArray OnReceivedByteDataEvent = new UnityEventByteArray();
        public UnityEventString OnReceivedStringDataEvent = new UnityEventString();
        public UnityEventByteArray GetRawReceivedByteDataEvent = new UnityEventByteArray();
        public UnityEventString GetRawReceivedStringDataEvent = new UnityEventString();

        public UnityEventString OnClientConnectedEvent = new UnityEventString();
        public UnityEventString OnClientDisconnectedEvent = new UnityEventString();
        public UnityEventInt OnConnectionCountChangedEvent = new UnityEventInt();

        //room
        public UnityEventString OnJoinedLobbyEvent = new UnityEventString();
        public UnityEventString OnJoinedRoomEvent = new UnityEventString();
        public void OnJoinedLobby(string inputWSID)
        {
            OnJoinedLobbyEvent.Invoke(inputWSID);
            DebugLog("OnJoinedLobbyEvent");
        }
        public void OnJoinedRoom(string inputRoomName)
        {
            OnJoinedRoomEvent.Invoke(inputRoomName);
            DebugLog("OnJoinedRoom: " + inputRoomName);

            //For Network Room Objects, default owner is RoomMaster
            if (Settings.wsRoomMaster) AutoAssignRoomMasterOwner();
        }
        public void OnClientConnected(string inputClientWSID)
        {
            OnClientConnectedEvent.Invoke(inputClientWSID);
            DebugLog("OnClientConnected: " + inputClientWSID);

            //Update Clients this info!
            SyncNetworkViewsInfo(inputClientWSID);
        }

        public void OnClientDisconnected(string inputClientWSID)
        {
            OnClientDisconnectedEvent.Invoke(inputClientWSID);
            DebugLog("OnClientDisonnected: " + inputClientWSID);

            //check and cleanup network views
            CleanupNetworkViews(inputClientWSID);
        }
        public void OnConnectionCountChanged(int inputConnectionCount)
        {
            OnConnectionCountChangedEvent.Invoke(inputConnectionCount);
            DebugLog("OnConnectionCountChanged: " + inputConnectionCount);
        }

        internal void OnConnectionStatusUpdated(FMWebSocketConnectionStatus inputConnectionStatus) { if (Settings.ConnectionStatus != inputConnectionStatus) OnConnectionStatusChanged(inputConnectionStatus); }
        internal void OnConnectionStatusChanged(FMWebSocketConnectionStatus inputConnectionStatus)
        {
            DebugLog("Connection status changed: " + inputConnectionStatus);
            Settings.ConnectionStatus = inputConnectionStatus;
            if (Settings.ConnectionStatus == FMWebSocketConnectionStatus.Disconnected) RemoveAllNetworkAndLocalViews();
        }

        internal void OnJoinedRoomUpdated(bool inputJoinedRoomStatus) { if (Settings.wsJoinedRoom != inputJoinedRoomStatus) OnJoinedRoomChanged(inputJoinedRoomStatus); }
        internal void OnJoinedRoomChanged(bool inputJoinedRoomStatus) { Settings.wsJoinedRoom = inputJoinedRoomStatus; }
        internal void OnRoomMasterStatusUpdated(bool inputRoomMasterStatus) { if (Settings.wsRoomMaster != inputRoomMasterStatus) OnRoomMasterStatusChanged(inputRoomMasterStatus); }
        internal void OnRoomMasterStatusChanged(bool inputRoomMasterStatus)
        {
            DebugLog("OnRoomMasterStatusChanged: " + inputRoomMasterStatus);
            Settings.wsRoomMaster = inputRoomMasterStatus;

            //For Network Room Objects, default owner is RoomMaster
            if (Settings.wsRoomMaster) AutoAssignRoomMasterOwner();
        }
        internal void OnRoomMasterRequestEvent(bool inputRegistered)
        {
            DebugLog("OnRoomMasterRequestEvent: " + inputRegistered);
            //check and cleanup network views
            if (inputRegistered) CleanupNetworkViews(null);
        }

        /// <summary>
        /// Initialise FMWebSocket server
        /// </summary>
        public void Init(FMWebSocketNetworkType inputNetworkType)
        {
            NetworkType = inputNetworkType;
            CheckSettingsIP();
            if (fmwebsocket == null)
            {
#if !UNITY_WEBGL || UNITY_EDITOR //FM Notes: stripped in build bug, with #if UNITY_WEBGL || !UNITY_EDITOR
                fmwebsocket = this.gameObject.AddComponent<FMWebSocketPlatformStandalone.Component>();
#else
                fmwebsocket = this.gameObject.AddComponent<FMWebSocketPlatformWebGL.Component>();
#endif
            }

            fmwebsocket.hideFlags = HideFlags.HideInInspector;

            fmwebsocket.Manager = this;
            fmwebsocket.NetworkType = NetworkType;
            fmwebsocket.IP = Settings.IP;
            fmwebsocket.port = Settings.port;
            fmwebsocket.sslEnabled = Settings.sslEnabled;
            fmwebsocket.sslProtocols = Settings.sslProtocols;
            fmwebsocket.portRequired = Settings.portRequired;

            fmwebsocket.autoReconnect = Settings.autoReconnect;

            fmwebsocket.UseMainThreadSender = Application.platform == RuntimePlatform.WebGLPlayer ? true : Settings.UseMainThreadSender;
            fmwebsocket.ShowLog = ShowLog;

            fmwebsocket.Connect();
            Initialised = true;
        }

        public void Action_Close()
        {
            fmwebsocket.Close();
            Initialised = false;
            Settings.ConnectionStatus = FMWebSocketConnectionStatus.Disconnected;
        }
        public void Action_JoinOrCreateRoom() { Init(FMWebSocketNetworkType.Room); }
        public void Action_JoinOrCreateRoom(string inputRoomName)
        {
            RoomName = inputRoomName;
            Init(FMWebSocketNetworkType.Room);
        }
        public void Action_InitAsWebSocket() { Init(FMWebSocketNetworkType.WebSocket); }
        public void Action_RequestRoomMaster()
        {
            if (NetworkType != FMWebSocketNetworkType.Room) return;
            if (!Initialised) return;
            if (!fmwebsocket.IsWebSocketConnected()) return;
            fmwebsocket.FMWebSocketEvent("requestRoomMaster", Settings.wsid);
        }

        public void Send(byte[] _byteData, FMWebSocketSendType _type) { Send(_byteData, _type, null); }
        public void Send(string _stringData, FMWebSocketSendType _type) { Send(_stringData, _type, null); }

        public void SendToAll(byte[] _byteData) { Send(_byteData, FMWebSocketSendType.All, null); }
        public void SendToServer(byte[] _byteData) { Send(_byteData, FMWebSocketSendType.Server, null); }
        public void SendToOthers(byte[] _byteData) { Send(_byteData, FMWebSocketSendType.Others, null); }
        public void SendToTarget(byte[] _byteData, string _wsid) { Send(_byteData, FMWebSocketSendType.Target, _wsid); }

        public void SendToAll(string _stringData) { Send(_stringData, FMWebSocketSendType.All, null); }
        public void SendToServer(string _stringData) { Send(_stringData, FMWebSocketSendType.Server, null); }
        public void SendToOthers(string _stringData) { Send(_stringData, FMWebSocketSendType.Others, null); }
        public void SendToTarget(string _stringData, string _wsid) { Send(_stringData, FMWebSocketSendType.Target, _wsid); }

        /// <summary name="Send()">
        /// Send FMWebSocket data as byte[]
        /// </summary>
        /// <param name="_ip">It requires FMWebSocket NetworkType: Server or Client</param>
        public void Send(byte[] _byteData, FMWebSocketSendType _type, string _targetID)
        {
            if (NetworkType != FMWebSocketNetworkType.Room) return;
            if (Initialised && Settings.wsJoinedRoom) fmwebsocket.Send(_byteData, _type, _targetID);
        }

        /// <summary name="Send()">
        /// Send FMWebSocket message as string
        /// </summary>
        /// <param name="_ip">It requires FMWebSocket NetworkType: Server or Client</param>
        public void Send(string _stringData, FMWebSocketSendType _type, string _targetID)
        {
            if (NetworkType != FMWebSocketNetworkType.Room) return;
            if (Initialised && Settings.wsJoinedRoom) fmwebsocket.Send(_stringData, _type, _targetID);
        }

        /// <summary name="Send()">
        /// Send WebSocket message as string
        /// </summary>
        /// <param name="_ip">It requires FMWebSocket NetworkType: WebSocket</param>
        public void WebSocketSend(string _stringData) { if (Initialised) fmwebsocket.WebSocketSend(_stringData); }

        /// <summary name="Send()">
        /// Send WebSocket data as byte[]
        /// </summary>
        /// <param name="_ip">It requires FMWebSocket NetworkType: WebSocket</param>
        public void WebSocketSend(byte[] _byteData) { if (Initialised) fmwebsocket.WebSocketSend(_byteData); }

        #region FM WebSocket Sync Network Object
        public FMSerializableDictionary<int, FMWebSocketView> NetworkViews = new FMSerializableDictionary<int, FMWebSocketView>();
        private void RemoveAllNetworkAndLocalViews()
        {
            int[] _networkViewsKey = NetworkViews.Keys.ToArray();
            foreach(int _key in _networkViewsKey) RemoveNetworkView(_key);
            NetworkViews = new FMSerializableDictionary<int, FMWebSocketView>();

            int[] _localViewsKey = pendingLocalViews.Keys.ToArray();
            foreach (int _key in _localViewsKey) RemovePendingLocalView(_key);
            pendingLocalViews = new FMSerializableDictionary<int, FMWebSocketView>();

            int[] _localCallbacksKey = pendingCallbacks.Keys.ToArray();
            foreach (int _key in _localCallbacksKey) { if (pendingCallbacks.ContainsKey(_key)) pendingCallbacks.Remove(_key); }
            pendingCallbacks = new FMSerializableDictionary<int, UnityAction<FMWebSocketView>>();
        }
        public void UpdateRoomViewIDsAll()
        {
            if (Application.isPlaying) return;
            if (Application.IsPlaying(this)) return;

            FMWebSocketView[] _allViews = FindObjectsOfType<FMWebSocketView>();
            _allViews.OrderBy(_viewObj => _viewObj.gameObject.transform.GetSiblingIndex()).ToArray();
            _allViews = _allViews.Reverse().ToArray();

            foreach (FMWebSocketView _view in _allViews) Action_EditorRegisterRoomNetworkedView(_view);

            NetworkViews.SortByKey();
            DebugLog("Updated All Network Views");
        }

        public void Action_EditorRegisterRoomNetworkedView(FMWebSocketView inputView)
        {
            if (Application.isPlaying) return;
            if (NetworkViews.ContainsValue(inputView)) Action_EditorUnregisterRoomNetworkedView(inputView);

            int _viewID = GetAvailableNetworkedViewID();
            DebugLog("registered NetworkViews(Global Room): [" + _viewID + "] " + inputView.gameObject.name);
            NetworkViews.Add(_viewID, inputView);
            inputView.SetViewID(_viewID);
        }
        public void Action_EditorUnregisterRoomNetworkedView(FMWebSocketView inputView)
        {
            if (Application.isPlaying) return;
            if (NetworkViews.ContainsValue(inputView))
            {
                int[] _networkViewsKey = NetworkViews.Keys.ToArray();
                for (int i = 0; i < _networkViewsKey.Length; i++)
                {
                    int _key = _networkViewsKey[i];
                    if (NetworkViews.ContainsKey(_key))
                    {
                        if (NetworkViews.TryGetValue(_key, out FMWebSocketView _view, false))
                        {
                            if (_view == null || _view == inputView) Action_EditorUnregisterRoomNetworkedView(_key);
                            DebugLog("Unregistered: " + inputView.gameObject.name);
                        }
                    }
                }
            }
        }
        public void Action_EditorUnregisterRoomNetworkedView(int _viewID)
        {
            if (Application.isPlaying) return;
            if (NetworkViews.ContainsKey(_viewID)) NetworkViews.Remove(_viewID);
        }

        private int FindSmallestNonDuplicate(int[] numbers)
        {
            Array.Sort(numbers);//sort the order from 1,2,3...etc
            HashSet<int> numberSet = new HashSet<int>(numbers);
            int smallestNonDuplicate = 1;
            while (numberSet.Contains(smallestNonDuplicate)) smallestNonDuplicate++;
            return smallestNonDuplicate;
        }
        private int GetAvailableNetworkedViewID()
        {
            int _foundSmallestViewID = FindSmallestNonDuplicate(NetworkViews.Keys.ToArray());
            //remove this viewID from recent Destroyed list
            if (recentRoomMasterDestroyedList.Contains(_foundSmallestViewID)) recentRoomMasterDestroyedList.Remove(_foundSmallestViewID);
            return _foundSmallestViewID;
        }

        private void RemoveNetworkView(int inputKey) { RemoveView(NetworkViews, inputKey); }
        private void RemovePendingLocalView(int inputKey) { RemoveView(pendingLocalViews, inputKey); }
        private ConcurrentQueue<FMWebSocketView> appendQueueDestroyViews = new ConcurrentQueue<FMWebSocketView>();
        private void RemoveView(FMSerializableDictionary<int, FMWebSocketView> inputDicitionary, int inputKey)
        {
            try
            {
                if (!inputDicitionary.ContainsKey(inputKey)) return;
                if (inputDicitionary.TryGetValue(inputKey, out FMWebSocketView _view, true))
                {
                    if (_view.GetIsInstaniatedInRuntime())
                    {
                        inputDicitionary.Remove(inputKey);
                        appendQueueDestroyViews.Enqueue(_view);//need to destroy it in MainThread, enqueue it and check in update...
                    }
                }
                else { inputDicitionary.Remove(inputKey); }//remove null item...
            }
            catch (Exception e) { DebugLog(e.ToString()); }
        }

        private List<int> recentRoomMasterDestroyedList = new List<int>();
        private void AddToRecentDestroyList(int inputKey) { recentRoomMasterDestroyedList.Add(inputKey); }
        private void CleanupNetworkViews(string inputOwnerID = null)
        {
            int[] _networkViewsKey;
            if (!string.IsNullOrEmpty(inputOwnerID))
            {
                //remove target OwnerID
                _networkViewsKey = NetworkViews.Keys.ToArray();
                for (int i = 0; i < _networkViewsKey.Length; i++)
                {
                    int _key = _networkViewsKey[i];
                    if (NetworkViews.ContainsKey(_key))
                    {
                        if (NetworkViews.TryGetValue(_key, out FMWebSocketView _view, true))
                        {
                            if (_view.GetOwnerID() == inputOwnerID)
                            {
                                RemoveNetworkView(_key);
                                AddToRecentDestroyList(_key);
                            }
                        }
                    }
                }
            }

            //just double check...
            _networkViewsKey = NetworkViews.Keys.ToArray();
            for (int i = 0; i < _networkViewsKey.Length; i++)
            {
                int _key = _networkViewsKey[i];

                bool _foundExistedOwner = false;
                for (int j = 0; j < ConnectedClients.Count && !_foundExistedOwner; j++)
                {
                    if (NetworkViews.TryGetValue(_key, out FMWebSocketView _view, true))
                    {
                        if (_view.GetOwnerID() == ConnectedClients[j].wsid) _foundExistedOwner = true;
                    }
                }
                if (!_foundExistedOwner)
                {
                    RemoveNetworkView(_key);
                    AddToRecentDestroyList(_key);
                }
            }

            //Update Clients this info!
            SyncNetworkViewsInfo();
        }
        private void AutoAssignRoomMasterOwner()
        {
            int[] _networkViewsKey = NetworkViews.Keys.ToArray();
            for (int i = 0; i < _networkViewsKey.Length; i++)
            {
                int _key = _networkViewsKey[i];
                if (NetworkViews.TryGetValue(_key, out FMWebSocketView _view, true))
                {
                    if (!_view.GetIsInstaniatedInRuntime())
                    {
                        _view.SetOwnerID(Settings.wsid);
                        _view.SetIsOwner(true);
                    }
                }
            }
        }

        private void SyncNetworkViewsInfo() { fmwebsocket.SendNetworkInfo(EncodeNetworkViews(), FMWebSocketSendType.Others); }
        private void SyncNetworkViewsInfo(string inputTargetID) { fmwebsocket.SendNetworkInfo(EncodeNetworkViews(), FMWebSocketSendType.Target, inputTargetID); }
        private byte[] EncodeNetworkViews()
        {
            //ownerID, prefabName, viewID
            List<byte> _encoded = new List<byte>();

            _encoded.AddRange(new byte[] { 19, 93, 12, 23 });

            int[] _networkViewsKey = NetworkViews.Keys.ToArray();
            for (int i = 0; i < _networkViewsKey.Length; i++)
            {
                int _key = _networkViewsKey[i];
                if (NetworkViews.TryGetValue(_key, out FMWebSocketView _view, true))
                {
                    byte[] _ownerIDByte = Encoding.ASCII.GetBytes(_view.GetOwnerID());
                    byte[] _prefabNameByte = Encoding.ASCII.GetBytes(_view.GetPrefabName());
                    byte[] _viewIDByte = BitConverter.GetBytes(_view.GetViewID());

                    _encoded.AddRange(new byte[] { (byte)_ownerIDByte.Length, (byte)_prefabNameByte.Length, (byte)_viewIDByte.Length, (byte)0 });
                    _encoded.AddRange(_ownerIDByte);
                    _encoded.AddRange(_prefabNameByte);
                    _encoded.AddRange(_viewIDByte);
                }
            }
            return _encoded.ToArray();
        }

        private int DecodeNetworkedViewData(byte[] inputData, int inputOffset, ref Dictionary<int, FMWebSocketView> referenceNetworkViews)
        {
            int _offset = inputOffset;

            int _length_ownerID = (int)inputData[_offset];
            int _length_prefabName = (int)inputData[_offset + 1];
            int _length_viewID = (int)inputData[_offset + 2];
            _offset += 4;//first 4bytes are metadata(length)

            string _ownerID = Encoding.ASCII.GetString(inputData, _offset, _length_ownerID);
            _offset += _length_ownerID;

            string _prefabName = Encoding.ASCII.GetString(inputData, _offset, _length_prefabName);
            _offset += _length_prefabName;

            int _viewID = BitConverter.ToInt32(inputData, _offset);
            _offset += _length_viewID;

            //if exist, check prefab name, update owner id
            //if exist, prefab name is different, remove existing gameobject, instantiate new prefab
            //if not exist, instantiate new prefab
            if (!NetworkViews.ContainsKey(_viewID))
            {
                if (!recentRoomMasterDestroyedList.Contains(_viewID)) InstaniateNetworkObject(_ownerID, -1, _prefabName, _viewID);
            }
            else
            {
                if (NetworkViews.TryGetValue(_viewID, out FMWebSocketView _view, true))
                {
                    if (_view.GetPrefabName() != _prefabName)
                    {
                        RemoveNetworkView(_viewID);
                        InstaniateNetworkObject(_ownerID, -1, _prefabName, _viewID);
                    }
                    else
                    {
                        _view.SetOwnerID(_ownerID);
                    }
                }
            }

            if (NetworkViews.ContainsKey(_viewID))
            {
                if (NetworkViews.TryGetValue(_viewID, out FMWebSocketView _view, true))
                {
                    if (!referenceNetworkViews.ContainsKey(_viewID)) referenceNetworkViews.Add(_viewID, _view);
                }
            }

            return _offset;
        }
        private void DecodeNetworkViews(byte[] inputData)
        {
            int _count = inputData.Length;
            int _offset = 4; //skip first 4 bytes metadata

            Dictionary<int, FMWebSocketView> _temporaryViews = new Dictionary<int, FMWebSocketView>();
            while (_offset < _count) _offset = DecodeNetworkedViewData(inputData, _offset, ref _temporaryViews);

            //compare two dictionary...
            //if networkedViews exist, but temporaryViews not exist, remove it from networkedViews
            int[] _networkViewsKey = NetworkViews.Keys.ToArray();
            for (int i = 0; i < _networkViewsKey.Length; i++)
            {
                int _key = _networkViewsKey[i];
                if (!_temporaryViews.ContainsKey(_key)) RemoveNetworkView(_key);
            }
        }

        private void DecodeAndInstaniateNetworkObject(byte[] inputBytes)
        {
            //ignore first 4bytes as metadata symbol..
            //metadata(4) + ownerID(13) + prefabName(total-4-13-4 = total - 21) + viewID(4)
            string _ownerID = Encoding.ASCII.GetString(inputBytes, 4, 13);
            int _localID = BitConverter.ToInt32(inputBytes, 17);
            string _prefabName = Encoding.ASCII.GetString(inputBytes, 21, inputBytes.Length - 25);
            int _viewID = BitConverter.ToInt32(inputBytes, inputBytes.Length - 4);
            InstaniateNetworkObject(_ownerID, _localID, _prefabName, _viewID);
        }

        internal void OnReceivedNetworkedObject(byte[] inputBytes)
        {
            if (inputBytes.Length < 12) return;
            byte[] _globalMetadata = new byte[] { inputBytes[0], inputBytes[1], inputBytes[2], inputBytes[3] };

            if (_globalMetadata[0] == 12 && _globalMetadata[1] == 23 && _globalMetadata[2] == 0 && _globalMetadata[3] == 0)
            {
                //request from room clients
                byte[] _viewIDByte = BitConverter.GetBytes(GetAvailableNetworkedViewID());
                byte[] _sendByte = new byte[inputBytes.Length + _viewIDByte.Length];

                Buffer.BlockCopy(inputBytes, 0, _sendByte, 0, inputBytes.Length);
                Buffer.BlockCopy(_viewIDByte, 0, _sendByte, inputBytes.Length, 4);
                _sendByte[0] = (byte)23;
                _sendByte[1] = (byte)12;

                //send to others...
                fmwebsocket.SendNetworkInfo(_sendByte, FMWebSocketSendType.Others);
                //server will instaniate itself locally
                DecodeAndInstaniateNetworkObject(_sendByte);
            }
            else if (_globalMetadata[0] == 23 && _globalMetadata[1] == 12 && _globalMetadata[2] == 0 && _globalMetadata[3] == 0)
            {
                //respond from room Master
                DecodeAndInstaniateNetworkObject(inputBytes);
            }
            else if (_globalMetadata[0] == 19 && _globalMetadata[1] == 93 && _globalMetadata[2] == 0 && _globalMetadata[3] == 0)
            {
                //network transformation sync
                byte[] _transformViewBytes = new byte[inputBytes.Length - 4];
                Buffer.BlockCopy(inputBytes, 4, _transformViewBytes, 0, _transformViewBytes.Length);
                Action_DecodeNetworkTransformView(_transformViewBytes);
            }
            else if (_globalMetadata[0] == 19 && _globalMetadata[1] == 93 && _globalMetadata[2] == 12 && _globalMetadata[3] == 23)
            {
                DecodeNetworkViews(inputBytes);
            }
        }

        private int localViewID = 0;
        private int GetAvailableLocalViewID() { return localViewID = (localViewID + 1) % (int.MaxValue / 2); }
        private FMSerializableDictionary<int, FMWebSocketView> pendingLocalViews = new FMSerializableDictionary<int, FMWebSocketView>();
        private FMSerializableDictionary<int, UnityAction<FMWebSocketView>> pendingCallbacks = new FMSerializableDictionary<int, UnityAction<FMWebSocketView>>();

        private void UpdateViewInfo(FMWebSocketView inputView, string inputOwnerID, string inputPrefabName, int inputNetworkID)
        {
            inputView.SetFMWebSocketManager(this);
            inputView.SetIsOwner(inputOwnerID == Settings.wsid);
            inputView.SetOwnerID(inputOwnerID);
            inputView.SetViewID(inputNetworkID);
            inputView.SetPrefabName(inputPrefabName);
            inputView.SetIsInstaniatedInRuntime(true);
            inputView.gameObject.name = "view_" + inputView.GetViewID() + "_" + inputPrefabName + (inputView.IsOwner ? "(Owner)" : "") + (inputNetworkID < 0 ? "_local" : "");
        }
        public FMWebSocketView InstaniateNetworkObject(string inputPrefabName, UnityAction<FMWebSocketView> networkViewCallback = null)
        {
            if (Settings.ConnectionStatus != FMWebSocketConnectionStatus.FMWebSocketConnected)
            {
                DebugLog("Can't instaniate network object, please check your connection.");
                return null;
            }

            FMWebSocketView _view = null;
            try { _view = Instantiate(Resources.Load<FMWebSocketView>(inputPrefabName)); } catch { DebugLogWarning("Missing FMWebSocketView Component"); }

            if (_view != null)
            {
                _view.SetLocalViewID(GetAvailableLocalViewID());
                UpdateViewInfo(_view, Settings.wsid, inputPrefabName, -1);

                pendingLocalViews.Add(_view.GetLocalViewID(), _view);
                if (networkViewCallback != null) pendingCallbacks.Add(_view.GetLocalViewID(), networkViewCallback);

                byte[] _wsidByte = Encoding.ASCII.GetBytes(_view.GetOwnerID());
                byte[] _localIDByte = BitConverter.GetBytes(_view.GetLocalViewID());
                byte[] _prefabByte = Encoding.ASCII.GetBytes(_view.GetPrefabName());
                byte[] _sendByte = new byte[4 + _wsidByte.Length + _localIDByte.Length + _prefabByte.Length];
                _sendByte[0] = (byte)12;
                _sendByte[1] = (byte)23;
                _sendByte[2] = (byte)0;
                _sendByte[3] = (byte)0;
                Buffer.BlockCopy(_wsidByte, 0, _sendByte, 4, 13);
                Buffer.BlockCopy(_localIDByte, 0, _sendByte, 17, 4);
                Buffer.BlockCopy(_prefabByte, 0, _sendByte, 21, _prefabByte.Length);
                fmwebsocket.SendNetworkInfo(_sendByte, FMWebSocketSendType.Server);
            }
            return _view;
        }

        private void InstaniateNetworkObject(string inputOwnerID, int inputLocalID, string inputPrefabName, int inputNetworkID)
        {
            FMWebSocketView _view = null;
            if (inputOwnerID == Settings.wsid)
            {
                if (pendingLocalViews.TryGetValue(inputLocalID, out FMWebSocketView _pendingView))
                {
                    _view = _pendingView;
                    pendingLocalViews.Remove(inputLocalID);

                    UpdateViewInfo(_view, inputOwnerID, inputPrefabName, inputNetworkID);

                    if (pendingCallbacks.TryGetValue(inputLocalID, out UnityAction<FMWebSocketView> _pendingCallback))
                    {
                        _pendingCallback.Invoke(_view);
                        pendingCallbacks.Remove(inputLocalID);
                    }
                }
            }
            else
            {
                try
                {
                    _view = Instantiate(Resources.Load<FMWebSocketView>(inputPrefabName));
                    UpdateViewInfo(_view, inputOwnerID, inputPrefabName, inputNetworkID);
                }
                catch { DebugLogWarning("Missing FMWebSocketView Component"); }
            }

            if (_view != null)
            {
                NetworkViews.Add(inputNetworkID, _view);
                DebugLog("Instaniated Network Object: " + _view.gameObject.name);
            }
        }

        ///
        public Queue<FMWebsocketViewSyncData> AppendQueueTransformSyncData = new Queue<FMWebsocketViewSyncData>();
        public void Action_EnqueueTransformSyncData(FMWebsocketViewSyncData inputSyncData)
        {
            if (!enabled) return;
            if (!Initialised) return;
            if (Settings.ConnectionStatus != FMWebSocketConnectionStatus.FMWebSocketConnected) return;
            AppendQueueTransformSyncData.Enqueue(inputSyncData);
        }

        private void EncodeSyncData(FMWebsocketViewSyncData inputData, bool assignOwnerInfo, bool assignPosition, bool assignRotation, bool assignScale, ref List<byte> referenceByteList)
        {
            if (assignOwnerInfo)
            {
                byte[] _ownerInfoBytes = Encoding.ASCII.GetBytes(inputData.ownerID);
                byte[] _lengthBytes = BitConverter.GetBytes(_ownerInfoBytes.Length);
                referenceByteList.AddRange(_lengthBytes);
                referenceByteList.AddRange(_ownerInfoBytes);
            }
            if (assignPosition)
            {
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.position.x));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.position.y));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.position.z));
            }
            if (assignRotation)
            {
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.rotation.x));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.rotation.y));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.rotation.z));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.rotation.w));
            }
            if (assignScale)
            {
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.localScale.x));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.localScale.y));
                referenceByteList.AddRange(BitConverter.GetBytes(inputData.localScale.z));
            }
        }
        //reserve range 0-999 for internal usage
        [Range(0, 999)] private UInt16 labelTransformView = 101;
        private void DequeueTransformSyncData()
        {
            if (AppendQueueTransformSyncData.Count == 0) return;

            byte[] _networkedObjectSymbolBytes = new byte[] { 19, 93, 0, 0 };//check symbol first byte 93
            byte[] _labelBytes = BitConverter.GetBytes(labelTransformView);
            byte[] _timestampBytes = BitConverter.GetBytes(Time.realtimeSinceStartup);

            //metadata: _networkedObjectSymbolBytes(4) + label(2) + timestamp(4)
            //syncData: viewID(4) + syncType(1) + position(12) + rotation(16) + scale(12)
            //4 + 1 + 12 + 16 + 12 = 45
            while (AppendQueueTransformSyncData.Count > 0)
            {
                List<byte> _sentBytes = new List<byte>();
                _sentBytes.AddRange(_networkedObjectSymbolBytes);
                _sentBytes.AddRange(_labelBytes);
                _sentBytes.AddRange(_timestampBytes);

                while (_sentBytes.Count < 1350 && AppendQueueTransformSyncData.Count > 0)
                {
                    FMWebsocketViewSyncData _syncData = AppendQueueTransformSyncData.Dequeue();

                    _sentBytes.AddRange(BitConverter.GetBytes(_syncData.viewID));
                    _sentBytes.Add((byte)_syncData.syncType);

                    switch (_syncData.syncType)
                    {
                        case FMWebsocketViewSyncType.All: EncodeSyncData(_syncData, false, true, true, true, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.PositionOnly: EncodeSyncData(_syncData, false, true, false, false, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.RotationOnly: EncodeSyncData(_syncData, false, false, true, false, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.ScaleOnly: EncodeSyncData(_syncData, false, false, false, true, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.PositionAndRotation: EncodeSyncData(_syncData, false, true, true, false, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.PositionAndScale: EncodeSyncData(_syncData, false, true, false, true, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.RotationAndScale: EncodeSyncData(_syncData, false, false, true, true, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.OwnerInfo: EncodeSyncData(_syncData, true, false, false, false, ref _sentBytes); break;
                        case FMWebsocketViewSyncType.None: break;
                    }
                }
                if (_sentBytes.Count >= 27) SendNetworkTransformToOthers(_sentBytes.ToArray());
            }
        }
        public void SendNetworkTransformToOthers(byte[] _byteData) { fmwebsocket.SendNetworkInfo(_byteData, FMWebSocketSendType.Others); }

        private void Update()
        {
            while (!appendQueueDestroyViews.IsEmpty)
            {
                if(appendQueueDestroyViews.TryDequeue(out FMWebSocketView _view))
                {
                    if (_view.gameObject != null) Destroy(_view.gameObject);
                    DebugLog("Deleted View Object: " + _view.gameObject);
                }
            }
        }
        private float syncTimer = 0f;
        private float syncFPS = 30f;
        private void LateUpdate()
        {
            if (Initialised == false) return;

            //network view sync(sender)
            syncTimer += Time.deltaTime;
            if (syncTimer > (1f / syncFPS)) DequeueTransformSyncData();
            syncTimer %= (1f / syncFPS);
        }

        #endregion

        #region Decode
        private int DecodeSyncData(byte[] inputData, int inputOffset, bool decodeOwnerInfo, bool decodePosition, bool decodeRotation, bool decodeScale, ref FMWebsocketViewSyncData referenceSyncData)
        {
            int _offset = inputOffset;
            if (decodeOwnerInfo)
            {
                int _lengthBytes = BitConverter.ToInt32(inputData, _offset);
                referenceSyncData.ownerID = Encoding.ASCII.GetString(inputData, _offset + 4, _lengthBytes);
                _offset += 4 + _lengthBytes;
            }
            if (decodePosition)
            {
                referenceSyncData.position.x = BitConverter.ToSingle(inputData, _offset);
                referenceSyncData.position.y = BitConverter.ToSingle(inputData, _offset + 4);
                referenceSyncData.position.z = BitConverter.ToSingle(inputData, _offset + 8);
                _offset += 12;
            }
            if (decodeRotation)
            {
                referenceSyncData.rotation.x = BitConverter.ToSingle(inputData, _offset);
                referenceSyncData.rotation.y = BitConverter.ToSingle(inputData, _offset + 4);
                referenceSyncData.rotation.z = BitConverter.ToSingle(inputData, _offset + 8);
                referenceSyncData.rotation.w = BitConverter.ToSingle(inputData, _offset + 12);
                _offset += 16;
            }
            if (decodeScale)
            {
                referenceSyncData.localScale.x = BitConverter.ToSingle(inputData, _offset);
                referenceSyncData.localScale.y = BitConverter.ToSingle(inputData, _offset + 4);
                referenceSyncData.localScale.z = BitConverter.ToSingle(inputData, _offset + 8);
                _offset += 12;
            }

            return _offset;
        }

        private void Action_DecodeNetworkTransformView(byte[] inputData)
        {
            int _count = inputData.Length;
            int _offset = 0;

            UInt16 _label = BitConverter.ToUInt16(inputData, _offset);
            _offset += 2;

            if (_label != labelTransformView) return;

            float Timestamp = BitConverter.ToSingle(inputData, _offset);
            _offset += 4;

            while (_offset < _count)
            {
                FMWebsocketViewSyncData _syncData = new FMWebsocketViewSyncData();
                _syncData.viewID = BitConverter.ToInt32(inputData, _offset);
                _syncData.syncType = (FMWebsocketViewSyncType)((int)inputData[_offset + 4]);
                _offset += 5;

                switch (_syncData.syncType)
                {
                    case FMWebsocketViewSyncType.All: _offset = DecodeSyncData(inputData, _offset, false, true, true, true, ref _syncData); break;
                    case FMWebsocketViewSyncType.PositionOnly: _offset = DecodeSyncData(inputData, _offset, false, true, false, false, ref _syncData); break;
                    case FMWebsocketViewSyncType.RotationOnly: _offset = DecodeSyncData(inputData, _offset, false, false, true, false, ref _syncData); break;
                    case FMWebsocketViewSyncType.ScaleOnly: _offset = DecodeSyncData(inputData, _offset, false, false, false, true, ref _syncData); break;
                    case FMWebsocketViewSyncType.PositionAndRotation: _offset = DecodeSyncData(inputData, _offset, false, true, true, false, ref _syncData); break;
                    case FMWebsocketViewSyncType.PositionAndScale: _offset = DecodeSyncData(inputData, _offset, false, true, false, true, ref _syncData); break;
                    case FMWebsocketViewSyncType.RotationAndScale: _offset = DecodeSyncData(inputData, _offset, false, false, true, true, ref _syncData); break;
                    case FMWebsocketViewSyncType.OwnerInfo: _offset = DecodeSyncData(inputData, _offset, true, false, false, false, ref _syncData); break;
                    case FMWebsocketViewSyncType.None: break;
                }

                if (NetworkViews.TryGetValue(_syncData.viewID, out FMWebSocketView _view, true)) _view.Action_UpdateSyncData(_syncData, Timestamp);
            }
        }
        #endregion
    }
    #region FMWebSocket Sync Network Object
    public enum FMWebsocketViewSyncType
    {
        None = 0,
        All = 255,
        PositionOnly = 1,
        RotationOnly = 2,
        ScaleOnly = 3,
        PositionAndRotation = 4,
        PositionAndScale = 5,
        RotationAndScale = 6,
        OwnerInfo = 10,
    }
    public struct FMWebsocketViewSyncData
    {
        public string ownerID;
        public int viewID;
        public bool isOwner;
        public FMWebsocketViewSyncType syncType;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
    }
    #endregion
}
