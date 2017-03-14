using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif      
using UnityEngine;

namespace Captury
{
    public class CapturyConfigManager
    {
        /// <summary>
        /// Path to the config file
        /// </summary>
        public static readonly string configFilePath = "./captury-config.json";

        private static CapturyConfig config;

        /// <summary>
        /// Returns the <see cref="CapturyConfig"/> if it can be loaded and parsed.
        /// Otherwise <see cref="null"/> is returned;
        /// </summary>
        public static CapturyConfig Config
        {
            get
            {
                if(config == null)
                {
                    config = LoadConfig();
                }
                return config;
            }
        }

        /// <summary>
        /// Loads the config file from <see cref="configFilePath"/>
        /// </summary>
        private static CapturyConfig LoadConfig()
        {
            if (File.Exists(configFilePath))
            {
                string configJSON = File.ReadAllText(configFilePath, System.Text.Encoding.ASCII);
                config = JsonUtility.FromJson<CapturyConfig>(configJSON);
            } else
            {
                config = new CapturyConfig();
                Debug.LogErrorFormat("No Captury config file found at {0}.", configFilePath);
            }
            return config;
        }

        /// <summary>
        /// Saves the given config to related file
        /// </summary>
        /// <param name="config"></param>
        public static void SaveConfig()
        {
            string directoryPath = Path.GetDirectoryName(configFilePath);

            if (Directory.Exists(directoryPath) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            }

            string jsonString = JsonUtility.ToJson(config, true);
            File.WriteAllText(configFilePath, jsonString, Encoding.ASCII);
        }

        public static void CopyToBuildFolder(CapturyConfig config, string pathToBuiltProject)
        {
            string buildConfigPath = Path.Combine(Path.GetDirectoryName(pathToBuiltProject), Path.GetFileName(configFilePath));

            Copy(configFilePath, buildConfigPath);
        }

        public static void Copy(string src, string dest)
        {
            if (File.Exists(src))
            {
                Debug.LogFormat("Copying file from {0} to {1}", src, dest);
                string directory = Path.GetDirectoryName(dest);
                if (Directory.Exists(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(src, dest, true);
            }
        }

#if UNITY_EDITOR
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            string buildConfigPath = Path.Combine(Path.GetDirectoryName(pathToBuiltProject), Path.GetFileName(configFilePath));
        
            if (File.Exists(configFilePath))
            {
                Debug.LogFormat("Copying Captury config file from {0} to {1}", configFilePath, buildConfigPath);
                File.Copy(configFilePath, buildConfigPath, true);
            }
        }
#endif
    }
}
