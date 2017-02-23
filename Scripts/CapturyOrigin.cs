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
        /// <summary>
        /// Gets the offset to world origin.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetOffsetToWorldOrigin()
        {
            return transform.position;
        }
    }
}
