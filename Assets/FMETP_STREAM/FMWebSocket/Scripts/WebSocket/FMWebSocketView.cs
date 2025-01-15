using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FMSolution.FMWebSocket
{
    //[ExecuteInEditMode]
    [DisallowMultipleComponent]
    [System.Serializable]
    public class FMWebSocketView : MonoBehaviour
    {
        public FMWebSocketManager FMWebSocket;
        [SerializeField] private string ownerID = "";
        [SerializeField] private int viewID = -1;
        [SerializeField] private int localViewID = -1;
        public void SetLocalViewID(int inputValue) { localViewID = inputValue; }
        public int GetLocalViewID() { return localViewID; }

        [SerializeField] private string prefabName = "";

        [Space]
        [SerializeField] private bool isInstaniatedInRuntime = false;
        [SerializeField] private bool isOwner = false;
        public bool IsOwner
        {
            get { return isOwner; }
            set
            {
                isOwner = value;
                try
                {
                    ResetNetworkObjectSyncTimestamp();

                    //force taking ownership when it's true...etc
                    if (isOwner)
                    {
                        updatingOwnership = true;
                        EnqueueTransformSyncData();
                    }
                }
                catch (Exception e) { Debug.LogWarning(e); }
            }
        }
        private float syncTimer = 0f;
        [Range(1f, 60f)] public float SyncFPS = 20f;
        private float SyncFPS_old = -1;
        public FMWebsocketViewSyncType SyncType = FMWebsocketViewSyncType.PositionAndRotation;

        private FMWebsocketViewSyncData syncData = new FMWebsocketViewSyncData();
        private FMWebsocketViewSyncData receivedSyncData = new FMWebsocketViewSyncData();

        private bool updatingOwnership = false;
        private float updateOwnershipTimer = 0f;
        private float updateOwnershipThreshold = 1f;
        public void Action_UpdateSyncData(FMWebsocketViewSyncData inputSyncData, float inputTimestamp)
        {
            //ignore and skip it if updating ownership
            if (updatingOwnership) return;

            isOwner = false;
            receivedSyncData = inputSyncData;

            float Timestamp = inputTimestamp;
            if (Timestamp > LastReceivedTimestamp)
            {
                LastReceivedTimestamp = TargetTimestamp;
                TargetTimestamp = Timestamp;
                CurrentTimestamp = LastReceivedTimestamp;
            }
        }

        public string GetOwnerID() { return ownerID; }
        public string GetPrefabName() { return prefabName; }
        public int GetViewID() { return viewID; }
        public bool GetIsInstaniatedInRuntime() { return isInstaniatedInRuntime; }

        public void SetFMWebSocketManager(FMWebSocketManager inputFMWebsocket) { FMWebSocket = inputFMWebsocket; }
        public void SetIsOwner(bool inputIsOwner) { isOwner = inputIsOwner; }
        public void SetOwnerID(string inputOwnerID) { ownerID = inputOwnerID; }
        public void SetPrefabName(string inputPrefabName) { prefabName = inputPrefabName; }
        public void SetViewID(int inputViewID) { viewID = inputViewID; }
        public void SetIsInstaniatedInRuntime(bool inputIsInstaniatedInRuntime) { isInstaniatedInRuntime = inputIsInstaniatedInRuntime; }

        private void EnqueueTransformSyncData()
        {
            if (SyncType == FMWebsocketViewSyncType.None) return;

            syncData.viewID = viewID;
            syncData.syncType = SyncType;

            syncData.position = transform.position;
            syncData.rotation = transform.rotation;
            syncData.localScale = transform.localScale;

            //skip owner info, if its ownerID is null or empty
            if (syncData.syncType == FMWebsocketViewSyncType.OwnerInfo && string.IsNullOrEmpty(syncData.ownerID)) return;

            FMWebSocket.Action_EnqueueTransformSyncData(syncData);
        }
        private void EnqueueNetworkViewInfo()
        {
            if (SyncType == FMWebsocketViewSyncType.None) return;

            ownerID = FMWebSocket.Settings.wsid;
            syncData.ownerID = ownerID;
            syncData.viewID = viewID;
            syncData.syncType = FMWebsocketViewSyncType.OwnerInfo;

            syncData.position = transform.position;
            syncData.rotation = transform.rotation;
            syncData.localScale = transform.localScale;
            FMWebSocket.Action_EnqueueTransformSyncData(syncData);
        }

        /// <summary name="Action_TakeOwnership()">
        /// Request Server, and Transfer Ownership to myself
        /// </summary>
        public void Action_TakeOwnership()
        {
            IsOwner = true;
            updatingOwnership = true;

            EnqueueNetworkViewInfo();
        }

        private float LastReceivedTimestamp = 0f;
        private float TargetTimestamp = 0f;
        private float CurrentTimestamp = 0f;
        private void ResetNetworkObjectSyncTimestamp()
        {
            //reset network sync timestamp
            CurrentTimestamp = 0f;
            LastReceivedTimestamp = 0f;
            TargetTimestamp = 0f;
        }

        //
        private void Update()
        {
            if (!Application.isPlaying) return;
            if (updatingOwnership)
            {
                updateOwnershipTimer += Time.deltaTime;
                if (updateOwnershipTimer > updateOwnershipThreshold)
                {
                    updatingOwnership = false;
                    updateOwnershipTimer = 0f;
                }

                return;
            }

            if (isOwner)
            {
                //on sync fps changes, reset the timer..
                if (SyncFPS != SyncFPS_old)
                {
                    SyncFPS_old = SyncFPS;
                    syncTimer = 0f;
                }

                syncTimer += Time.deltaTime;
                if (syncTimer > (1f / SyncFPS))
                {
                    syncTimer %= (1f / SyncFPS);

                    EnqueueTransformSyncData();
                }
            }
            else
            {
                if (LastReceivedTimestamp <= 0)
                {
                    //force stay at the bottom
                    transform.position = new Vector3(0f, int.MinValue, 0f);
                    transform.rotation = Quaternion.identity;
                    transform.localScale = new Vector3(1f, 1f, 1f);
                }
            }
        }

        private void OverrideTransformation(bool overridePosition, bool overrideRotation, bool overrideScale, float inputStep)
        {
            if (overridePosition) transform.position = Vector3.Slerp(transform.position, receivedSyncData.position, inputStep);
            if (overrideRotation) transform.rotation = Quaternion.Slerp(transform.rotation, receivedSyncData.rotation, inputStep);
            if (overrideScale) transform.localScale = Vector3.Slerp(transform.localScale, receivedSyncData.localScale, inputStep);
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;

            if (isOwner) return;
            if (LastReceivedTimestamp > 0)
            {
                //keep delta time, but update in late update, to make sure it override the transformation properly.
                CurrentTimestamp += Time.deltaTime;
                float step = (CurrentTimestamp - LastReceivedTimestamp) / (TargetTimestamp - LastReceivedTimestamp);
                step = Mathf.Clamp(step, 0f, 1f);

                switch (receivedSyncData.syncType)
                {
                    case FMWebsocketViewSyncType.All: OverrideTransformation(true, true, true, step); break;
                    case FMWebsocketViewSyncType.PositionOnly: OverrideTransformation(true, false, false, step); break;
                    case FMWebsocketViewSyncType.RotationOnly: OverrideTransformation(false, true, false, step); break;
                    case FMWebsocketViewSyncType.ScaleOnly: OverrideTransformation(false, false, true, step); break;
                    case FMWebsocketViewSyncType.PositionAndRotation: OverrideTransformation(true, true, false, step); break;
                    case FMWebsocketViewSyncType.PositionAndScale: OverrideTransformation(true, false, true, step); break;
                    case FMWebsocketViewSyncType.RotationAndScale: OverrideTransformation(false, true, true, step); break;
                    case FMWebsocketViewSyncType.OwnerInfo: SetOwnerID(receivedSyncData.ownerID); break;
                    case FMWebsocketViewSyncType.None: break;
                }
            }
            else
            {
                //force stay at the bottom
                transform.position = new Vector3(0f, int.MinValue, 0f);
                transform.rotation = Quaternion.identity;
                transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (FMWebSocket == null) return;
            FMWebSocket.Action_EditorUnregisterRoomNetworkedView(this);
        }

        private void Reset() { UpdateRoomViewID(); }
        private void OnValidate() { UpdateRoomViewID(); }

        private void FindAndGetFMWebSocket()
        {
            FMWebSocketManager[] _fmwebsockets = FindObjectsOfType<FMWebSocketManager>();
            for (int i = 0; i < _fmwebsockets.Length; i++)
            {
                if (i == 0 || _fmwebsockets[i].NetworkType == FMWebSocketNetworkType.Room)
                {
                    FMWebSocket = _fmwebsockets[i];
                }
            }
        }
        public void UpdateRoomViewID()
        {
            if (gameObject.scene.name == null)
            {
                if (FMWebSocket != null) FMWebSocket.Action_EditorUnregisterRoomNetworkedView(this);
                SetViewID(-1);
                FMWebSocket = null;
                return;
            }

            if (Application.isPlaying) return;
            if (Application.IsPlaying(this)) return;

            if (FMWebSocket == null) FindAndGetFMWebSocket();
            if (FMWebSocket == null)
            {
                SetViewID(-1);
                return;
            }

            //check, if my view < 0 -> new item
            if (GetViewID() < 0) FMWebSocket.Action_EditorRegisterRoomNetworkedView(this);
            if (!FMWebSocket.NetworkViews.ContainsValue(this)) FMWebSocket.Action_EditorRegisterRoomNetworkedView(this);
        }
    }
}
