using UnityEngine;

/// <summary>
/// Unity breaks/mutes audio when multiple AudioListeners exist.
/// Fighter prefabs have listeners on both cameras — keep only one.
/// </summary>
public class SingleAudioListenerFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<SingleAudioListenerFix>() != null)
            return;
#else
        if (FindObjectOfType<SingleAudioListenerFix>() != null)
            return;
#endif

        var host = new GameObject("SingleAudioListenerFix");
        DontDestroyOnLoad(host);
        host.AddComponent<SingleAudioListenerFix>();
    }

    float nextCheck;

    void Start()
    {
        CameraSwitcher.EnforceSingleListener();
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.5f;
        CameraSwitcher.EnforceSingleListener();
    }
}
