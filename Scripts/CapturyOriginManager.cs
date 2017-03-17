using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Captury
{
    /// <summary>
    /// Manages the <see cref="CapturyOrigin"/> whithin a scene which defines the origin of the coordinate system of all avatars 
    /// </summary>
    [RequireComponent(typeof(CapturyNetworkPlugin))]
    public class CapturyOriginManager : MonoBehaviour
    {
        /// <summary>
        /// Event which is called when <see cref="CapturyOrigin"/> changes 
        /// </summary>
        /// <param name="capturyOrigin"></param>
        public delegate void CapturyOriginDelegate(CapturyOrigin capturyOrigin);
        public event CapturyOriginDelegate CapturyOriginChanged;

        /// <summary>
        /// Coordinate system origin for all avatars
        /// </summary>
        private CapturyOrigin capturyOrigin;

        /// <summary>
        /// Network plugin
        /// </summary>
        private CapturyNetworkPlugin networkPlugin;

        // Use this for initialization
        void Start()
        {
            networkPlugin = GetComponent<CapturyNetworkPlugin>();

            // find CapturyOrigin to define spawn position of avatars
            capturyOrigin = FindObjectOfType<CapturyOrigin>();
            if (capturyOrigin == null)
            {
                // create origin at world origin if none exists
                capturyOrigin = CreateCapturyOrigin();
            }
            CapturyOriginChanged(capturyOrigin);

            // register for scene change events
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>
        /// Called when scene has changed/switched.
        /// </summary>
        /// <param name="previousScene"></param>
        /// <param name="currentScene"></param>
        private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            capturyOrigin = FindObjectOfType<CapturyOrigin>();

            if(capturyOrigin == null)
            {
                // create origin at world origin of none exists
                capturyOrigin = CreateCapturyOrigin();
            }

            CapturyOriginChanged(capturyOrigin);

            networkPlugin.offsetToWorld = capturyOrigin.OffsetToWorldOrigin;
        }

        /// <summary>
        /// Creates a CapturyOrigin at world origin.
        /// </summary>
        /// <returns></returns>
        private CapturyOrigin CreateCapturyOrigin()
        {
            GameObject go = new GameObject();
            go.name = "CapturyOrigin";
            CapturyOrigin capturyOrigin = go.AddComponent<CapturyOrigin>();
            return capturyOrigin;
        }
    }
}
