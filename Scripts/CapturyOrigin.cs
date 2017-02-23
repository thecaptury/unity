using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Captury
{
    /// <summary>
    /// Acts as origin for all avatars.
    /// </summary>
    public class CapturyOrigin : MonoBehaviour
    {
        public void Start()
        {
            // keeps the origin through scene change. necessary to not lose avatars
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Gets the offset to world origin.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetOffsetToWorldOrigin()
        {
            return transform.position;
        }

        /// <summary>
        /// Removes the origin but keeps the avatars and puts them under root.
        /// </summary>
        public void RemoveOrigin()
        {
            transform.DetachChildren();
            DestroyImmediate(gameObject);
        }
    }
}
