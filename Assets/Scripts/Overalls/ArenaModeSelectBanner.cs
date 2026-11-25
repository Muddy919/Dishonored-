using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// On Character Select during VS Bot, shows arena + difficulty reminder.
/// </summary>
public class ArenaModeSelectBanner : MonoBehaviour
{
    const string CharacterSelectSceneName = "CharacterSelect";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != CharacterSelectSceneName)
            return;

        if (!ArenaBotModeState.IsActive)
            return;

#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<ArenaModeSelectBanner>() != null)
            return;
#else
        if (FindObjectOfType<ArenaModeSelectBanner>() != null)
            return;
#endif

        var host = new GameObject("ArenaModeSelectBanner");
        host.AddComponent<ArenaModeSelectBanner>();
    }

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        var canvas = FindFirstObjectByType<Canvas>();
#else
        var canvas = FindObjectOfType<Canvas>();
#endif
        if (canvas == null)
            return;

        var go = new GameObject("ArenaModeBanner", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(820f, 70f);

        go.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.85f, 0.9f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = "1V1 ARENA — VS BOT  |  " + ArenaBotModeState.Difficulty + "  |  pick your fighter";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }
}
