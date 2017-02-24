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
        public Vector3 OffsetToWorldOrigin
        {
            get
            {
                return transform.position;
            }
        }
    }
}
