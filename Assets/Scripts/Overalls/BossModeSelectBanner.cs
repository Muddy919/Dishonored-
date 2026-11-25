using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// On Character Select, shows which boss fight was chosen.
/// </summary>
public class BossModeSelectBanner : MonoBehaviour
{
    const string CharacterSelectSceneName = "CharacterSelect";

    static readonly string[] BossNames =
    {
        "Railway Protector",
        "Excavator",
        "Front Guard",
        "Brain Bot"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != CharacterSelectSceneName)
            return;

        if (!BossModeState.IsActive)
            return;

#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<BossModeSelectBanner>() != null)
            return;
#else
        if (FindObjectOfType<BossModeSelectBanner>() != null)
            return;
#endif

        var host = new GameObject("BossModeSelectBanner");
        host.AddComponent<BossModeSelectBanner>();
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

        int boss = Mathf.Clamp(BossModeState.SelectedBossLevel, 1, 4);
        string bossName = BossNames[boss - 1];

        var go = new GameObject("BossModeBanner", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(820f, 70f);

        go.GetComponent<Image>().color = new Color(0.85f, 0.2f, 0.1f, 0.9f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = "BOSS FIGHT " + boss + " — " + bossName + "  |  pick your fighter";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }
}
