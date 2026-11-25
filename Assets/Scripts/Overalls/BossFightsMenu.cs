using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main Menu → Boss Fights → pick one of 4 bosses → Character Select → that boss arena.
/// </summary>
public class BossFightsMenu : MonoBehaviour
{
    const string MenuSceneName = "Menu";
    const int CharacterSelectBuildIndex = 1;

    static readonly string[] BossTitles =
    {
        "Boss 1 — Railway Protector",
        "Boss 2 — Excavator",
        "Boss 3 — Front Guard",
        "Boss 4 — Brain Bot"
    };

    static readonly string[] BossSubtitles =
    {
        "Secret Lab's Interior",
        "Abandoned Factory",
        "Lab's Cooling Tunnel",
        "Mountain Forest Cabin"
    };

    static BossFightsMenuWatcher watcher;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureWatcher();
        watcher.EnsureMenuUi();
    }

    static void EnsureWatcher()
    {
        if (watcher != null)
            return;

        var go = new GameObject("BossFightsMenuWatcher");
        Object.DontDestroyOnLoad(go);
        watcher = go.AddComponent<BossFightsMenuWatcher>();
    }

    GameObject panelRoot;

    public void SetupMenuUi()
    {
        // Fresh menu visit clears leftover boss mode from a previous run.
        BossModeState.Disable();
        // Don't clear ArenaBotModeState here — ArenaMenu owns that on Menu load.
        EnsureEventSystem();
        BuildBossFightsButton();

        if (panelRoot != null)
            Destroy(panelRoot);

        panelRoot = BuildBossSelectPanel();
        panelRoot.SetActive(false);
    }

    void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
#else
        if (FindObjectOfType<EventSystem>() != null)
            return;
#endif
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void BuildBossFightsButton()
    {
        var multiplayer = FindMenuButtonNamed("Multiplayer");
        if (multiplayer == null)
        {
            Debug.LogError("BossFightsMenu: could not find Multiplayer button to clone.");
            return;
        }

        // Replace a stale button if the scene was reloaded / partially rebuilt.
        var existing = GameObject.Find("BossFights");
        if (existing != null)
            Destroy(existing);

        Transform parent = multiplayer.transform.parent;
        GameObject bossBtn = Instantiate(multiplayer, parent);
        bossBtn.SetActive(true);
        bossBtn.name = "BossFights";

        var bossRt = bossBtn.GetComponent<RectTransform>();
        if (bossRt != null)
        {
            bossRt.localRotation = Quaternion.identity;
            bossRt.localScale = Vector3.one;
            bossRt.anchorMin = new Vector2(0.5f, 0.5f);
            bossRt.anchorMax = new Vector2(0.5f, 0.5f);
            bossRt.pivot = new Vector2(0.5f, 0.5f);
            bossRt.anchoredPosition = new Vector2(-760f, -360f);
            bossRt.sizeDelta = new Vector2(420f, 88f);
        }

        var tmp = bossBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = "Boss Fights";
            tmp.rectTransform.localRotation = Quaternion.identity;
            tmp.rectTransform.localScale = Vector3.one;
        }

        var legacyText = bossBtn.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = "Boss Fights";
            legacyText.rectTransform.localRotation = Quaternion.identity;
            legacyText.rectTransform.localScale = Vector3.one;
        }

        var button = bossBtn.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OpenBossSelect);
        }
    }

    void OpenBossSelect()
    {
        EnsureEventSystem();
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    void CloseBossSelect()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void SelectBoss(int bossLevel)
    {
        Debug.Log("BossFightsMenu: selected boss " + bossLevel + " → Character Select");
        BossModeState.Enable(bossLevel);
        if (panelRoot != null)
            panelRoot.SetActive(false);
        FastSceneLoader.Load(CharacterSelectBuildIndex);
    }

    GameObject BuildBossSelectPanel()
    {
        // Own overlay canvas so clicks aren't eaten by menu UI / aspect fixes.
        var canvasGo = new GameObject("BossSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("BossSelectPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var dim = panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true;

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(720f, 520f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.98f);
        box.GetComponent<Image>().raycastTarget = true;

        CreateUiText(box.transform, "Title", "SELECT BOSS FIGHT", 34, FontStyle.Bold,
            new Vector2(0f, 220f), new Vector2(680f, 50f));
        CreateUiText(box.transform, "Hint", "Choose a boss, then pick your fighter.", 18, FontStyle.Normal,
            new Vector2(0f, 175f), new Vector2(680f, 36f));

        for (int i = 0; i < BossTitles.Length; i++)
        {
            int bossLevel = i + 1;
            float y = 100f - i * 75f;
            string label = BossTitles[i] + "\n" + BossSubtitles[i];
            CreateBossRow(box.transform, bossLevel, label, new Vector2(0f, y));
        }

        CreateSimpleButton(box.transform, "Back", new Vector2(0f, -210f), new Vector2(200f, 48f), CloseBossSelect);
        return panel;
    }

    void CreateBossRow(Transform parent, int bossLevel, string label, Vector2 pos)
    {
        var go = new GameObject("Boss" + bossLevel, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(620f, 60f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.95f, 0.4f, 0.12f, 1f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        int captured = bossLevel;
        button.onClick.AddListener(() => SelectBoss(captured));

        CreateUiText(go.transform, "Label", label, 18, FontStyle.Bold, Vector2.zero, new Vector2(600f, 56f));
    }

    void CreateSimpleButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.35f, 0.35f, 0.38f, 1f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateUiText(go.transform, "Label", label, 22, FontStyle.Bold, Vector2.zero, size);
    }

    void CreateUiText(Transform parent, string name, string text, int fontSize, FontStyle style, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var label = go.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
    }

    /// <summary>Finds a menu button even if ArenaMenu hid the original Multiplayer object.</summary>
    public static GameObject FindMenuButtonNamed(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null)
            return go;

#if UNITY_2023_1_OR_NEWER
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
#endif
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null || transforms[i].name != objectName)
                continue;
            if (!transforms[i].gameObject.scene.IsValid())
                continue;
            return transforms[i].gameObject;
        }

        return null;
    }
}

/// <summary>
/// Survives scene changes and recreates the Boss Fights button every time Menu loads.
/// RuntimeInitializeOnLoad only runs once per domain, so returning from Character Select
/// / levels would otherwise leave the button missing.
/// </summary>
public class BossFightsMenuWatcher : MonoBehaviour
{
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
        EnsureMenuUi();
    }

    public void EnsureMenuUi()
    {
        if (!IsMenuScene())
            return;

        StopAllCoroutines();
        StartCoroutine(EnsureMenuUiRoutine());
    }

    static bool IsMenuScene()
    {
        string name = SceneManager.GetActiveScene().name ?? "";
        return name.Equals("Menu", System.StringComparison.OrdinalIgnoreCase);
    }

    IEnumerator EnsureMenuUiRoutine()
    {
        // Let Menu UI (Multiplayer button, Canvas) finish spawning.
        yield return null;
        yield return null;

        if (!IsMenuScene())
            yield break;

#if UNITY_2023_1_OR_NEWER
        var menu = Object.FindFirstObjectByType<BossFightsMenu>();
#else
        var menu = Object.FindObjectOfType<BossFightsMenu>();
#endif
        if (menu == null)
        {
            var host = new GameObject("BossFightsMenu");
            menu = host.AddComponent<BossFightsMenu>();
        }

        menu.SetupMenuUi();
    }
}

/// <summary>
/// Async scene loader with a simple loading overlay.
/// </summary>
public class FastSceneLoader : MonoBehaviour
{
    static FastSceneLoader instance;

    public static void Load(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError("FastSceneLoader: scene " + buildIndex + " is not in Build Settings!");
            return;
        }

        EnsureInstance().StartCoroutine(EnsureInstance().LoadRoutine(buildIndex));
    }

    public static void LoadByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("FastSceneLoader: empty scene name.");
            return;
        }

        int buildIndex = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
                continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                buildIndex = i;
                break;
            }
        }

        if (buildIndex < 0)
        {
            Debug.LogError("FastSceneLoader: scene '" + sceneName + "' is not in Build Settings!");
            return;
        }

        Load(buildIndex);
    }

    public static void ReloadCurrent()
    {
        Load(SceneManager.GetActiveScene().buildIndex);
    }

    static FastSceneLoader EnsureInstance()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("FastSceneLoader");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FastSceneLoader>();
        return instance;
    }

    GameObject overlay;
    Text progressText;
    Image barFill;

    IEnumerator LoadRoutine(int buildIndex)
    {
        ShowOverlay();
        try
        {
            // Avoid NetworkObject.OnDestroy NRE when leaving Menu with a live NetworkManager.
            yield return NetworkPlayCleanup.PrepareRoutine();

            AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex);
            if (op == null)
                yield break;

            // Keep forcing activation — Netcode SceneManager can leave loads stuck at 0.9.
            float waited = 0f;
            while (!op.isDone)
            {
                op.allowSceneActivation = true;
                SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                waited += Time.unscaledDeltaTime;
                if (waited > 12f && op.progress >= 0.9f)
                {
                    Debug.LogWarning("FastSceneLoader: scene activation stalled — forcing continue.");
                    break;
                }
                yield return null;
            }

            SetProgress(1f);
            yield return null;
            yield return null;
        }
        finally
        {
            HideOverlay();
        }
    }

    public static void ForceHideOverlay()
    {
        if (instance != null)
            instance.HideOverlay();

        var leftover = GameObject.Find("LoadingOverlay");
        if (leftover != null)
            leftover.SetActive(false);
    }

    void ShowOverlay()
    {
        if (overlay != null)
        {
            overlay.SetActive(true);
            SetProgress(0f);
            return;
        }

        overlay = new GameObject("LoadingOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(overlay);

        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(overlay.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(overlay.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.anchoredPosition = new Vector2(0f, 40f);
        labelRt.sizeDelta = new Vector2(800f, 60f);
        progressText = labelGo.GetComponent<Text>();
        progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (progressText.font == null)
            progressText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        progressText.fontSize = 36;
        progressText.fontStyle = FontStyle.Bold;
        progressText.alignment = TextAnchor.MiddleCenter;
        progressText.color = Color.white;
        progressText.text = "Loading... 0%";

        var barBg = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(overlay.transform, false);
        var barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.5f, 0.5f);
        barBgRt.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRt.anchoredPosition = new Vector2(0f, -30f);
        barBgRt.sizeDelta = new Vector2(600f, 24f);
        barBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);

        var bar = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(barBg.transform, false);
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.anchoredPosition = Vector2.zero;
        barRt.sizeDelta = Vector2.zero;
        barFill = bar.GetComponent<Image>();
        barFill.color = new Color(1f, 0.45f, 0.12f, 1f);
    }

    void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (progressText != null)
            progressText.text = "Loading... " + Mathf.RoundToInt(t * 100f) + "%";

        if (barFill != null)
        {
            var rt = barFill.rectTransform;
            rt.anchorMax = new Vector2(t, 1f);
            rt.sizeDelta = Vector2.zero;
        }
    }

    void HideOverlay()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }
}

