using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Clamps insane neon-city bloom at runtime so Level 2 isn't a cyan washout.
/// </summary>
public class NeonBloomFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<NeonBloomFix>() != null)
            return;
#else
        if (FindObjectOfType<NeonBloomFix>() != null)
            return;
#endif

        var host = new GameObject("NeonBloomFix");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<NeonBloomFix>();
        FixAll();
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
        FixAll();
    }

    static void FixAll()
    {
#if UNITY_2023_1_OR_NEWER
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var volumes = Object.FindObjectsOfType<Volume>(true);
#endif
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume v = volumes[i];
            if (v == null)
                continue;

            VolumeProfile profile = v.profile != null ? v.profile : v.sharedProfile;
            if (profile == null)
                continue;

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.active = true;
                bloom.intensity.Override(Mathf.Min(bloom.intensity.value, 0.35f));
                if (bloom.intensity.value < 0.05f)
                    bloom.intensity.Override(0.2f);
                bloom.threshold.Override(Mathf.Max(bloom.threshold.value, 1.2f));
                bloom.clamp.Override(8f);
                bloom.scatter.Override(0.35f);
            }

            if (profile.TryGet(out DepthOfField dof))
            {
                // Menu/character select looked soft partly from DoF.
                dof.active = false;
            }
        }
    }
}
