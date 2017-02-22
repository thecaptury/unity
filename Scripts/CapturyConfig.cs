using System;

namespace Captury
{
    [Serializable]
    public class CapturyConfig
    {
        /// <summary>
        /// AR Tag IDs which are assigned to the local user
        /// </summary>
        public int[] arTagIDs;

        /// <summary>
        /// The avatar ID which is assigned to the local user
        /// </summary>
        public int avatarID;
    }
}
