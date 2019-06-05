using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using System.Net;
using System;
using NeonNetworking.DataTypes;

namespace NeonNetworking
{
    public class NetworkObject : MonoBehaviour
    {
        public string OwnerID;
        public string InstanceID;
        public NetInstantiate instanMsg;

        /// <summary>
        /// Returns isServer variable from networkManager, is false if we are a client
        /// </summary>
        public bool isServer
        {
            get
            {
                return NetworkManager.Instance.isServer;
            }
        }

        /// <summary>
        /// Compares local ID to owner of this network object, if they match then it's true
        /// </summary>
        public bool isOwner
        {
            get
            {
                if (NetworkManager.Instance.localClientID == OwnerID)
                    return true;

                else
                    return false;
            }
        }

        void Start()
        {
            Debug.Log("Adding Net Object");
            NetworkManager.Instance.netObjects.Add(this);
        }

        private void OnDestroy()
        {
            if (!NetworkManager.Instance.isQuitting)
                NetworkManager.Instance.netObjects.Remove(this);
        }

        public void rec(MsgEvent msg, EndPoint sender)
        {
            OnRecieve(msg, sender);
        }

        protected virtual void OnRecieve(MsgEvent msg, EndPoint sender)
        {
            //Function for users to hook into
        }

        public virtual object SyncObj()
        {
            return null;
        }

        public virtual void OnNewPlayer(Client client)
        {

        }

        public virtual void OnPlayerDisconnect(Client client)
        {

        }
    }
}
