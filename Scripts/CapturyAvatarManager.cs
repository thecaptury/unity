using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [Tooltip("Avatar prefabs for local players (without head). userAvatarID is set in the Captury config file (see CapturyConfigManager for more infos)")]
        private GameObject[] localAvatarPrefabs = new GameObject[] { };

        [SerializeField]
        [Tooltip("Avatar prefabs for remote players (with head). userAvatarID is set in he Captury config file (see CapturyConfigManager for more infos)")]
        private GameObject[] remoteAvatarPrefabs = new GameObject[] { };

        [SerializeField]
        [Tooltip("The default avatar prefab which will be instantiated if no user is assigned to a skeleton.")]
        private GameObject defaultAvatar;

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
        /// The <see cref="CapturySkeleton"/> which is assigned to the local player.
        /// null if local player is not assigned to a skeleton yet.
        /// </summary>
        private CapturySkeleton playerSkeleton;

        /// <summary>
        /// The captury config will be loaded from <see cref="CapturyConfigManager.configFileName"/>
        /// </summary>
        private CapturyConfig capturyConfig;

        /// <summary>
        /// Avatar transform names, to find the right transforms of an instantiated avatar.
        /// </summary>
        private const string AVATAR_LEFT_HAND_TRANSFORM_NAME = "LeftFingerBase";
        private const string AVATAR_RIGHT_HAND_TRANSFORM_NAME = "RightFingerBase";
        private const string AVATAR_HEAD_TRANSFORM_NAME = "Head";

        /// <summary>
        /// Player Assignment Changed is fired when the assignement of the local player with a skeleton changed
        /// skeleton is null if the assignment was cleared
        /// </summary>
        /// <param name="skeleton"></param>
        public delegate void PlayerAssignmentChangedDelegate(int skeletonID, bool isAssigned);
        public event PlayerAssignmentChangedDelegate PlayerAssignmentChanged;

        /// <summary>
        /// Used to determine if we didn't have ARTag updates between two frames and need to destroy the AR Tag GameObjects
        /// </summary>
        private bool arTagsUpdated = false;

        /// <summary>
        /// Debug GameObjects for ARTags
        /// </summary>
        private Dictionary<ARTag, GameObject> trackedARTagGameObjects = new Dictionary<ARTag, GameObject>();

        /// <summary>
        /// List of <see cref="ARTag"/> which are currently tracked
        /// </summary>
        private List<ARTag> trackedARTags = new List<ARTag>();

        #region Unity Methods
        private void Start()
        {
            // get config
            capturyConfig = CapturyConfigManager.Config;

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

            // check the avatar prefabs
            if(defaultAvatar == null)
            {
                Debug.LogError("defaultAvatar not set. Make sure you assign a Avatar prefab to CapturyAvatarManager.defaultAvatar");
            }
            if (localAvatarPrefabs.Length != remoteAvatarPrefabs.Length)
            {
                Debug.LogError("localAvatarPrefabs.Length != remoteAvatarPrefabs.Length. For every localAvatarPrefab (without head) there has to be a remoteAvatarPrefab (with head) which will be spawned on remote experiences");
            }

            // keep the CapturyAvatarManager GameObject between scenes
            DontDestroyOnLoad(gameObject);

            // register for skeleton events
            networkPlugin.SkeletonFound += OnSkeletonFound;
            networkPlugin.SkeletonLost += OnSkeletonLost;
            // register for AR Tag (marker) events
            networkPlugin.ARTagsDetected += OnARTagsDetected;
        }

        private void Update()
        {
            lock (newSkeletons)
            {
                lock (trackedSkeletons)
                {
                    InstantiateDefaultAvatars(newSkeletons);
                }
            }
            lock (lostSkeletons)
            {
                lock (trackedSkeletons)
                {
                    DestroyAvatars(lostSkeletons);
                }
            }
            lock (trackedSkeletons)
            {
                lock (trackedARTags)
                {
                    // check headset to skeleton assignment with AR tags
                    if (playerSkeleton == null)
                    {
                        TryARTagToSkeletonAssignemnt(trackedARTags);
                    }

                    // Clear tracked AR Tags if there's no AR Tag update since last frame.
                    if (arTagsUpdated == false)
                    {
                        ClearTrackedARTags();
                    }

                    if (Input.GetKeyDown(KeyCode.C))
                    {
                        // calibrate AR Tag offset
                        CalibrateHeadsetARTags(trackedARTags, playerSkeleton);
                    }

                    arTagsUpdated = false;
                }
            }
        }

        private void LateUpdate()
        {
            // rotational drift correction
            List<ARTag> trackedHeadsetARTags = GetHeadsetARTags(trackedARTags);
            if (trackedHeadsetARTags.Count > 0)
            {
                OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
                if(cameraRig != null)
                {
                    //Debug.LogFormat("Slerping from {0} to {1}", cameraRig.transform.rotation.eulerAngles, trackedHeadsetARTags[0].rotation.eulerAngles);
                    //cameraRig.transform.rotation = Quaternion.Slerp(cameraRig.transform.rotation, trackedHeadsetARTags[0].rotation, 0.1f);
                }
            }
        }

        private void OnDestroy()
        {
            // unregister from events
            if (networkPlugin != null)
            {
                networkPlugin.SkeletonFound -= OnSkeletonFound;
                networkPlugin.SkeletonLost -= OnSkeletonLost;
                networkPlugin.ARTagsDetected -= OnARTagsDetected;
            }
        }

        private void OnApplicationQuit()
        {
            CapturyConfigManager.SaveConfig();
        }
        #endregion Unity Methods

        /// <summary>
        /// Can be called from a multiplayer manager to set the skeletons playerID
        /// </summary>
        /// <param name="skeletonID">Captury Skeleton id</param>
        /// <param name="playerID">Networking Player id</param>
        public void SetSkeletonPlayerID(int skeletonID, int playerID)
        {
            CapturySkeleton skel = trackedSkeletons.Single(s => s.id == skeletonID);
            if (skel != null)
            {
                skel.playerID = playerID;
            }
        }

        #region Captury Delegates
        /// <summary>
        /// Called when a new captury skeleton is found
        /// </summary>
        /// <param name="skeleton"></param>
        private void OnSkeletonFound(CapturySkeleton skeleton)
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
        private void OnSkeletonLost(CapturySkeleton skeleton)
        {
            Debug.Log("CapturyAvatarManager lost skeleton with id " + skeleton.id + " and name " + skeleton.name);
            lock (lostSkeletons)
            {
                lostSkeletons.Add(skeleton);
            }
            // clear the assignment between local player and the skelton if it's lost
            if (IsLocalPlayer(skeleton))
            {
                ClearPlayerAssignment();
            }
        }

        /// <summary>
        /// Called when a Captury AR tags (markers) are detected
        /// </summary>
        /// <param name="tags"></param>
        private void OnARTagsDetected(ARTag[] tags)
        {
            lock (trackedARTags)
            {
                List<ARTag> lostTags = new List<ARTag>();

                // find lost AR tags and delete them
                foreach (var prevTag in trackedARTags)
                {
                    ARTag newTag = tags.Single(item => item.id == prevTag.id);
                    if (newTag == null)
                    {
                        // tag is no longer tracked
                        lostTags.Add(prevTag);
                        if (capturyConfig.debugARTags)
                        {
                            lock (trackedARTagGameObjects)
                            {
                                GameObject gO = trackedARTagGameObjects[prevTag];
                                DestroyImmediate(gO);
                                trackedARTagGameObjects.Remove(prevTag);
                            }
                        }
                    }
                }
                trackedARTags.RemoveAll(item => lostTags.Contains(item));

                // find new AR tags / update existing
                foreach (var tag in tags)
                {
                    int tagIndexInPrevTagsArray = trackedARTags.FindIndex(item => item.id == tag.id);
                    if (tagIndexInPrevTagsArray != -1)
                    {
                        // update AR tag
                        trackedARTags[tagIndexInPrevTagsArray] = tag;
                        if (capturyConfig.debugARTags)
                        {
                            lock (trackedARTagGameObjects)
                            {
                                // update pose
                                GameObject gO = trackedARTagGameObjects.Single(item => item.Key.id == tag.id).Value;
                                if (gO != null)
                                {
                                    gO.transform.position = tag.translation;
                                    gO.transform.rotation = tag.rotation;
                                }
                            }
                        }
                    }
                    else
                    {
                        // new AR tag found
                        trackedARTags.Add(tag);
                        if (capturyConfig.debugARTags)
                        {
                            lock (trackedARTagGameObjects)
                            {
                                // can be optimized with object pool
                                GameObject gO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                gO.name = "arTag " + tag.id;
                                gO.transform.localScale = new Vector3(0.1f, 0.1f, 0.01f);
                                trackedARTagGameObjects.Add(tag, gO);
                            }
                        }
                    }
                }

                arTagsUpdated = true;
            }
        }
        #endregion

        #region Avatars
        /// <summary>
        /// Returns the avatar prefab with the given avatarID from <see cref="localAvatarPrefabs"/> or <see cref="remoteAvatarPrefabs"/> depending on isLocal.
        /// If avatarID is invalid, <see cref="defaultAvatar"/> will be returned
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="isLocal"></param>
        /// <returns>Avatar prefab</returns>
        private GameObject GetAvatarPrefab(int avatarID, bool isLocal)
        {
            GameObject[] avatars;
            if (isLocal)
            {
                avatars = localAvatarPrefabs;
            }
            else
            {
                avatars = remoteAvatarPrefabs;
            }
            if (avatarID < 0 || avatarID > avatars.Length)
            {
                Debug.LogError("Trying to get avatar for invalid id " + avatarID + ". returning defaultAvatar!");
                return defaultAvatar;
            }
            return avatars[avatarID];
        }

        /// <summary>
        /// Instantiates and sets the given avatarPrefab for the CapturySkeleton
        /// </summary>
        /// <param name="skel"></param>
        /// <param name="avatarPrefab"></param>
        private void SetAvatar(CapturySkeleton skel, GameObject avatarPrefab)
        {
            GameObject avatar = Instantiate(avatarPrefab);
            DontDestroyOnLoad(avatar);
            avatar.SetActive(true);
            if(skel.mesh != null)
            {
                // destory old avatar
                DestroyImmediate(skel.mesh);
            }
            skel.mesh = avatar;
        }

        /// <summary>
        /// Instantiates default avatars for the given list of skeletons
        /// </summary>
        /// <param name="skeletons"></param>
        private void InstantiateDefaultAvatars(List<CapturySkeleton> skeletons)
        {
            foreach (CapturySkeleton skel in skeletons)
            {
                Debug.Log("Instantiating avatar for skeleton with id " + skel.id + " and name " + skel.name);
                SetAvatar(skel, defaultAvatar);
                trackedSkeletons.Add(skel);
            }
            skeletons.Clear();
        }

        /// <summary>
        /// Destorys avatars for the given list of skeletons
        /// </summary>
        private void DestroyAvatars(List<CapturySkeleton> skeltons)
        {
            foreach (CapturySkeleton skel in skeltons)
            {
                Debug.Log("Destroying avatar for skeleton with id " + skel.id + " and name " + skel.name);
                DestroyImmediate(skel.mesh);
                skel.mesh = null;
                trackedSkeletons.Remove(skel);
            }
            skeltons.Clear();
        }
        #endregion

        #region CapturySkeleton To Avatar Handling
        /// <summary>
        /// Assigns the local player to the given <see cref="CapturySkeleton"/>.
        /// </summary>
        /// <param name="skeleton"></param>
        private void AssignPlayerToSkeleton(CapturySkeleton skeleton)
        {
            // instantiate the local player avatar
            GameObject avatarPrefab = GetAvatarPrefab(capturyConfig.avatarID, true);
            SetAvatar(skeleton, avatarPrefab);
            playerSkeleton = skeleton;

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

            if (head != null)
            {
                transformFollower.Target = head;
            }
            else
            {
                Debug.Log("Cannot find head on target avatar with name " + AVATAR_HEAD_TRANSFORM_NAME);
            }

            if (PlayerAssignmentChanged != null)
            {
                PlayerAssignmentChanged(playerSkeleton.id, true);
            }
            Debug.Log("Assigned local player to skeleton with name " + skeleton.name + " and id " + skeleton.id);
        }

        /// <summary>
        /// Clears the assignment of the local player with an skeleton
        /// </summary>
        private void ClearPlayerAssignment()
        {
            if(playerSkeleton == null)
            {
                Debug.LogError("Trying to clear player assignment, but playerSkeleton == null");
                return;
            }

            if (PlayerAssignmentChanged != null)
            {
                PlayerAssignmentChanged(playerSkeleton.id, false);
            }

            playerSkeleton.playerID = -1;
            capturyLeapIntegration.setTargetModel(null, null, -1);
            transformFollower.Target = null;
            playerSkeleton = null;
        }
        #endregion Skeleton To Player Assignment

        #region CapturySkeleton Helper
        /// <summary>
        /// Returns the head <see cref="CapturySkeletonJoint"/> of the given <see cref="CapturySkeleton"/> by looking for <see cref="CapturyNetworkPlugin.HeadJointName"/>
        /// </summary>
        /// <param name="skel"></param>
        /// <returns></returns>
        private CapturySkeletonJoint GetHeadJoint(CapturySkeleton skel)
        {
            return skel.joints.Single(item => item.name == CapturyNetworkPlugin.HeadJointName);
        }

        /// <summary>
        /// Return true if the given skeleton is the local player skeleton
        /// </summary>
        /// <param name="skeleton"></param>
        /// <returns></returns>
        private bool IsLocalPlayer(CapturySkeleton skeleton)
        {
            return skeleton.Equals(playerSkeleton);
        }
        #endregion CapturySkeleton Helper

        #region AR Tag Helper
        /// <summary>
        /// Get all <see cref="ARTag"/>s in the given list which are assigned to the headset. This is defined in <see cref="capturyConfig"/>.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns>List of AR Tags which are assigned to the headset</returns>
        private List<ARTag> GetHeadsetARTags(List<ARTag> tags)
        {
            return tags.Where(tag => capturyConfig.headsetARTags.Any(headsetTag => headsetTag.id == tag.id)).ToList();
        }

        /// <summary>
        /// Clears <see cref="trackedARTags"/> and <see cref="trackedARTagGameObjects"/>
        /// </summary>
        private void ClearTrackedARTags()
        {
            trackedARTags.Clear();
            if (capturyConfig.debugARTags)
            {
                foreach (var tag in trackedARTagGameObjects)
                {
                    DestroyImmediate(tag.Value);
                }
                trackedARTagGameObjects.Clear();
            }
        }
        #endregion AR Tags

        #region AR Tag To CapturySkeleton Handling
        /// <summary>
        /// Looks for <see cref="ARTag"/>s in arTags which are assigned to the player/headset.
        /// The offset from the found tags to the given <see cref="CapturySkeleton"/> head joint will be saved to <see cref="capturyConfig"/>.
        /// </summary>
        /// <param name="arTags">List of <see cref="ARTag"/>s to look for player/headset tags.</param>
        /// <param name="skel"><see cref="CapturySkeleton"/> which head joint will be used to calculate the offsets.</param>
        private void CalibrateHeadsetARTags(List<ARTag> arTags, CapturySkeleton skel)
        {
            if (arTags != null && skel != null)
            {
                Debug.LogFormat("Head rotation: {0}, arTagRotation: {1}", GetHeadJoint(skel).transform.rotation.eulerAngles, arTags[0].rotation.eulerAngles);
                List<ARTag> headsetTags = GetHeadsetARTags(arTags);
                CapturySkeletonJoint headJoint = GetHeadJoint(skel);
                if (headJoint != null)
                {
                    foreach (var tag in headsetTags)
                    {
                        Vector3 posOffset = tag.translation - headJoint.transform.position;
                        Vector3 headDirection = headJoint.transform.rotation * Vector3.forward;
                        Vector3 tagDirection = tag.rotation * Vector3.forward;
                        Quaternion rotOffset = Quaternion.FromToRotation(headDirection, tagDirection);

                        CapturyConfig.HeadsetARTag headsetARTag = capturyConfig.headsetARTags.Single(t => t.id == tag.id);
                        if (headsetARTag != null)
                        {
                            headsetARTag.offsetPosX = posOffset.x;
                            headsetARTag.offsetPosY = posOffset.y;
                            headsetARTag.offsetPosZ = posOffset.z;
                            headsetARTag.offsetRotX = rotOffset.eulerAngles.x;
                            headsetARTag.offsetRotY = rotOffset.eulerAngles.y;
                            headsetARTag.offsetRotZ = rotOffset.eulerAngles.z;
                            Debug.LogFormat("Set new offset of headsetARTag {0}, pos:{1}, rot{2}", headsetARTag.id, posOffset, rotOffset.eulerAngles);
                        }
                        else
                        {
                            Debug.LogErrorFormat("headsetARTag == null for tag id {0}", tag.id);
                        }
                    }
                }
                else
                {
                    Debug.LogError("Can't calibrate since headJoint == null");
                }
            }
        }

        /// <summary>
        /// Checks if the <see cref="ARTag"/> is attached to the <see cref="CapturySkeleton"/> by comparing their positions
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="skel"></param>
        /// <returns></returns>
        private bool IsAttachedToSkeleton(ARTag tag, CapturySkeleton skel)
        {
            CapturySkeletonJoint headJoint = GetHeadJoint(skel);
            if (headJoint != null && headJoint.transform != null)
            {
                if (Vector3.Distance(tag.translation, headJoint.transform.position) < capturyConfig.arTagSkeletonThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the given <see cref="ARTag"/> is attached to a <see cref="CapturySkeleton"/> in <see cref="trackedSkeletons"/>
        /// </summary>
        /// <param name="tag"></param>
        /// <returns>The skeleton which tag is attached to. null if there's none.</returns>
        private CapturySkeleton GetAttachedSkeleton(ARTag tag)
        {
            foreach (var skel in trackedSkeletons)
            {
                if (IsAttachedToSkeleton(tag, skel))
                {
                    return skel;
                }
            }
            return null;
        }

        private void TryARTagToSkeletonAssignemnt(List<ARTag> arTags)
        {
            // get the AR tags which are attached to the players headset
            List<ARTag> trackedHeadsetTags = GetHeadsetARTags(arTags);
            foreach (var tag in trackedHeadsetTags)
            {
                CapturySkeleton skel = GetAttachedSkeleton(tag);
                if (skel != null)
                {
                    if (skel.playerID == -1)
                    {
                        AssignPlayerToSkeleton(skel);
                    }
                    else
                    {
                        Debug.Log("Skeleton " + skel.id + " is already assigned to player " + skel.playerID);
                    }
                }
            }
        }
        #endregion AR Tag CapturySkeleton
    }
}
