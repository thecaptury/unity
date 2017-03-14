using System;

namespace Captury
{
    [Serializable]
    public class CapturyConfig
    {
        [Serializable]
        public class HeadsetARTag
        {
            /// <summary>
            /// id of the AR tag which is attached to the headset
            /// </summary>
            public int id;

            /// <summary>
            /// offsets from head to AR Tag
            /// </summary>
            public float offsetPosX;
            public float offsetPosY;
            public float offsetPosZ;
            public float offsetRotX;
            public float offsetRotY;
            public float offsetRotZ;
        }

        /// <summary>
        /// array of ar tags which are attached to the headset
        /// </summary>
        public HeadsetARTag[] headsetARTags;

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

        /// <summary>
        /// if true AR tags will be displayed as smal plates
        /// </summary>
        public bool debugARTags;

        /// <summary>
        /// Distance threshold (in meter) for user assignment with AR Tag
        /// </summary>
        public float arTagSkeletonThreshold;
    }
}
