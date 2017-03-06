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
        /// Avatar ID which is assigned to the local user
        /// </summary>
        public int avatarID;

        /// <summary>
        /// Captury Live host
        /// </summary>
        public string host;

        /// <summary>
        /// Captury Live port
        /// </summary>
        public ushort port;

        /// <summary>
        /// Timeout in ms for checking new actors
        /// </summary>
        public int actorCheckTimeout;

        /// <summary>
        /// Captury Live avatar scale factor
        /// </summary>
        public float scaleFactor;

        /// <summary>
        /// if true AR tags will be streamed from Captury Live
        /// </summary>
        public bool streamARTags;
    }
}
