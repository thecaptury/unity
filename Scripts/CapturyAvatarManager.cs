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
        [Tooltip("Avatar prefabs for local players (without head). userAvatarID is set in " + CAPTURY_CONFIG_FILE_PATH)]
        private GameObject[] localAvatarPrefabs = new GameObject[] { };

        [SerializeField]
        [Tooltip("Avatar prefabs for remote players (with head). userAvatarID is set in " + CAPTURY_CONFIG_FILE_PATH)]
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
        /// The captury config will be loaded from <see cref="CAPTURY_CONFIG_FILE_PATH"/>
        /// </summary>
        private CapturyConfig capturyConfig;

        /// <summary>
        /// Path of the captury config file
        /// </summary>
        private const string CAPTURY_CONFIG_FILE_PATH = "capturyconfig.json";

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
        /// Can be called from a multiplayer manager to set the skeletons playerID
        /// </summary>
        /// <param name="skeletonID">Captury Skeleton id</param>
        /// <param name="playerID">Networking Player id</param>
        public void SetSkeletonPlayerID(int skeletonID, int playerID)
        {
            CapturySkeleton skel = trackedSkeletons.First(s => s.id == skeletonID);
            if (skel != null)
            {
                skel.playerID = playerID;
            }
        }

        private void Start()
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

        private void Update()
        {
            lock (newSkeletons)
            {
                InstantiateDefaultAvatars(newSkeletons);
            }
            lock (lostSkeletons)
            {
                DestroyAvatars(lostSkeletons);
            }
        }

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
        private void OnARTagsDetected(CapturyARTag[] tags)
        {
            // check player/skeleton assignment with AR tags
            if (playerSkeleton == null)
            {
                // get the AR tags which are assigned to the player 
                List<CapturyARTag> playerARTags = tags.Where(t => capturyConfig.arTagIDs.Contains(t.id)).ToList();
                foreach (var tag in playerARTags)
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
        }

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
            } else
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
            lock (trackedSkeletons)
            {
                foreach (CapturySkeleton skel in skeletons)
                {
                    Debug.Log("Instantiating avatar for skeleton with id " + skel.id + " and name " + skel.name);
                    SetAvatar(skel, defaultAvatar);
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
                    DestroyImmediate(skel.mesh);
                    skel.mesh = null;
                    trackedSkeletons.Remove(skel);
                }
                skeltons.Clear();
            }
        }

        /// <summary>
        /// Checks if the <see cref="CapturyARTag"/> is attached to the <see cref="CapturySkeleton"/> by comparing their positions
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="skel"></param>
        /// <returns></returns>
        private bool IsAttachedToSkeleton(CapturyARTag tag, CapturySkeleton skel)
        {
            // TODO Nils: Tag attachment logic
            float threshold = 0.5f;
            Vector3 tP = new Vector3(tag.ox, tag.oy, tag.oz);
            foreach(var joint in skel.joints)
            {
                if(joint != null && joint.transform != null)
                {
                    // TODO check if local / global position
                    if (Vector3.Distance(tP, joint.transform.position) < threshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the given <see cref="CapturyARTag"/> is attached to a <see cref="CapturySkeleton"/> in <see cref="trackedSkeletons"/>
        /// </summary>
        /// <param name="tag"></param>
        /// <returns>The skeleton which tag is attached to. null if there's none.</returns>
        private CapturySkeleton GetAttachedSkeleton(CapturyARTag tag)
        {
            foreach(var skel in trackedSkeletons)
            {
                if (IsAttachedToSkeleton(tag, skel))
                {
                    return skel;
                }
            }
            return null;
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

            if (head != null)
            {
                transformFollower.Target = head;
            }
            else
            {
                Debug.Log("Cannot find head on target avatar with name " + AVATAR_HEAD_TRANSFORM_NAME);
            }

            // instantiate the local player avatar
            GameObject avatarPrefab = GetAvatarPrefab(capturyConfig.avatarID, true);
            SetAvatar(skeleton, avatarPrefab);
            playerSkeleton = skeleton;
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
            if (PlayerAssignmentChanged != null)
            {
                PlayerAssignmentChanged(playerSkeleton.id, false);
            }
            playerSkeleton.playerID = -1;
            capturyLeapIntegration.setTargetModel(null, null, -1);
            transformFollower.Target = null;
            playerSkeleton = null;
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
                string json = File.ReadAllText(CAPTURY_CONFIG_FILE_PATH, System.Text.Encoding.ASCII);
                capturyConfig = JsonUtility.FromJson<CapturyConfig>(json);
                if(capturyConfig == null)
                {
                    Debug.LogError("Couldn't parse json from " + CAPTURY_CONFIG_FILE_PATH + " to CapturyConfig");
                }
            }
            else
            {
                Debug.LogError("No Captury config file found at " + CAPTURY_CONFIG_FILE_PATH);
            }
        }
    }
}
