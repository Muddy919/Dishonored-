using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Watches offline arena fighters and shows win / lose overlays.
/// </summary>
public class ArenaRoundController : MonoBehaviour
{
    public static bool IsFinished { get; private set; }

    GameObject player;
    GameObject bot;
    AttributesManager playerAttrs;
    AttributesManager botAttrs;
    bool built;
    GameObject panel;
    Text titleLabel;

    public void Bind(GameObject playerGo, GameObject botGo)
    {
        player = playerGo;
        bot = botGo;
        playerAttrs = player != null ? player.GetComponent<AttributesManager>() : null;
        botAttrs = bot != null ? bot.GetComponent<AttributesManager>() : null;
        IsFinished = false;
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
        IsFinished = false;
        if (panel != null)
            panel.SetActive(false);
    }

    void Update()
    {
        if (IsFinished || !ArenaBotModeState.IsActive)
            return;

        if (playerAttrs != null && playerAttrs.IsDead())
        {
            Finish(false);
            return;
        }

        if (botAttrs != null && botAttrs.IsDead())
        {
            Finish(true);
            return;
        }
    }

    public static void NotifyBotDied()
    {
#if UNITY_2023_1_OR_NEWER
        var round = FindFirstObjectByType<ArenaRoundController>();
#else
        var round = FindObjectOfType<ArenaRoundController>();
#endif
        if (round != null)
            round.Finish(true);
    }

    void Finish(bool playerWon)
    {
        if (IsFinished)
            return;

        IsFinished = true;

        // Stop bot / player control.
        if (bot != null)
        {
            var ai = bot.GetComponent<ArenaFighterBotAI>();
            if (ai != null)
                ai.enabled = false;
        }

        if (playerWon)
            ShowResult("YOU WIN", new Color(0.35f, 0.95f, 0.45f, 1f));
        else
        {
            // Player death overlay may also fire — prefer our arena result.
            ShowResult("YOU LOSE", new Color(1f, 0.35f, 0.3f, 1f));
        }
    }

    void ShowResult(string title, Color color)
    {
        if (!built)
            BuildUi();

        EnsureEventSystem();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (titleLabel != null)
        {
            titleLabel.text = title;
            titleLabel.color = color;
        }

        if (panel != null)
            panel.SetActive(true);
    }

    void Rematch()
    {
        IsFinished = false;
        Time.timeScale = 1f;
        FastSceneLoader.ReloadCurrent();
    }

    void Menu()
    {
        IsFinished = false;
        Time.timeScale = 1f;
        ArenaBotModeState.Disable();
        FastSceneLoader.Load(0);
    }

    void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;
#endif
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("ArenaResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8600;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.78f);

        titleLabel = CreateLabel(panel.transform, "Title", "YOU WIN", 56, FontStyle.Bold,
            new Vector2(0f, 120f), new Vector2(700f, 70f), Color.white);

        MakeButton(panel.transform, "Rematch", "Rematch", new Vector2(0f, 10f), Rematch);
        MakeButton(panel.transform, "Menu", "Main Menu", new Vector2(0f, -70f), Menu);

        panel.SetActive(false);
        built = true;
    }

    Text CreateLabel(Transform parent, string name, string value, int size, FontStyle style, Vector2 pos, Vector2 dim, Color color)
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
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    void MakeButton(Transform parent, string name, string caption, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360f, 56f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.22f, 0.95f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        CreateLabel(go.transform, "Label", caption, 26, FontStyle.Bold,
            Vector2.zero, new Vector2(340f, 50f), Color.white);
    }
}
