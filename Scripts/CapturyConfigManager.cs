using Captury;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif      
using UnityEngine;

public class CapturyConfigManager
{
    /// <summary>
    /// Name of the <see cref="CapturyConfig"/> file.
    /// The file has to be placed in the Resources folder.
    /// </summary>
    public static readonly string configFilePath = "./capturyConfig.json";

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
    /// Loads the config file.
    /// Config values:
    /// markerID=0...10
    /// </summary>
    private static CapturyConfig LoadConfig()
    {
        if (File.Exists(configFilePath))
        {
            string configJSON = File.ReadAllText(configFilePath, System.Text.Encoding.ASCII);
            CapturyConfig cC = JsonUtility.FromJson<CapturyConfig>(configJSON);
            return cC;
        } else
        {
            Debug.LogErrorFormat("No Captury config file foudn at {0}.", configFilePath);
        }
        return null;
    }

#if UNITY_EDITOR
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        string buildConfigPath = Path.Combine(Path.GetDirectoryName(pathToBuiltProject), Path.GetFileName(configFilePath));
        
        if (File.Exists(configFilePath))
        {
            Debug.LogFormat("Copying Captury config file from  {0} to {1}", configFilePath, buildConfigPath);
            File.Copy(configFilePath, buildConfigPath);
        }
    }
#endif
    }
