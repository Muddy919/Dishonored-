using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Death-screen text only. Does not rewrite every CanvasScaler
/// (that made menus look zoomed out).
/// </summary>
public class UiAspectFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<UiAspectFix>() != null)
            return;
#else
        if (FindObjectOfType<UiAspectFix>() != null)
            return;
#endif

        var host = new GameObject("UiAspectFix");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<UiAspectFix>();
        FixDeathUi();
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
        FixDeathUi();
    }

    static void FixDeathUi()
    {
        string scene = SceneManager.GetActiveScene().name ?? "";
        if (scene.IndexOf("Death", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

#if UNITY_2023_1_OR_NEWER
        var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var scalers = Object.FindObjectsOfType<CanvasScaler>(true);
#endif
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler s = scalers[i];
            if (s == null)
                continue;

            // Death screens were authored around 1920x1080 after our patch.
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
        }

#if UNITY_2023_1_OR_NEWER
        var tmps = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var tmps = Object.FindObjectsOfType<TextMeshProUGUI>(true);
#endif
        for (int i = 0; i < tmps.Length; i++)
        {
            TextMeshProUGUI t = tmps[i];
            if (t == null || string.IsNullOrEmpty(t.text))
                continue;

            bool isDied = t.text.IndexOf("YOU DIED", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBtn = t.text.IndexOf("Retry", System.StringComparison.OrdinalIgnoreCase) >= 0
                         || t.text.IndexOf("Quit", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isDied && !isBtn)
                continue;

            var rt = t.rectTransform;
            if (isDied)
            {
                rt.sizeDelta = new Vector2(900f, 160f);
                rt.anchoredPosition = new Vector2(0f, 120f);
                rt.localScale = Vector3.one;
                t.enableAutoSizing = true;
                t.fontSizeMin = 36f;
                t.fontSizeMax = 96f;
                t.fontSize = 96f;
            }

            t.enableWordWrapping = false;
            t.alignment = TextAlignmentOptions.Center;
        }
    }
}
