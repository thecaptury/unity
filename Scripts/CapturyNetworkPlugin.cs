using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using UnityEngine;

namespace Captury
{
    //=================================
    // define captury class structures
    //=================================
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyJoint
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] name;
        public int parent;
        public float ox, oy, oz;
        public float rx, ry, rz;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyActor
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] name;
        public int id;
        public int numJoints;
        public IntPtr joints;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyPose
    {
        public int actor;
        public long timestamp;
        public int numValues;
        public IntPtr values;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyARTag
    {
        public int id;
        public float ox, oy, oz; // position
        public float nx, ny, nz; // normal
	}

    [StructLayout(LayoutKind.Sequential)]
    public struct CapturyImage
    {
        public int width;
        public int height;
        public int camera;
        public ulong timestamp;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyTransform
    {
        public float rx; // rotation euler angles
        public float ry;
        public float rz;
        public float tx; // translation
        public float ty;
        public float tz;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CapturyCamera
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] name;
        public int id;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float orientationX;
        public float orientationY;
        public float orientationZ;
        public float sensorWidth;   // in mm
        public float sensorHeight;  // in mm
        public float focalLength;   // in mm
        public float lensCenterX;   // in mm
        public float lensCenterY;   // in mm
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] distortionModel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public float distortion;

        // the following can be computed from the above values and are provided for convenience only
        // the matrices are stored column wise:
        // 0  3  6  9
        // 1  4  7 10
        // 2  5  8 11
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        float extrinsic;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        float intrinsic;
    };

    //==========================================
    // internal structures that are more easy to use
    //==========================================
    [Serializable]
    public class CapturySkeletonJoint
    {
        public string name;
        public int parent;
        public Vector3 offset;
        public Vector3 orientation;
        public Transform transform;
    }

    [Serializable]
    public class CapturySkeleton
    {
        public string name;
        public int id;
        public CapturySkeletonJoint[] joints;

        private GameObject reference;
        public GameObject mesh // reference to game object that is animated
        {
            get { return reference; }
            set
            {
                reference = value;
                if (reference == null)
                {
                    foreach (CapturySkeletonJoint j in joints)
                        j.transform = null;
                    return;
                }
                foreach (CapturySkeletonJoint j in joints)
                {
                    // check if the joint name matches a reference transform and assign it
                    ArrayList children = reference.transform.GetAllChildren();
                    foreach (Transform tra in children)
                    {
                        if (tra.name.EndsWith(j.name))
                        {
                            j.transform = tra;
                            continue;
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class CapturyMarkerTransform
    {
        public Quaternion rotation;
        public Vector3 translation;
        public UInt64 timestamp;
        public float bestAccuracy;
        public bool consumed;
    }

    //====================
    // the network plugin
    //====================
    [RequireComponent(typeof(CapturyOriginManager))]
    public class CapturyNetworkPlugin : MonoBehaviour
    {
        //=============================================
        // import the functions from RemoteCaptury dll
        //=============================================
        [DllImport("RemoteCaptury")]
        private static extern int Captury_connect(string ip, ushort port);
        [DllImport("RemoteCaptury")]
        private static extern int Captury_disconnect();
        [DllImport("RemoteCaptury")]
        private static extern int Captury_getActors(out IntPtr actorData);
        [DllImport("RemoteCaptury")]
        private static extern int Captury_startStreaming(int what);
        [DllImport("RemoteCaptury")]
        private static extern int Captury_stopStreaming();
        [DllImport("RemoteCaptury")]
        private static extern IntPtr Captury_getCurrentPose(IntPtr actor);
        [DllImport("RemoteCaptury")]
        private static extern void Captury_freePose(IntPtr pose);
        [DllImport("RemoteCaptury")]
        private static extern void Captury_requestTexture(IntPtr actor);
        [DllImport("RemoteCaptury")]
        private static extern IntPtr Captury_getTexture(IntPtr actor);
        [DllImport("RemoteCaptury")]
        private static extern void Captury_freeImage(IntPtr image);
        [DllImport("RemoteCaptury")]
        private static extern int Captury_setRotationConstraint(int actorId, int jointIndex, IntPtr rotation, UInt64 timestamp, float weight);
        [DllImport("RemoteCaptury")]
        private static extern UInt64 Captury_getMarkerTransform(IntPtr actor, int jointIndex, IntPtr transform);
        [DllImport("RemoteCaptury")]
        private static extern UInt64 Captury_synchronizeTime();
        [DllImport("RemoteCaptury")]
        private static extern UInt64 Captury_getTime();
        [DllImport("RemoteCaptury")]
        private static extern Int64 Captury_getTimeOffset();
        [DllImport("RemoteCaptury")]
        private static extern IntPtr Captury_getLastErrorMessage();
        [DllImport("RemoteCaptury")]
        private static extern void Captury_freeErrorMessage(IntPtr msg);
        [DllImport("RemoteCaptury")]
        private static extern int Captury_getCameras(out IntPtr cameras);
        [DllImport("RemoteCaptury")]
		private static extern IntPtr Captury_getCurrentARTags();
        [DllImport("RemoteCaptury")]
        private static extern void Captury_freeARTags(IntPtr arTags);

		// config settings
        public string host = "127.0.0.1";
        public ushort port = 2101;
        public float scaleFactor = 0.001f; // mm to m
        public int actorCheckTimeout = 500; // in ms
		public bool streamARTags = false;

        // Events
        public delegate void FoundSkeletonDelegate(CapturySkeleton skeleton);
        public event FoundSkeletonDelegate foundSkeleton;
        public delegate void LostSkeletonDelegate(CapturySkeleton skeleton);
        public event LostSkeletonDelegate lostSkeleton;
        public delegate void CamerasChangedDelegate(Vector3[] positions, Quaternion[] rotations);
        public event CamerasChangedDelegate CamerasChanged;
        public delegate void DetectedARTagsDelegate(CapturyARTag[] artags);
        public event DetectedARTagsDelegate detectedARTags;

        public Vector3[] cameraPositions;
        public Quaternion[] cameraOrientations;
		
		public CapturyARTag[] arTags = new CapturyARTag[0];

        /// <summary>
        /// Reference to <see cref="capturyOriginManager"/> which handles the origin of the coordinate system
        /// </summary>
        private CapturyOriginManager capturyOriginManager;

        /// <summary>
        /// Reference to the current <see cref="CapturyOrigin"/> in the scene which defines the origin of the coordinate system of all avatars 
        /// </summary>
        private CapturyOrigin capturyOrigin;

        private string headJointName = "Head";

        // threading data for communication with server
        private Thread communicationThread;
        private Mutex communicationMutex = new Mutex();
        private bool communicationFinished = false;

        // internal variables
        private bool isConnected = false;
        private bool isSetup = false;
        private bool receivedFirstCameras = false;

        // skeleton data from Captury
        private Dictionary<int, IntPtr> actorPointers = new Dictionary<int, IntPtr>();
        private Dictionary<int, int> actorFound = new Dictionary<int, int>();
        private Dictionary<int, CapturySkeleton> skeletons = new Dictionary<int, CapturySkeleton>();
        private Dictionary<int, CapturyMarkerTransform> headTransforms = new Dictionary<int, CapturyMarkerTransform>();
        private Dictionary<string, int> jointsWithConstraints = new Dictionary<string, int>();

        void Awake()
        {
            // asign to CapturyOrigin change 
            capturyOriginManager = GetComponent<CapturyOriginManager>();
            capturyOriginManager.CapturyOriginChanged += OnCapturyOriginChanged;
        }

        //=============================
        // this is run once at startup
        //=============================
        void Start()
        {
            // start the connection thread
            communicationThread = new Thread(lookForActors);
            communicationThread.Start();
        }

        //==========================
        // this is run once at exit
        //==========================
        void OnDisable()
        {
            communicationFinished = true;
            communicationThread.Join();
        }

        //============================
        // this is run once per frame
        //============================
        void Update()
        {
            // only perform if we are actually connected
            if (!isConnected)
                return;

            // make sure we lock access before doing anything
            //			Debug.Log ("Starting pose update...");
            communicationMutex.WaitOne();

            // fetch current pose for all skeletons
            foreach (KeyValuePair<int, CapturySkeleton> kvp in skeletons)
            {
                // get the actor id
                int actorID = kvp.Key;

                // check if the actor is mapped to something, if not, ignore
                if (skeletons[actorID].mesh == null)
                    continue;

                // get pointer to pose
                IntPtr poseData = Captury_getCurrentPose(actorPointers[actorID]);

                // check if we actually got data, if not, continue
                if (poseData == IntPtr.Zero)
                {
                    // something went wrong, get error message
                    IntPtr msg = Captury_getLastErrorMessage();
                    string errmsg = Marshal.PtrToStringAnsi(msg);
                    Debug.Log("Stream error: " + errmsg);
                    Captury_freeErrorMessage(msg);
                } else {

					//Debug.Log("received pose for " + actorID);

					// convert the pose
					CapturyPose pose = (CapturyPose)Marshal.PtrToStructure(poseData, typeof(CapturyPose));

					// get the data into a float array
					float[] values = new float[pose.numValues * 6];
					Marshal.Copy(pose.values, values, 0, pose.numValues * 6);

					// now loop over all joints
					Vector3 pos = new Vector3();
					Vector3 rot = new Vector3();
                    
                    // set origin offset based on CapturyOrigin, if existent. Otherwise keep world origin (0,0,0)
                    Vector3 offsetToOrigin = Vector3.zero;
                    if (capturyOrigin != null)
                    {
                        offsetToOrigin = capturyOrigin.OffsetToWorldOrigin;
                    }

					for (int jointID = 0; jointID < skeletons[actorID].joints.Length; jointID++)
					{
						// ignore any joints that do not map to a transform
						if (skeletons[actorID].joints[jointID].transform == null)
							continue;

						// set offset and rotation
						int baseIndex = jointID * 6;
						pos.Set(values[baseIndex + 0], values[baseIndex + 1], values[baseIndex + 2]);
						rot.Set(values[baseIndex + 3], values[baseIndex + 4], values[baseIndex + 5]);

                        skeletons[actorID].joints[jointID].transform.position = ConvertPosition(pos) + offsetToOrigin;
						skeletons[actorID].joints[jointID].transform.rotation = ConvertRotation(rot);
					}

					// finally, free the pose data again, as we are finished
					Captury_freePose(poseData);
				}
            }

			// get artags
			IntPtr arTagData = Captury_getCurrentARTags();

			// check if we actually got data, if not, continue
			if (arTagData == IntPtr.Zero)
			{
				// something went wrong, get error message
				IntPtr msg = Captury_getLastErrorMessage();
				string errmsg = Marshal.PtrToStringAnsi(msg);
				Debug.Log("Stream error: " + errmsg);
				Captury_freeErrorMessage(msg);
			} else {
			
				IntPtr at = arTagData;
				int num;
				for (num = 0; num < 100; ++num) {
					CapturyARTag arTag = (CapturyARTag)Marshal.PtrToStructure(at, typeof(CapturyARTag));
					if (arTag.id == -1)
						break;
					Array.Resize(ref arTags, num+1);
					arTags[num] = arTag;
					at = new IntPtr(at.ToInt64() + Marshal.SizeOf(typeof(CapturyARTag)));
				}
				if (num != 0 && detectedARTags != null)
					detectedARTags(arTags);
				else
					Array.Resize(ref arTags, 0);

				Debug.Log("found artags: " + num);

				Captury_freeARTags(arTagData);
			}

            communicationMutex.ReleaseMutex();
        }

        //================================================
        // This function continously looks for new actors
        // It runs in a separate thread
        //================================================
        void lookForActors()
        {
            while (!communicationFinished)
            {
                // wait for actorCheckTimeout ms before continuing
                //			Debug.Log ("Going to sleep...");
                Thread.Sleep(actorCheckTimeout);
                //			Debug.Log ("Waking up...");

                // now look for new data

                // try to connect to captury live
                if (!isSetup)
                {
                    if (Captury_connect(host, port) == 1 && Captury_synchronizeTime() != 0)
                    {
                        isSetup = true;
                        Debug.Log("Successfully opened port to Captury Live");
                        Debug.Log("The time difference is " + Captury_getTimeOffset());
                    }
                    else
                        Debug.Log(String.Format("Unable to connect to Captury Live at {0}:{1} ", host, port));

					IntPtr cameraData = IntPtr.Zero;
                    int numCameras = Captury_getCameras(out cameraData);
					if (numCameras > 0 && cameraData != IntPtr.Zero)
                    {
                        cameraPositions = new Vector3[numCameras];
                        cameraOrientations = new Quaternion[numCameras];
                        int szStruct = Marshal.SizeOf(typeof(CapturyCamera)) + 192; // this offset is here to take care of implicit padding
                        for (uint i = 0; i < numCameras; i++)
                        {
                            CapturyCamera camera = new CapturyCamera();
                            camera = (CapturyCamera)Marshal.PtrToStructure(new IntPtr(cameraData.ToInt64() + (szStruct * i)), typeof(CapturyCamera));
                            // Debug.Log("camera " + camera.id.ToString("x") + " (" + camera.positionX + ", "  + camera.positionY + ","  + camera.positionZ + ") (" +
                            // camera.orientationX + ", "  + camera.orientationY + ","  + camera.orientationZ + ") ss: (" + camera.sensorWidth + ", " + camera.sensorHeight + ") fl:" +
                            // camera.focalLength + " lc: (" + camera.lensCenterX + ", " + camera.lensCenterY + ") ");
                            cameraPositions[i] = ConvertPosition(new Vector3(camera.positionX, camera.positionY, camera.positionZ));
                            cameraOrientations[i] = ConvertRotation(new Vector3(camera.orientationX, camera.orientationY, camera.orientationZ));
                        }
                        // Fire cameras changed event
                        if (CamerasChanged != null)
                        {
                            CamerasChanged(cameraPositions, cameraOrientations);
                        }
                    }
                }
                if (isSetup)
                {
					// grab actors
                    IntPtr actorData = IntPtr.Zero;
                    int numActors = Captury_getActors(out actorData);
                    if (numActors > 0 && actorData != IntPtr.Zero)
                    {
                        Debug.Log(String.Format("Received {0} actors", numActors));

                        // create actor struct
                        int szStruct = Marshal.SizeOf(typeof(CapturyActor)) + 16; // implicit padding
                        for (uint i = 0; i < numActors; i++)
                        {
                            // get an actor
                            CapturyActor actor = new CapturyActor();
                            actor = (CapturyActor)Marshal.PtrToStructure(new IntPtr(actorData.ToInt64() + (szStruct * i)), typeof(CapturyActor));

                            // check if we already have it in our dictionary
                            if (skeletons.ContainsKey(actor.id)) // access to actors does not need to be locked here because the other thread is read-only
                            {
                                communicationMutex.ReleaseMutex();
                                actorFound[actor.id] = 5;
                                continue;
                            }
                            Debug.Log("Found new actor " + actor.id);

                            // no? we need to convert it
                            IntPtr actorPointer = new IntPtr(actorData.ToInt64() + (szStruct * i));
                            CapturySkeleton skeleton = new CapturySkeleton();
                            ConvertActor(actor, ref skeleton);

                            if (foundSkeleton != null)
                            {
                                foundSkeleton(skeleton);
                            }

                            //  and add it to the list of actors we are processing, making sure this is secured by the mutex
                            communicationMutex.WaitOne();
                            actorPointers.Add(actor.id, actorPointer);
                            skeletons.Add(actor.id, skeleton);
                            actorFound.Add(actor.id, 5);
                            communicationMutex.ReleaseMutex();
                        }
                    }

					if (!isConnected)
					{
						if (Captury_startStreaming(streamARTags ? 5 : 1) == 1) {
							Debug.Log("Successfully started streaming data");
							isConnected = true;
						} else
							Debug.LogWarning("failed to start streaming");
					}

                    // reduce the actor countdown by one for each actor
                    int[] keys = new int[actorFound.Keys.Count];
                    actorFound.Keys.CopyTo(keys, 0);
                    foreach (int key in keys)
                        actorFound[key]--;
                }

                // remove all actors that were not found in the past few actor checks
                //			Debug.Log ("Updating actor structure");
                communicationMutex.WaitOne();
                List<int> unusedKeys = new List<int>();
                foreach (KeyValuePair<int, int> kvp in actorFound)
                {
                    if (kvp.Value <= 0)
                    {
                        if (lostSkeleton != null)
                        {
                            Debug.Log("lost skeleton. telling all my friends.");
                            lostSkeleton(skeletons[kvp.Key]);
                        }

                        // remove actor
                        actorPointers.Remove(kvp.Key);
                        skeletons.Remove(kvp.Key);
                        unusedKeys.Add(kvp.Key);
                    }
                }
                communicationMutex.ReleaseMutex();
                //			Debug.Log ("Updating actor structure done");

                // clear out actorfound structure
                foreach (int key in unusedKeys)
                    actorFound.Remove(key);

                // look for current transformation of bones with markers - the head
                foreach (KeyValuePair<int, IntPtr> kvp in actorPointers)
                {
                    int id = kvp.Key;

                    // find the index of the head joint
                    int headJointIndex = -1;
                    for (int i = 0; i < skeletons[id].joints.Length; ++i)
                    {
                        if (skeletons[id].joints[i].name.EndsWith(headJointName))
                        {
                            headJointIndex = i;
                            break;
                        }
                    }
                    if (headJointIndex == -1)
                    {
                        Debug.Log("no head joint for skeleton " + id);
                        continue;
                    }

                    // get the transform and store it in headTransforms
                    IntPtr trafo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CapturyTransform)));
                    UInt64 timestamp = Captury_getMarkerTransform(kvp.Value, headJointIndex, trafo);
                    // is there a constraint for this joint that is not older than 500ms?
                    if (timestamp != 0)
                    {
                        CapturyTransform t = (CapturyTransform)Marshal.PtrToStructure(trafo, typeof(CapturyTransform));
                        communicationMutex.WaitOne();
                        if (headTransforms.ContainsKey(id))
                        {
                            // this is a new transform. the other thread should have a look at it.
                            if (headTransforms[id].timestamp < timestamp)
                                headTransforms[id].consumed = false;
                        }
                        else
                        {
                            headTransforms[id] = new CapturyMarkerTransform();
                            headTransforms[id].bestAccuracy = 0.95f;
                            // if the new transform is actually already old mark it as old directly
                            if (timestamp > Captury_getTime() - 500000)
                                headTransforms[id].consumed = false;
                            else
                                headTransforms[id].consumed = true;
                        }
                        headTransforms[id].rotation = ConvertRotation(new Vector3(t.rx * 180 / (float)Math.PI, t.ry * 180 / (float)Math.PI, t.rz * 180 / (float)Math.PI));
                        headTransforms[id].translation = ConvertPosition(new Vector3(t.tx, t.ty, t.tz));
                        headTransforms[id].timestamp = timestamp;
                        communicationMutex.ReleaseMutex();
                        //                    Debug.Log(string.Format("transform for actor.joint {0}.{1} is good, really t {2}, delta now {3}", id, headJointIndex, timestamp, Captury_getTime() - timestamp));
                    }
                    else
                    {
                        communicationMutex.WaitOne();
                        headTransforms.Remove(id);
                        communicationMutex.ReleaseMutex();
                    }
                    Marshal.FreeHGlobal(trafo);
                }
            }

            Debug.Log("Disconnecting");
            // make sure we disconnect
            Captury_disconnect();
            isSetup = false;
            isConnected = false;
        }

        public void setRotationConstraint(int id, string jointName, Transform t)
        {
            if (skeletons.ContainsKey(id))
            {
                Debug.Log("Cannot set rotation for " + jointName + ": no skeleton with id " + id);
                return;
            }
            else
                Debug.Log("Set " + jointName + "-rotation to " + t);
            communicationMutex.WaitOne();
            CapturySkeleton skel = skeletons[id];
            communicationMutex.ReleaseMutex();

            int index;
            if (jointsWithConstraints.ContainsKey(jointName))
                index = jointsWithConstraints[jointName];
            else
            {
                index = 0;
                foreach (CapturySkeletonJoint j in skel.joints)
                {
                    if (j.name == jointName)
                        break;
                    ++index;
                }
                if (index == skel.joints.Length)
                {
                    Debug.Log("Cannot set constraint for joint " + jointName + ": no such joint");
                    return;
                }
            }

            //        CapturySkeletonJoint jnt = skel.joints[index];
            Vector3 euler = ConvertToEulerAngles(ConvertRotationToLive(t.rotation));
            IntPtr rotation = Marshal.AllocHGlobal(12);
            Marshal.StructureToPtr(euler, rotation, false);
            Captury_setRotationConstraint(id, index, rotation, Captury_getTime(), 1.0f);
            Marshal.FreeHGlobal(rotation);
        }

        /// <summary>
        /// Called when <see cref="CapturyOrigin"/> changes and sets it as local variable.
        /// </summary>
        /// <param name="newCapturyOrigin"></param>
        public void OnCapturyOriginChanged(CapturyOrigin capturyOrigin)
        {
            this.capturyOrigin = capturyOrigin;
        }

        //===============================================
        // helper function to map an actor to a skeleton
        //===============================================
        private void ConvertActor(CapturyActor actor, ref CapturySkeleton skel)
        {
            if (skel == null)
            {
                Debug.Log("Null skeleton reference");
                return;
            }

            // copy data over
            skel.name = System.Text.Encoding.UTF8.GetString(actor.name);
            skel.id = actor.id;

            // create joints
            int szStruct = Marshal.SizeOf(typeof(CapturyJoint));
            skel.joints = new CapturySkeletonJoint[actor.numJoints];
            for (uint i = 0; i < actor.numJoints; i++)
            {
                // marshall the joints into a new joint struct
                CapturyJoint joint = new CapturyJoint();
                joint = (CapturyJoint)Marshal.PtrToStructure(new IntPtr(actor.joints.ToInt64() + (szStruct * i)), typeof(CapturyJoint));

                skel.joints[i] = new CapturySkeletonJoint();
                skel.joints[i].name = System.Text.Encoding.ASCII.GetString(joint.name);
                int jpos = skel.joints[i].name.IndexOf("\0");
                skel.joints[i].name = skel.joints[i].name.Substring(0, jpos);
                skel.joints[i].offset.Set(joint.ox, joint.oy, joint.oz);
                skel.joints[i].orientation.Set(joint.rx, joint.ry, joint.rz);

                //Debug.Log ("Got joint " + skel.joints[i].name + " at " + joint.ox + joint.oy + joint.oz);
            }
        }

        //========================================================================================================
        // Helper function to convert a position from a right-handed to left-handed coordinate system (both Y-up)
        //========================================================================================================
        private Vector3 ConvertPosition(Vector3 position)
        {
            position.x *= -scaleFactor;
            position.y *= scaleFactor;
            position.z *= scaleFactor;
            return position;
        }

        //===========================================================================================================================
        // Helper function to convert a rotation from a right-handed Captury Live to left-handed Unity coordinate system (both Y-up)
        //===========================================================================================================================
        private Quaternion ConvertRotation(Vector3 rotation)
        {
            Quaternion qx = Quaternion.AngleAxis(rotation.x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(rotation.y, Vector3.down);
            Quaternion qz = Quaternion.AngleAxis(rotation.z, Vector3.back);
            Quaternion qq = qz * qy * qx;
            return qq;
        }

        //===========================================================================================================
        // Helper function to convert a rotation from Unity back to Captury Live (left-handed to right-handed, Y-up)
        //===========================================================================================================
        private Quaternion ConvertRotationToLive(Quaternion rotation)
        {
            Vector3 angles = rotation.eulerAngles;

            Quaternion qx = Quaternion.AngleAxis(angles.x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(angles.y, Vector3.down);
            Quaternion qz = Quaternion.AngleAxis(angles.z, Vector3.back);
            Quaternion qq = qz * qy * qx;
            return qq;
        }

        //=============================================================================
        // Helper function to convert a rotation to the Euler angles Captury Live uses
        //=============================================================================
        private Vector3 ConvertToEulerAngles(Quaternion quat)
        {
            const float RAD2DEGf = 0.0174532925199432958f;
            Vector3 euler = new Vector3();
            float sqw = quat.w * quat.w;
            float sqx = quat.x * quat.x;
            float sqy = quat.y * quat.y;
            float sqz = quat.z * quat.z;
            float tmp1 = quat.x * quat.y;
            float tmp2 = quat.z * quat.w;
            euler[1] = (float)-Math.Asin(2.0 * (tmp1 - tmp2));
            float C = (float)Math.Cos(euler[1]);
            if (Math.Abs(C) > 0.005)
            {
                euler[2] = (float)Math.Atan2(2.0f * (tmp1 + tmp2) / C, (sqx - sqy - sqz + sqw) / C) * RAD2DEGf;
                euler[0] = (float)Math.Atan2(2.0f * (tmp1 + tmp2) / C, (-sqx - sqy + sqz + sqw) / C) * RAD2DEGf;
            }
            else
            {
                euler[2] = 0;
                if ((tmp1 - tmp2) < 0)
                    euler[0] = (float)Math.Atan2(0.0f, (-sqx + sqy - sqz + sqw) * 0.5f + (tmp1 + tmp2)) * RAD2DEGf;
                else
                    euler[0] = (float)Math.Atan2(2 * (tmp1 - tmp2), (-sqx + sqy - sqz + sqw) * 0.5f - (tmp1 + tmp2)) * RAD2DEGf;
            }
            euler[1] *= RAD2DEGf;

            return euler;
        }
    }

    //==========================================================================
    // Helper extension function to get all children from a specified transform
    //==========================================================================
    public static class TransformExtension
    {
        public static ArrayList GetAllChildren(this Transform transform)
        {
            ArrayList children = new ArrayList();
            foreach (Transform child in transform)
            {
                children.Add(child);
                children.AddRange(GetAllChildren(child));
            }
            return children;
        }
    }
}
