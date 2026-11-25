using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single in-level objective HUD: Kill all enemies + remaining count.
/// </summary>
public class LevelObjectiveHud : MonoBehaviour
{
    static LevelObjectiveHud instance;

    Text objectiveText;
    Text countText;
    Text hintText;
    float hintHideAt;
    bool uiBuilt;
    float nextHudRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!LevelEnemyTracker.ShouldShowHud())
            return;

        Ensure();
    }

    public static void Ensure()
    {
        if (!LevelEnemyTracker.ShouldShowHud())
            return;

        if (instance != null)
            return;

#if UNITY_2023_1_OR_NEWER
        instance = FindFirstObjectByType<LevelObjectiveHud>();
#else
        instance = FindObjectOfType<LevelObjectiveHud>();
#endif
        if (instance != null)
            return;

        var host = new GameObject("LevelObjectiveHud");
        instance = host.AddComponent<LevelObjectiveHud>();
    }

    public static void ShowBlockedHint(int remaining)
    {
        Ensure();
        if (instance == null)
            return;

        instance.hintHideAt = Time.time + 2.2f;
        if (instance.hintText != null)
        {
            instance.hintText.text = "Defeat all enemies first! (" + remaining + " left)";
            instance.hintText.gameObject.SetActive(true);
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        if (!LevelEnemyTracker.ShouldShowHud())
        {
            Destroy(gameObject);
            return;
        }

        BuildUi();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void BuildUi()
    {
        if (uiBuilt)
            return;

        var canvasGo = new GameObject("ObjectiveOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4800;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -28f);
        rt.sizeDelta = new Vector2(360f, 110f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
        panel.GetComponent<Image>().raycastTarget = false;

        objectiveText = CreateText(panel.transform, "Objective", "OBJECTIVE: Kill all enemies", 22, FontStyle.Bold,
            new Vector2(0f, 28f), new Vector2(340f, 34f), new Color(1f, 0.7f, 0.25f, 1f));

        countText = CreateText(panel.transform, "Count", "Enemies left: --", 28, FontStyle.Bold,
            new Vector2(0f, -8f), new Vector2(340f, 36f), Color.white);

        hintText = CreateText(panel.transform, "Hint", "", 18, FontStyle.Normal,
            new Vector2(0f, -42f), new Vector2(340f, 26f), new Color(1f, 0.45f, 0.35f, 1f));
        hintText.gameObject.SetActive(false);

        uiBuilt = true;
    }

    Text CreateText(Transform parent, string name, string value, int size, FontStyle style, Vector2 pos, Vector2 dim, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;

        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    void Update()
    {
        if (!LevelEnemyTracker.ShouldShowHud())
        {
            Destroy(gameObject);
            return;
        }

        if (!uiBuilt)
            BuildUi();

        if (hintText != null && hintText.gameObject.activeSelf && Time.time >= hintHideAt)
            hintText.gameObject.SetActive(false);

        if (Time.unscaledTime < nextHudRefresh)
            return;

        nextHudRefresh = Time.unscaledTime + 0.25f;

        if (BossModeState.IsActive)
        {
            if (objectiveText != null)
                objectiveText.text = "OBJECTIVE: Defeat the boss";
            if (countText != null)
            {
                countText.text = "Boss fight active";
                countText.color = new Color(1f, 0.85f, 0.3f, 1f);
            }
            return;
        }

        if (ArenaBotModeState.IsActive)
        {
            if (objectiveText != null)
                objectiveText.text = "OBJECTIVE: Defeat the bot";
            if (countText != null)
            {
                string botName = ArenaBotModeState.GetBotFighterName() ?? "Bot";
                countText.text = "VS " + botName + "  (" + ArenaBotModeState.Difficulty + ")";
                countText.color = new Color(0.55f, 0.85f, 1f, 1f);
            }
            return;
        }

        int left = LevelEnemyTracker.CountAliveEnemies();
        if (countText != null)
        {
            if (left <= 0)
            {
                countText.text = "Enemies left: 0  - Boss ready!";
                countText.color = new Color(0.55f, 1f, 0.55f, 1f);
                if (objectiveText != null)
                    objectiveText.text = "OBJECTIVE: Enter the boss area";
            }
            else
            {
                countText.text = "Enemies left: " + left;
                countText.color = Color.white;
                if (objectiveText != null)
                    objectiveText.text = "OBJECTIVE: Kill all enemies";
            }
        }
    }
}
