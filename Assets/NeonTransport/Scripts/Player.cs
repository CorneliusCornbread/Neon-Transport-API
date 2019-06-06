using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using NeonNetworking.DataTypes;

namespace NeonNetworking
{
    public class Player : NetworkObject
    {
        [Range(0f, 1f)]
        [Tooltip("The minimum percent that will be used for the target pos, based on distance to move")]
        /// <summary>
        /// The minimum percent that will be for the target pos, based on distance to move
        /// </summary>
        public float minInterp = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("The max percent that will be used for the target pos, based on distance to move")]
        /// <summary>
        /// The max percent that will be used for the target pos, based on distance to move
        /// </summary>
        public float maxInterp = 1;

        [Range(0, .25f)]
        public float interpFactor = 0.016f;

        private bool started = false;

        private Vector3 lastRealPos;

        private Vector3 targetPos;
        private Vector3 interpPos;

        private Vector3 targetScale;
        private Vector3 interpScale;

        private Quaternion targetRot;
        private Quaternion interpRot;

        public bool interp = true;
        public bool prediction = true;

        Vector3 lastPos;
        Vector3 lastScale;
        Quaternion lastRot;
        public override void OnPlayerDisconnect(Client client)
        {
            Debug.Log("ON DISCONNECT");

            if (NetworkManager.Instance.isServer && client.ID == OwnerID)
                NetworkManager.Instance.NetworkDestroy(InstanceID);

        }

        private void Update()
        {
            if (NetworkManager.Instance.isServer)
                return;

            if (!started)
            {
                targetPos = transform.position;
                targetRot = new Quaternion(0, 0, 0, 0);
                targetScale = new Vector3(1, 1, 1);

                interpPos = transform.position;
                interpRot = transform.rotation;
                interpScale = transform.localScale;

                lastPos = transform.position;
                lastRot = transform.rotation;
                lastScale = transform.localScale;
                started = true;
                return;
            }

            if (!interp)
            {
                interpPos = transform.position;
                transform.position = targetPos;
                transform.rotation = targetRot;
                transform.localScale = targetScale;
                return;
            }

            float error = Vector3.Distance(targetPos, transform.position);
            float i = Mathf.Pow(error, 2) * interpFactor;
            float targetInterp = Mathf.Clamp(i, minInterp, maxInterp);

            interpPos.x = Mathf.Lerp(transform.position.x, targetPos.x, targetInterp);
            interpPos.y = Mathf.Lerp(transform.position.y, targetPos.y, targetInterp);
            interpPos.z = Mathf.Lerp(transform.position.z, targetPos.z, targetInterp);

            error = Vector3.Distance(targetScale, transform.localScale);
            i = Mathf.Pow(error, 2) * interpFactor;
            targetInterp = Mathf.Clamp(i, minInterp, maxInterp);

            interpScale.x = Mathf.Lerp(transform.localScale.x, targetScale.x, targetInterp);
            interpScale.y = Mathf.Lerp(transform.localScale.y, targetScale.y, targetInterp);
            interpScale.z = Mathf.Lerp(transform.localScale.z, targetScale.z, targetInterp);

            error = QuanternionDist(targetRot, transform.rotation);
            i = Mathf.Pow(error, 2) * interpFactor;
            targetInterp = Mathf.Clamp(i, minInterp, maxInterp);

            interpRot.x = Mathf.Lerp(transform.rotation.x, targetRot.x, targetInterp);
            interpRot.y = Mathf.Lerp(transform.rotation.y, targetRot.y, targetInterp);
            interpRot.z = Mathf.Lerp(transform.rotation.z, targetRot.z, targetInterp);
            interpRot.w = Mathf.Lerp(transform.rotation.w, targetRot.w, targetInterp);

            transform.position = interpPos;
            transform.localScale = interpScale;
            transform.rotation = interpRot;
        }

        private float QuanternionDist(Quaternion q1, Quaternion q2)
        {
            float value = 0;

            value += Mathf.Abs(q1.x - q2.x);
            value += Mathf.Abs(q1.y - q2.y);
            value += Mathf.Abs(q1.z - q2.z);
            value += Mathf.Abs(q1.w - q2.w);

            return value;
        }

        protected override void OnRecieve(MsgEvent msg, EndPoint sender)
        {
            Debug.LogWarning("PLAYER REC");

            if (msg.msg.GetType() == typeof(PlayerData))
            {
                Debug.LogWarning("PLAYERDATA TYPE");

                PlayerData pData = (PlayerData)msg.msg;

                if (pData.instanceID == InstanceID && pData.playerID == OwnerID)
                {
                    targetPos = pData.pos;
                    targetRot = pData.rot;
                    targetScale = pData.scale;
                }

                if (prediction)
                {
                    Vector3 predictedDelta = new Vector3
                    {
                        x = pData.pos.x - lastRealPos.x,
                        y = pData.pos.y - lastRealPos.y,
                        z = pData.pos.z - lastRealPos.z
                    };

                    lastRealPos = pData.pos;
                    targetPos += predictedDelta * (ping / 1000);
                }
            }
        }

        public override object SyncObj()
        {
            if (!NetworkManager.Instance.isServer || (lastPos == transform.position && lastRot == transform.rotation && lastScale == transform.localScale))
                return null;

            PlayerData data = new PlayerData
            {
                playerID = OwnerID,
                pos = transform.position,
                instanceID = InstanceID,
                rot = transform.rotation,
                scale = transform.localScale
            };

            lastPos = data.pos;
            lastRot = data.rot;
            lastScale = data.scale;

            return data;
        }
    }
}
