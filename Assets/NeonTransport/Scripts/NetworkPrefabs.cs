using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonNetworking
{
    public class NetworkPrefabs : MonoBehaviour
    {
        public GameObject[] objects;

        public Dictionary<int, GameObject> prefabDict = new Dictionary<int, GameObject>();

        /// <summary>
        /// Function used to setup our dictionary
        /// </summary>
        public void Initialize()
        {

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject g = objects[i];

                try
                {
                    GameObject f = prefabDict[i];
                    Debug.LogError("Duplicate prefab found with: " + g.name);
                }


                catch
                {
                    prefabDict.Add(i, g);
                    Debug.Log("Instance ID: " + i);
                }
            }
        }
    }
}
