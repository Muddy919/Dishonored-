using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Esc pause mid-fight: Resume / Restart / Difficulty / Menu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    GameObject panel;
    Text difficultyLabel;
    float savedTimeScale = 1f;
    bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindFirstObjectByType<PauseMenu>() != null)
            return;
#else
        if (Object.FindObjectOfType<PauseMenu>() != null)
            return;
#endif

        var host = new GameObject("PauseMenu");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<PauseMenu>();
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
        ForceResume();
        if (panel != null)
            panel.SetActive(false);
    }

    void Update()
    {
        bool canPause = LevelEnemyTracker.IsGameplayLevelScene()
                        || LevelEnemyTracker.IsMultiplayerBattleScene();

        if (!canPause)
        {
            if (IsPaused)
                ForceResume();
            if (panel != null)
                panel.SetActive(false);
            return;
        }

        if (GameplayDeathOverlay.IsOpen)
            return;

        bool esc =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || Input.GetKeyDown(KeyCode.Escape);

        if (!esc)
            return;

        if (IsPaused)
            Resume();
        else
            Pause();
    }

    void Pause()
    {
        if (!built)
            BuildUi();

        EnsureEventSystem();
        savedTimeScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        IsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (panel != null)
        {
            panel.SetActive(true);
            RefreshDifficultyLabel();
        }
    }

    public void Resume()
    {
        ForceResume();
        if (panel != null)
            panel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ForceResume()
    {
        Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
        IsPaused = false;
    }

    void RestartLevel()
    {
        ForceResume();
        FastSceneLoader.ReloadCurrent();
    }

    void CycleDifficulty()
    {
        GameDifficulty.Cycle();
        RefreshDifficultyLabel();
    }

    void GoMenu()
    {
        ForceResume();
        // Leaves Netcode cleanly via FastSceneLoader → NetworkPlayCleanup.
        FastSceneLoader.Load(0);
    }

    void RefreshDifficultyLabel()
    {
        if (difficultyLabel != null)
            difficultyLabel.text = "Difficulty: " + GameDifficulty.Label + "  (click to cycle)";
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

        var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.72f);

        CreateLabel(panel.transform, "PausedTitle", "PAUSED", 52, FontStyle.Bold,
            new Vector2(0f, 160f), new Vector2(600f, 70f), Color.white);

        MakeButton(panel.transform, "Resume", "Resume", new Vector2(0f, 60f), Resume);
        MakeButton(panel.transform, "Restart", "Restart Level", new Vector2(0f, -10f), RestartLevel);

        difficultyLabel = MakeButton(panel.transform, "Difficulty", "Difficulty: Normal", new Vector2(0f, -80f), CycleDifficulty);
        RefreshDifficultyLabel();

        MakeButton(panel.transform, "Menu", "Main Menu", new Vector2(0f, -150f), GoMenu);

        CreateLabel(panel.transform, "Hint", "Esc to resume", 20, FontStyle.Normal,
            new Vector2(0f, -230f), new Vector2(400f, 30f), new Color(0.75f, 0.8f, 0.85f, 1f));

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

    Text MakeButton(Transform parent, string name, string caption, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360f, 56f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var text = CreateLabel(go.transform, "Label", caption, 26, FontStyle.Bold,
            Vector2.zero, new Vector2(340f, 50f), Color.white);
        text.raycastTarget = false;
        return text;
    }
}
