using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main Menu → 1v1 Arena → VS Player (existing multiplayer) / VS Bot (offline AI duel).
/// Replaces the top-level Multiplayer button.
/// </summary>
public class ArenaMenu : MonoBehaviour
{
    const string MenuSceneName = "Menu";
    const int CharacterSelectBuildIndex = 1;

    static ArenaMenuWatcher watcher;

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

        var go = new GameObject("ArenaMenuWatcher");
        Object.DontDestroyOnLoad(go);
        watcher = go.AddComponent<ArenaMenuWatcher>();
    }

    GameObject panelRoot;
    GameObject difficultyRoot;

    public void SetupMenuUi()
    {
        ArenaBotModeState.Disable();
        EnsureEventSystem();
        HideLegacyMenuDecor();
        BuildArenaButton();

        if (panelRoot != null)
            Destroy(panelRoot);
        if (difficultyRoot != null)
            Destroy(difficultyRoot);

        panelRoot = BuildArenaPanel();
        panelRoot.SetActive(false);

        difficultyRoot = BuildDifficultyPanel();
        difficultyRoot.SetActive(false);
    }

    /// <summary>
    /// Removes the old Screen.png panel + "Buster Bots" title so the video background shows cleanly.
    /// </summary>
    void HideLegacyMenuDecor()
    {
        // Object named "Menu" holds Screen.png + Buster Bots TMP child.
        var brandedPanel = BossFightsMenu.FindMenuButtonNamed("Menu");
        if (brandedPanel != null
            && brandedPanel.GetComponent<UnityEngine.UI.Image>() != null
            && brandedPanel.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) != null)
        {
            brandedPanel.SetActive(false);
        }

        // Any leftover Title text that isn't a real button label.
        var title = BossFightsMenu.FindMenuButtonNamed("Title");
        if (title != null)
        {
            var tmp = title.GetComponent<TMPro.TextMeshProUGUI>();
            var text = title.GetComponent<UnityEngine.UI.Text>();
            string value = tmp != null ? tmp.text : (text != null ? text.text : "");
            if (!string.IsNullOrEmpty(value)
                && (value.IndexOf("Buster", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf("Multiplayer", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // Only hide if it's not the Multiplayer button itself.
                if (title.GetComponent<UnityEngine.UI.Button>() == null)
                    title.SetActive(false);
            }
        }
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

    void BuildArenaButton()
    {
        var multiplayer = BossFightsMenu.FindMenuButtonNamed("Multiplayer");
        if (multiplayer == null)
        {
            Debug.LogError("ArenaMenu: could not find Multiplayer button to replace.");
            return;
        }

        // Hide top-level Multiplayer — it now lives under 1v1 Arena as VS Player.
        multiplayer.SetActive(false);

        var existing = GameObject.Find("Arena1v1");
        if (existing != null)
            Destroy(existing);

        Transform parent = multiplayer.transform.parent;
        GameObject arenaBtn = Instantiate(multiplayer, parent);
        arenaBtn.SetActive(true);
        arenaBtn.name = "Arena1v1";

        var arenaRt = arenaBtn.GetComponent<RectTransform>();
        var mpRt = multiplayer.GetComponent<RectTransform>();
        if (arenaRt != null && mpRt != null)
        {
            arenaRt.localRotation = Quaternion.identity;
            arenaRt.localScale = Vector3.one;
            arenaRt.anchorMin = mpRt.anchorMin;
            arenaRt.anchorMax = mpRt.anchorMax;
            arenaRt.pivot = mpRt.pivot;
            arenaRt.anchoredPosition = mpRt.anchoredPosition;
            arenaRt.sizeDelta = mpRt.sizeDelta;
        }

        var tmp = arenaBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = "1v1 Arena";
            tmp.rectTransform.localRotation = Quaternion.identity;
            tmp.rectTransform.localScale = Vector3.one;
        }

        var legacyText = arenaBtn.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = "1v1 Arena";
            legacyText.rectTransform.localRotation = Quaternion.identity;
            legacyText.rectTransform.localScale = Vector3.one;
        }

        var button = arenaBtn.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OpenArenaPanel);
        }
    }

    void OpenArenaPanel()
    {
        EnsureEventSystem();
        if (difficultyRoot != null)
            difficultyRoot.SetActive(false);
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    void CloseArenaPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (difficultyRoot != null)
            difficultyRoot.SetActive(false);
    }

    void OpenVsPlayer()
    {
        CloseArenaPanel();
        BossModeState.Disable();
        ArenaBotModeState.Disable();

#if UNITY_2023_1_OR_NEWER
        var ui = FindFirstObjectByType<MultiplayerUI>(FindObjectsInactive.Include);
#else
        var ui = FindObjectOfType<MultiplayerUI>(true);
#endif
        if (ui != null)
        {
            ui.OpenMultiplayerPanel();
            return;
        }

#if UNITY_2023_1_OR_NEWER
        var menu = FindFirstObjectByType<MainMenu>();
#else
        var menu = FindObjectOfType<MainMenu>();
#endif
        if (menu != null)
        {
            menu.Multiplayer();
            return;
        }

        Debug.LogWarning("ArenaMenu: MultiplayerUI not found for VS Player.");
    }

    void OpenVsBotDifficulty()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (difficultyRoot != null)
            difficultyRoot.SetActive(true);
    }

    void SelectDifficultyAndGo(DifficultyPreset difficulty)
    {
        Debug.Log("ArenaMenu: VS Bot difficulty " + difficulty + " → Character Select");
        ArenaBotModeState.Enable(difficulty);
        if (difficultyRoot != null)
            difficultyRoot.SetActive(false);
        FastSceneLoader.Load(CharacterSelectBuildIndex);
    }

    GameObject BuildArenaPanel()
    {
        var canvasGo = new GameObject("ArenaSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9210;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("ArenaSelectPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        panel.GetComponent<Image>().raycastTarget = true;

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 360f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.98f);

        CreateUiText(box.transform, "Title", "1V1 ARENA", 34, FontStyle.Bold,
            new Vector2(0f, 130f), new Vector2(520f, 50f));
        CreateUiText(box.transform, "Hint", "Fight another player online, or duel a bot offline.", 16, FontStyle.Normal,
            new Vector2(0f, 85f), new Vector2(520f, 36f));

        CreateColoredButton(box.transform, "VsPlayer", "VS Player", new Vector2(0f, 20f),
            new Vector2(360f, 56f), new Color(0.2f, 0.55f, 0.95f, 1f), OpenVsPlayer);
        CreateColoredButton(box.transform, "VsBot", "VS Bot", new Vector2(0f, -50f),
            new Vector2(360f, 56f), new Color(0.95f, 0.45f, 0.15f, 1f), OpenVsBotDifficulty);
        CreateSimpleButton(box.transform, "Back", new Vector2(0f, -130f), new Vector2(200f, 48f), CloseArenaPanel);

        return panel;
    }

    GameObject BuildDifficultyPanel()
    {
        var canvasGo = new GameObject("ArenaDifficultyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9220;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("ArenaDifficultyPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        panel.GetComponent<Image>().raycastTarget = true;

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 400f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.98f);

        CreateUiText(box.transform, "Title", "BOT DIFFICULTY", 34, FontStyle.Bold,
            new Vector2(0f, 150f), new Vector2(520f, 50f));
        CreateUiText(box.transform, "Hint", "Then pick your fighter. The bot picks another at random.", 15, FontStyle.Normal,
            new Vector2(0f, 105f), new Vector2(520f, 40f));

        CreateColoredButton(box.transform, "Easy", "Easy", new Vector2(0f, 40f),
            new Vector2(360f, 52f), new Color(0.25f, 0.7f, 0.35f, 1f),
            () => SelectDifficultyAndGo(DifficultyPreset.Easy));
        CreateColoredButton(box.transform, "Normal", "Normal", new Vector2(0f, -25f),
            new Vector2(360f, 52f), new Color(0.95f, 0.7f, 0.2f, 1f),
            () => SelectDifficultyAndGo(DifficultyPreset.Normal));
        CreateColoredButton(box.transform, "Chaotic", "Chaotic", new Vector2(0f, -90f),
            new Vector2(360f, 52f), new Color(0.9f, 0.25f, 0.2f, 1f),
            () => SelectDifficultyAndGo(DifficultyPreset.Chaotic));

        CreateSimpleButton(box.transform, "Back", new Vector2(0f, -160f), new Vector2(200f, 48f), () =>
        {
            if (difficultyRoot != null)
                difficultyRoot.SetActive(false);
            if (panelRoot != null)
                panelRoot.SetActive(true);
        });

        return panel;
    }

    void CreateColoredButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateUiText(go.transform, "Label", label, 24, FontStyle.Bold, Vector2.zero, size);
    }

    void CreateSimpleButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        CreateColoredButton(parent, label, label, pos, size, new Color(0.35f, 0.35f, 0.38f, 1f), onClick);
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
}

public class ArenaMenuWatcher : MonoBehaviour
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
        return name.Equals(MenuSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    const string MenuSceneName = "Menu";

    IEnumerator EnsureMenuUiRoutine()
    {
        yield return null;
        yield return null;

        if (!IsMenuScene())
            yield break;

#if UNITY_2023_1_OR_NEWER
        var menu = Object.FindFirstObjectByType<ArenaMenu>();
#else
        var menu = Object.FindObjectOfType<ArenaMenu>();
#endif
        if (menu == null)
        {
            var host = new GameObject("ArenaMenu");
            menu = host.AddComponent<ArenaMenu>();
        }

        menu.SetupMenuUi();
    }
}
