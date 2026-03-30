using UnityEngine;
using UnityEngine.SceneManagement;

namespace VN
{
    public static class VNSceneLoader
    {
        public static string TargetSceneName { get; private set; }
        public static string LoadingSceneName { get; private set; } = "Loading";

        public static void ConfigureLoadingScene(string loadingSceneName)
        {
            if (string.IsNullOrWhiteSpace(loadingSceneName))
            {
                Debug.LogError("[VN] Loading scene name is null or empty.");
                return;
            }

            LoadingSceneName = loadingSceneName;
        }

        public static void Load(string targetSceneName)
        {
            Load(targetSceneName, LoadingSceneName);
        }

        public static void Load(string targetSceneName, string loadingSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[VN] Target scene name is null or empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(loadingSceneName))
            {
                Debug.LogError("[VN] Loading scene name is null or empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError($"[VN] Target scene '{targetSceneName}' is not added to Build Settings.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                Debug.LogError($"[VN] Loading scene '{loadingSceneName}' is not added to Build Settings.");
                return;
            }

            TargetSceneName = targetSceneName;
            LoadingSceneName = loadingSceneName;

            SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
        }

        internal static string ConsumeTargetScene()
        {
            string result = TargetSceneName;
            TargetSceneName = null;
            return result;
        }
    }
}