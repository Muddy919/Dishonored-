using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Only cleans leftover letterbox junk. Does not change cameras or UI.
/// </summary>
public class AspectRatioEnforcer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<AspectRatioEnforcer>() != null)
            return;
#else
        if (FindObjectOfType<AspectRatioEnforcer>() != null)
            return;
#endif

        var host = new GameObject("AspectRatioEnforcer");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<AspectRatioEnforcer>();
        Cleanup();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Cleanup();
    }

    static void Cleanup()
    {
        var bars = GameObject.Find("LetterboxBars");
        if (bars != null)
            Object.Destroy(bars);

#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>(true);
#endif
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].targetTexture == null)
                cams[i].rect = new Rect(0f, 0f, 1f, 1f);
        }
    }
}
