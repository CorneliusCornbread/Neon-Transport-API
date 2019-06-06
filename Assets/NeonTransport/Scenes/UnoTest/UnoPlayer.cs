using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using NeonNetworking.DataTypes;

namespace NeonNetworking
{
    public class UnoPlayer : NetworkObject
    {

        public override void OnPlayerDisconnect(Client client)
        {
            Debug.Log("ON DISCONNECT");

            if (NetworkManager.Instance.isServer && client.ID == OwnerID)
                NetworkManager.Instance.NetworkDestroy(InstanceID);

        }

        protected override void OnRecieve(MsgEvent msg, EndPoint sender)
        {
            //STUB
        }

        public override object SyncObj()
        {
            if (!NetworkManager.Instance.isServer)
                return null;

            return "yes";
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (NetworkManager.Instance.isServer)
                return;
        }
    }
}
