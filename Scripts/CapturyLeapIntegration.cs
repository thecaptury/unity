using Leap.Unity;
using System;
using UnityEngine;

namespace Captury
{
    //====================
    // integrates leap motion based hand tracking with The Captury's full body tracking
    //====================
    [RequireComponent(typeof(CapturyNetworkPlugin))]
    public class CapturyLeapIntegration : MonoBehaviour
    {
        private LeapProvider leapProvider;
        public int targetCapturyActorId = -1;   // id of CapturySkeleton
        public Transform leftTargetHand;        // transform of left hand
        public Transform rightTargetHand;       // transform of right hand
        private Transform[] leftFingers;
        private Transform[] rightFingers;
        private Quaternion[] leftFingersInit;
        private Quaternion[] rightFingersInit;
        private Quaternion leftHandInit;
        private Quaternion rightHandInit;
        private Vector3 leftHandInitOffset;
        private Vector3 rightHandInitOffset;

        public bool moveWrists = true; // set to true if you want to move the wrists where leap thinks they are

        // if connected will send constraints back to Live to force wrist positions to the right place
        // and updates finger positions on the server model
        private CapturyNetworkPlugin networkPlugin;

        public void setTargetModel(Transform left, Transform right, int id)
        {
            targetCapturyActorId = id;
            leftTargetHand = left;
            rightTargetHand = right;

            if (leapProvider && leftTargetHand && rightTargetHand)
            {
                String[] names = new String[] { "HandThumb1", "HandIndex1", "HandMiddle1", "HandRing1", "HandPinky1" };
                leftFingers = new Transform[15];
                leftFingersInit = new Quaternion[15];
                rightFingersInit = new Quaternion[15];
                foreach (Transform child in leftTargetHand)
                {
                    for (int i = 0; i < 5; ++i)
                    {
                        if (child.name.EndsWith(names[i]))
                        {
                            leftFingers[i * 3] = child;
                            leftFingers[i * 3 + 1] = child.GetChild(0);
                            leftFingers[i * 3 + 2] = child.GetChild(0).GetChild(0);
                        }
                    }
                }
                rightFingers = new Transform[15];
                foreach (Transform child in rightTargetHand)
                {
                    for (int i = 0; i < 5; ++i)
                    {
                        if (child.name.EndsWith(names[i]))
                        {
                            rightFingers[i * 3] = child;
                            rightFingers[i * 3 + 1] = child.GetChild(0);
                            rightFingers[i * 3 + 2] = child.GetChild(0).GetChild(0);
                        }
                    }
                }
                leftHandInitOffset = leftTargetHand.localPosition;
                rightHandInitOffset = rightTargetHand.localPosition;
                leftHandInit = Quaternion.Euler(0, -90, 0);
                rightHandInit = Quaternion.Euler(0, 90, 0);
                for (int i = 0; i < 15; ++i)
                {
                    {
                        Vector3 bone = (leftFingers[i].GetChild(0).position - leftFingers[i].position).normalized;
                        Vector3 fwd = Vector3.Cross(bone, Vector3.back);
                        Vector3 up = Vector3.Cross(bone, fwd);
                        Matrix4x4 M = new Matrix4x4();
                        M.SetRow(0, new Vector4(bone.x, bone.y, bone.z, 0.0f));
                        M.SetRow(1, new Vector4(fwd.x, fwd.y, fwd.z, 0.0f));
                        M.SetRow(2, new Vector4(up.x, up.y, up.z, 0.0f));
                        M.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                        leftFingersInit[i] = Quaternion.Euler(0.0f, -90.0f, 0.0f) * Quaternion.LookRotation(M.GetColumn(2), M.GetColumn(1));//FromToRotation(bone, Vector3.right);
                    }
                    {
                        Vector3 bone = (rightFingers[i].GetChild(0).position - rightFingers[i].position).normalized;
                        Vector3 fwd = Vector3.Cross(bone, Vector3.forward);
                        Vector3 up = Vector3.Cross(bone, fwd);
                        Matrix4x4 M = new Matrix4x4();
                        M.SetRow(0, new Vector4(bone.x, bone.y, bone.z, 0.0f));
                        M.SetRow(1, new Vector4(fwd.x, fwd.y, fwd.z, 0.0f));
                        M.SetRow(2, new Vector4(up.x, up.y, up.z, 0.0f));
                        M.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                        rightFingersInit[i] = Quaternion.Euler(0.0f, -90.0f, 0.0f) * Quaternion.LookRotation(M.GetColumn(2), M.GetColumn(1));
                    }
                }
            }
        }

        //=============================
        // this is run once at startup
        //=============================
        void Start()
        {
            networkPlugin = GetComponent<CapturyNetworkPlugin>();
            if (networkPlugin == null)
            {
                Debug.LogError("No CapturyNetworkPlugin attached to " + name);
            }

            leapProvider = FindObjectOfType<LeapProvider>();
            if (leapProvider == null)
            {
                Debug.LogError("No LeapProvider in Scene");
            }
        }

        //==========================
        // this is run once at exit
        //==========================
        void OnDisable()
        {
        }

        //============================
        // this is run once per frame
        //============================
        void Update()
        {
            if (!leapProvider || !leftTargetHand || !rightTargetHand || targetCapturyActorId == -1)
                return;

            Leap.Frame frame = leapProvider.CurrentFrame;
            if (frame.Hands.Count == 0)
                return;

            bool leftSet = false;
            bool rightSet = false;
            foreach (Leap.Hand hand in frame.Hands)
            {
                Transform targetHand;
                Transform[] targetFingers;
                Quaternion[] initRot;
                Quaternion rot;
                if (hand.IsLeft)
                {
                    targetHand = leftTargetHand;
                    targetFingers = leftFingers;
                    initRot = leftFingersInit;
                    rot = leftHandInit;
                    leftSet = true;
                }
                else
                {
                    targetHand = rightTargetHand;
                    targetFingers = rightFingers;
                    initRot = rightFingersInit;
                    rot = rightHandInit;
                    rightSet = true;
                }

                if (moveWrists)
                    targetHand.position = hand.WristPosition.ToVector3();
                targetHand.rotation = hand.Basis.rotation.ToQuaternion() * rot;
                //Debug.Log(frame.Id + " leap hand rot " + hand.Basis.rotation.ToQuaternion());
                foreach (Leap.Finger finger in hand.Fingers)
                {
                    int i = (int)finger.Type;
                    Transform targetFinger = targetFingers[i * 3];

                    //				Quaternion q = initRot[i*3];// * finger.Bone(Leap.Bone.BoneType.TYPE_PROXIMAL).Rotation.ToQuaternion();
                    //				if (i == 1)
                    //					Debug.Log(frame.Id + " leap hand has " + targetFinger.name + " " + finger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Rotation.ToQuaternion());

                    //				targetFinger.position                         = finger.Bone(Leap.Bone.BoneType.TYPE_PROXIMAL).PrevJoint.ToVector3();
                    //				targetFinger.GetChild(0).position             = finger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).PrevJoint.ToVector3();
                    //				targetFinger.GetChild(0).GetChild(0).position = finger.Bone(Leap.Bone.BoneType.TYPE_DISTAL).PrevJoint.ToVector3();
                    targetFinger.rotation = finger.Bone(Leap.Bone.BoneType.TYPE_PROXIMAL).Rotation.ToQuaternion() * initRot[i * 3];
                    targetFinger.GetChild(0).rotation = finger.Bone(Leap.Bone.BoneType.TYPE_INTERMEDIATE).Rotation.ToQuaternion() * initRot[i * 3 + 1];
                    targetFinger.GetChild(0).GetChild(0).rotation = finger.Bone(Leap.Bone.BoneType.TYPE_DISTAL).Rotation.ToQuaternion() * initRot[i * 3 + 2];
                }
            }
            if (!leftSet)
            {
                leftTargetHand.localRotation = Quaternion.identity;
                leftTargetHand.localPosition = leftHandInitOffset;
                foreach (Transform fingerBone in leftFingers)
                {
                    fingerBone.localRotation = Quaternion.identity;
                }
            }
            else if (networkPlugin)
            {
                networkPlugin.setRotationConstraint(targetCapturyActorId, leftTargetHand.name, leftTargetHand);
                foreach (Transform finger in leftFingers)
                    networkPlugin.setRotationConstraint(targetCapturyActorId, finger.name, finger);
            }
            if (!rightSet)
            {
                rightTargetHand.localRotation = Quaternion.identity;
                rightTargetHand.localPosition = rightHandInitOffset;
                foreach (Transform fingerBone in rightFingers)
                {
                    fingerBone.localRotation = Quaternion.identity;
                }
            }
            else if (networkPlugin)
            {
                networkPlugin.setRotationConstraint(targetCapturyActorId, rightTargetHand.name, rightTargetHand);
                foreach (Transform finger in rightFingers)
                    networkPlugin.setRotationConstraint(targetCapturyActorId, finger.name, finger);
            }
        }
    }
}
