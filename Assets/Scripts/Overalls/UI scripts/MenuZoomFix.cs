using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Forces main-menu / intro / select UI to stay readable.
/// Runs every frame on Menu so editor in-memory scene state can't leave buttons tilted.
/// </summary>
public class MenuZoomFix : MonoBehaviour
{
    float nextMenuPass;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<MenuZoomFix>() != null)
            return;
#else
        if (FindObjectOfType<MenuZoomFix>() != null)
            return;
#endif

        var host = new GameObject("MenuZoomFix");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<MenuZoomFix>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
        StartCoroutine(ApplyDelayed());
    }

    IEnumerator ApplyDelayed()
    {
        yield return null;
        Apply();
        yield return null;
        Apply();
    }

    void LateUpdate()
    {
        string n = SceneManager.GetActiveScene().name ?? "";
        if (!n.Equals("Menu", System.StringComparison.OrdinalIgnoreCase))
            return;

        if (Time.unscaledTime < nextMenuPass)
            return;

        nextMenuPass = Time.unscaledTime + 0.25f;
        FixMainMenu();
    }

    static void Apply()
    {
        string n = SceneManager.GetActiveScene().name ?? "";
        string l = n.ToLowerInvariant();

        // Always wipe leftover letterbox junk from older builds.
        var bars = GameObject.Find("LetterboxBars");
        if (bars != null)
            Object.Destroy(bars);

        if (l == "menu")
        {
            FixMainMenu();
            return;
        }

        if (l.Contains("select"))
        {
            SetScalers(new Vector2(800f, 600f), 0.5f);
            return;
        }

        if (l.Contains("intro"))
            FixIntroScreens();
    }

    // Far-left empty wall on the menu video — clear of the robots.
    const float MenuColX = -760f;

    static void FixMainMenu()
    {
        SetScalers(new Vector2(1920f, 1080f), 0.5f);
        FixMenuBackground();

        FixMenuButton("PLayButton", "Singleplayer", new Vector2(MenuColX, -140f), new Vector2(420f, 88f));
        // Multiplayer is hidden; 1v1 Arena replaces it at the same slot.
        FixMenuButton("Multiplayer", "Multiplayer", new Vector2(MenuColX, -250f), new Vector2(420f, 88f));
        FixMenuButton("Arena1v1", "1v1 Arena", new Vector2(MenuColX, -250f), new Vector2(420f, 88f));
        FixMenuButton("BossFights", "Boss Fights", new Vector2(MenuColX, -360f), new Vector2(420f, 88f));
        FixMenuButton("Exit", "Exit", new Vector2(MenuColX, -470f), new Vector2(300f, 88f));
        EnsureExitButtonWorks();
    }

    static void EnsureExitButtonWorks()
    {
        var exit = GameObject.Find("Exit");
        if (exit == null)
            return;

        var button = exit.GetComponent<Button>();
        if (button == null)
            return;

#if UNITY_2023_1_OR_NEWER
        var menu = Object.FindFirstObjectByType<MainMenu>();
#else
        var menu = Object.FindObjectOfType<MainMenu>();
#endif
        if (menu == null)
        {
            var host = GameObject.Find("MainMenu");
            if (host == null)
                host = new GameObject("MainMenu");
            menu = host.AddComponent<MainMenu>();
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(menu.QuitGame);
        button.interactable = true;
    }

    static void FixMenuBackground()
    {
        var menu = GameObject.Find("Menu");
        if (menu == null)
            return;

        var rt = menu.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1920f, 1080f);
        }

        var img = menu.GetComponent<Image>();
        if (img != null)
        {
            // Video background owns the backdrop — keep this Image transparent.
            img.preserveAspect = true;
            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.raycastTarget = false;
        }
    }

    static void FixMenuButton(string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = GameObject.Find(name);
        if (go == null)
            return;

        // Kill animation / layout that could re-tilt.
        var anim = go.GetComponent<Animator>();
        if (anim != null)
            anim.enabled = false;

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            return;

        rt.localRotation = Quaternion.identity;
        rt.localEulerAngles = Vector3.zero;
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.ForceUpdateRectTransforms();

        // World rotation must also be upright (parent shear safety).
        go.transform.rotation = Quaternion.identity;

        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            if (!string.IsNullOrEmpty(label))
                tmp.text = label;

            tmp.rectTransform.localRotation = Quaternion.identity;
            tmp.rectTransform.localScale = Vector3.one;
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 30f;
            tmp.fontSizeMax = 52f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterHorizontalScale = 1f;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.transform.rotation = Quaternion.identity;
        }

        var legacy = go.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            if (!string.IsNullOrEmpty(label))
                legacy.text = label;
            legacy.rectTransform.localRotation = Quaternion.identity;
            legacy.rectTransform.localScale = Vector3.one;
            legacy.alignment = TextAnchor.MiddleCenter;
        }
    }

    static void FixIntroScreens()
    {
        SetScalers(new Vector2(1920f, 1080f), 0.5f);

#if UNITY_2023_1_OR_NEWER
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var buttons = Object.FindObjectsOfType<Button>(true);
#endif
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null)
                continue;

            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null || label.text == null)
                continue;
            if (label.text.IndexOf("Continue", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var rt = b.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 110f);
                rt.anchoredPosition = new Vector2(0f, -360f);
            }

            label.rectTransform.localRotation = Quaternion.identity;
            label.rectTransform.localScale = Vector3.one;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 56f;
            label.alignment = TextAlignmentOptions.Center;
        }

#if UNITY_2023_1_OR_NEWER
        var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var texts = Object.FindObjectsOfType<TextMeshProUGUI>(true);
#endif
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI t = texts[i];
            if (t == null || string.IsNullOrEmpty(t.text) || t.text.Length < 40)
                continue;
            if (t.text.IndexOf("Continue", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            t.rectTransform.localRotation = Quaternion.identity;
            t.rectTransform.localScale = Vector3.one;
            t.rectTransform.sizeDelta = new Vector2(1100f, 480f);
            t.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 28f;
            t.fontSizeMax = 48f;
            t.enableWordWrapping = true;
            t.alignment = TextAlignmentOptions.Center;
        }

        var levelScreen = GameObject.Find("LevelScreen");
        if (levelScreen != null)
        {
            var rt = levelScreen.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1920f, 1080f);
                rt.anchoredPosition = Vector2.zero;
            }
        }
    }

    static void SetScalers(Vector2 reference, float match)
    {
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

            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = reference;
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = match;
        }
    }
}
