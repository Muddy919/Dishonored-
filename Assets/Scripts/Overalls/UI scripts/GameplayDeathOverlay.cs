using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instant death panel with Restart / Menu — no death-scene hopping.
/// </summary>
public class GameplayDeathOverlay : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    static GameplayDeathOverlay instance;
    GameObject panel;
    bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject("GameplayDeathOverlay");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<GameplayDeathOverlay>();
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
        Hide();
    }

    public static void Show()
    {
        Bootstrap();
        if (instance != null)
            instance.ShowInternal();
    }

    void ShowInternal()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
            return;

        if (!built)
            BuildUi();

        EnsureEventSystem();

        if (PauseMenu.IsPaused)
        {
            // Death wins over pause UI.
        }

        Time.timeScale = 0f;
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (panel != null)
            panel.SetActive(true);
    }

    void Hide()
    {
        IsOpen = false;
        if (panel != null)
            panel.SetActive(false);
        if (!PauseMenu.IsPaused)
            Time.timeScale = 1f;
    }

    void Restart()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        FastSceneLoader.ReloadCurrent();
    }

    void Menu()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        BossModeState.Disable();
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
        if (built)
            return;

        var canvasGo = new GameObject("DeathCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("DeathPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.12f, 0.02f, 0.02f, 0.78f);

        CreateLabel(panel.transform, "Title", "YOU DIED", 56, FontStyle.Bold,
            new Vector2(0f, 120f), new Vector2(700f, 70f), new Color(1f, 0.35f, 0.3f, 1f));

        MakeButton(panel.transform, "Restart", "Restart Level", new Vector2(0f, 10f), Restart);
        MakeButton(panel.transform, "Menu", "Main Menu", new Vector2(0f, -70f), Menu);

        CreateLabel(panel.transform, "Hint", "Quick restart — no menu hopping", 20, FontStyle.Normal,
            new Vector2(0f, -150f), new Vector2(500f, 30f), new Color(0.85f, 0.85f, 0.85f, 1f));

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
        img.color = new Color(0.18f, 0.08f, 0.08f, 0.95f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        CreateLabel(go.transform, "Label", caption, 26, FontStyle.Bold,
            Vector2.zero, new Vector2(340f, 50f), Color.white);
    }
}
