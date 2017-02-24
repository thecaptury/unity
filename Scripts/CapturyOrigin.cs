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
        /// Offset between this CapturyOrigin and the world origin (0,0,0)
        /// </summary>
        public Vector3 OffsetToWorldOrigin
        {
            get
            {
                return transform.position;
            }
        }
    }
}
