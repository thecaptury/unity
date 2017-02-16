using UnityEngine;
using System.Collections;

namespace Captury
{
    public class CameraFollower : MonoBehaviour
    {
        public Transform target;
        public float smooth = 5.0f;
        public Vector3 offset = new Vector3(0, 0, 0);
        public Quaternion orientationOffset = new Quaternion();

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (Input.GetKeyUp(KeyCode.X))
                UnityEngine.VR.InputTracking.Recenter();
            if (target != null)
            {
                transform.position = Vector3.Lerp(transform.position, target.transform.TransformPoint(offset), Time.deltaTime * smooth);
                transform.rotation = orientationOffset;
            }
        }
    }
}
