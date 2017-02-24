using UnityEngine;

namespace Captury
{
    public class TransformFollower : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Smoothing factor")]
        private float smooth = 5.0f;

        [SerializeField]
        [Tooltip("Position offset.")]
        private Vector3 offset = Vector3.zero;

        [SerializeField]
        [Tooltip("Rotation offset euler Angles.")]
        private Vector3 rotationOffset = Vector3.zero;

        /// <summary>
        /// Target which will be followed
        /// </summary>
        private Transform target;

        /// <summary>
        /// Target which will be followed
        /// </summary>
        public Transform Target
        {
            get
            {
                return target;
            }
            set
            {
                target = value;
            }
        }

        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.X))
            {
                UnityEngine.VR.InputTracking.Recenter();
            }
                
            if (target != null)
            {
                transform.position = Vector3.Lerp(transform.position, target.transform.TransformPoint(offset), Time.deltaTime * smooth);
                transform.rotation = Quaternion.Euler(rotationOffset);
            }
        }
    }
}
