﻿using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Captury
{
    /// <summary>
    /// Instantiates Captury Avatars and handles the user assignment
    /// </summary>
    [RequireComponent(typeof(CapturyNetworkPlugin), typeof(CapturyLeapIntegration))]
    public class CapturyAvatarManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Avatar template which will be instantiated for each tracked Captury Avatar")]
        private GameObject avatarTemplateObject;

        [SerializeField]
        [Tooltip("If true, first found skeleton will be assigned to local player")]
        private bool assignFirstSkeleton;

        [SerializeField]
        [Tooltip("The TransformFollower which will be manipulated by the captury tracking (should be on a parent GameObject of the camera).")]
        private TransformFollower transformFollower;

        /// <summary>
        /// The <see cref="CapturyNetworkPlugin"/> which handles the connection to the captuy server
        /// </summary>
        private CapturyNetworkPlugin networkPlugin;

        /// <summary>
        /// The <see cref="CapturyLeapIntegration"/> which reads the leap input data
        /// </summary>
        private CapturyLeapIntegration capturyLeapIntegration;

        /// <summary>
        /// List of <see cref="CapturySkeleton"/> which will be instantiated in the next Update
        /// </summary>
        private List<CapturySkeleton> newSkeletons = new List<CapturySkeleton>();
        /// <summary>
        /// List of <see cref="CapturySkeleton"/> which will be destroyed in the next Update
        /// </summary>
        private List<CapturySkeleton> lostSkeletons = new List<CapturySkeleton>();
        /// <summary>
        /// List of <see cref="CapturySkeleton"/> which are currently tracked
        /// </summary>
        private List<CapturySkeleton> trackedSkeletons = new List<CapturySkeleton>();

        /// <summary>
        /// True if local player is assigned to a Captury avatar
        /// </summary>
        private bool isPlayerAssigned = false;

        /// <summary>
        /// The local player id which will be read from capturyconfig.json
        /// The avatar with this marker id will be automatically assigned to the local player
        /// </summary>
        private int localPlayerID = -9999;

        /// <summary>
        /// Path of the captury config file
        /// </summary>
        private const string CAPTURY_CONFIG_FILE_PATH = "./capturyconfig.txt";

        /// <summary>
        /// Avatar transform names, to find the right transforms of an instantiated avatar.
        /// </summary>
        private const string AVATAR_LEFT_HAND_TRANSFORM_NAME = "LeftFingerBase";
        private const string AVATAR_RIGHT_HAND_TRANSFORM_NAME = "RightFingerBase";
        private const string AVATAR_HEAD_TRANSFORM_NAME = "Head";

        void Start()
        {
            LoadConfig();
            networkPlugin = GetComponent<CapturyNetworkPlugin>();
            capturyLeapIntegration = GetComponent<CapturyLeapIntegration>();

            if (transformFollower == null)
            {
                transformFollower = FindObjectOfType<TransformFollower>();
                if(transformFollower == null)
                {
                    Debug.LogError("No TransformFollower found in Scene. Camera manipulation by Captury tracking won't work.");
                }
            }

            // keep the CapturyAvatarManager GameObject between scenes
            DontDestroyOnLoad(gameObject);

            // register for skeleton events
            networkPlugin.foundSkeleton += OnFoundSkeleton;
            networkPlugin.lostSkeleton += OnLostSkeleton;
            // register for AR Tag (marker) events
            networkPlugin.detectedARTags += OnDetectedARTags;
        }

        void OnDestroy()
        {
            // unregister from events
            if (networkPlugin != null)
            {
                networkPlugin.foundSkeleton -= OnFoundSkeleton;
                networkPlugin.lostSkeleton -= OnLostSkeleton;
                networkPlugin.detectedARTags -= OnDetectedARTags;
            }
        }

        void Update()
        {
            lock (newSkeletons)
            {
                InstantiateAvatars(newSkeletons);
            }
            lock (lostSkeletons)
            {
                DestroyAvatars(lostSkeletons);
            }

            CheckPlayerSkeletonAssignment();
        }

        /// <summary>
        /// Called when a new captury skeleton is found
        /// </summary>
        /// <param name="skeleton"></param>
        void OnFoundSkeleton(CapturySkeleton skeleton)
        {
            Debug.Log("CapturyAvatarManager found skeleton with id " + skeleton.id + " and name " + skeleton.name);
            lock (newSkeletons)
            {
                newSkeletons.Add(skeleton);
            }
        }

        /// <summary>
        /// Called when a captury skeleton is lost
        /// </summary>
        /// <param name="skeleton"></param>
        void OnLostSkeleton(CapturySkeleton skeleton)
        {
            Debug.Log("CapturyAvatarManager lost skeleton with id " + skeleton.id + " and name " + skeleton.name);
            lock (lostSkeletons)
            {
                lostSkeletons.Add(skeleton);
            }
            // clear the assignment between local player and the skelton if it's lost
            if (isPlayerAssigned && IsLocalPlayer(skeleton))
            {
                ClearPlayerAssignment();
            }
        }

        /// <summary>
        /// Called when one or more captury AR Tags are detected
        /// </summary>
        /// <param name="skeleton"></param>
        void OnDetectedARTags(CapturyARTag[] arTags)
        {
            Debug.Log("Detected " + arTags.Length + " AR Tags");
        }

        /// <summary>
        /// Instantiates the avatars for the given list of skeletons
        /// </summary>
        /// <param name="skeletons"></param>
        private void InstantiateAvatars(List<CapturySkeleton> skeletons)
        {
            lock (trackedSkeletons)
            {
                foreach (CapturySkeleton skel in skeletons)
                {
                    Debug.Log("Instantiating avatar for skeleton with id " + skel.id + " and name " + skel.name);
                    GameObject actor = Instantiate(avatarTemplateObject);
                    DontDestroyOnLoad(actor);
                    actor.SetActive(true);
                    skel.mesh = actor;
                    trackedSkeletons.Add(skel);
                }
                skeletons.Clear();
            }
        }

        /// <summary>
        /// Destorys avatars for the given list of skeletons
        /// </summary>
        private void DestroyAvatars(List<CapturySkeleton> skeltons)
        {
            lock (trackedSkeletons)
            {
                foreach (CapturySkeleton skel in skeltons)
                {
                    Debug.Log("Destroying avatar for skeleton with id " + skel.id + " and name " + skel.name);
                    Destroy(skel.mesh);
                    skel.mesh = null;
                    trackedSkeletons.Remove(skel);
                }
                skeltons.Clear();
            }
        }

        /// <summary>
        /// If the player is not set
        /// </summary>
        private void CheckPlayerSkeletonAssignment()
        {
            if (isPlayerAssigned == false)
            {
                lock (trackedSkeletons)
                {
                    if (assignFirstSkeleton)
                    {
                        if (trackedSkeletons.Count > 0)
                        {
                            AssignPlayerToSkeleton(trackedSkeletons[0]);
                        }
                    }
                    else
                    {
                        foreach (CapturySkeleton skel in trackedSkeletons)
                        {
                            if (IsLocalPlayer(skel))
                            {
                                AssignPlayerToSkeleton(skel);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Assigns the local player to the given <see cref="CapturySkeleton"/>.
        /// </summary>
        /// <param name="skeleton"></param>
        private void AssignPlayerToSkeleton(CapturySkeleton skeleton)
        {
            Transform left = null;
            Transform right = null;
            Transform head = null;
            GameObject avatar = skeleton.mesh.gameObject;
            Component[] trafos = avatar.transform.GetComponentsInChildren<Transform>();
            foreach (Transform child in trafos)
            {
                if (child.name.EndsWith(AVATAR_LEFT_HAND_TRANSFORM_NAME))
                {
                    left = child;
                }
                if (child.name.EndsWith(AVATAR_RIGHT_HAND_TRANSFORM_NAME))
                {
                    right = child;
                }
                if (child.name.EndsWith(AVATAR_HEAD_TRANSFORM_NAME))
                {
                    head = child;
                }
            }
            if (left != null && right != null)
            {
                capturyLeapIntegration.setTargetModel(left, right, skeleton.id);
            }
            else
            {
                Debug.Log("Cannot find hands on target avatar with name '" + AVATAR_LEFT_HAND_TRANSFORM_NAME + "' and '" + AVATAR_RIGHT_HAND_TRANSFORM_NAME + "'");
            }
            Debug.Log("Assigned local player to skeleton with name " + skeleton.name + " and id " + skeleton.id);
            if (head != null)
            {
                transformFollower.Target = head;
            }
            else
            {
                Debug.Log("Cannot find head on target avatar with name " + AVATAR_HEAD_TRANSFORM_NAME);
            }
            isPlayerAssigned = true;
        }

        /// <summary>
        /// Clears the assignment of the local player with an skeleton
        /// </summary>
        private void ClearPlayerAssignment()
        {
            capturyLeapIntegration.setTargetModel(null, null, -1);
            transformFollower.Target = null;
        }

        /// <summary>
        /// Return true if the given skeleton is the local player skeleton
        /// </summary>
        /// <param name="skeleton"></param>
        /// <returns></returns>
        bool IsLocalPlayer(CapturySkeleton skeleton)
        {
            return skeleton.id == localPlayerID;
        }

        /// <summary>
        /// Loads the config file.
        /// Config values:
        /// markerID=0...10
        /// </summary>
        private void LoadConfig()
        {
            // read the local player id
            if (File.Exists(CAPTURY_CONFIG_FILE_PATH))
            {
                string[] configFileLines = File.ReadAllLines(CAPTURY_CONFIG_FILE_PATH);
                foreach (string line in configFileLines)
                {
                    if (line.StartsWith("markerID="))
                    {
                        int startIndex = "markerID=".Length;
                        localPlayerID = int.Parse(line.Substring(startIndex));
                        Debug.Log("localPlayerID = " + localPlayerID);
                    }
                }
            }
            else
            {
                Debug.LogError("No Captury config file found at " + CAPTURY_CONFIG_FILE_PATH);
            }
        }
    }
}
