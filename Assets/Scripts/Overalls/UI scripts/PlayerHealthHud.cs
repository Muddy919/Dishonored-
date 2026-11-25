using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime player health bar for fighter prefabs that ship with healthBar = null.
/// Survives Menu → Character Select → Boss/Level loads (unlike a one-shot AfterSceneLoad).
/// </summary>
public class PlayerHealthHud : MonoBehaviour
{
    static PlayerHealthHud instance;

    AttributesManager attrs;
    Image fill;
    Text label;
    bool built;
    int maxHealth = 100;
    bool difficultyApplied;
    GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
        instance.OnSceneChanged();
    }

    public static void Ensure()
    {
        if (instance != null)
            return;

        var host = new GameObject("PlayerHealthHud");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<PlayerHealthHud>();
    }

    /// <summary>Call after Boss Fights spawns the fighter so the bar binds immediately.</summary>
    public static void RebindToPlayer()
    {
        Ensure();
        instance.difficultyApplied = false;
        instance.attrs = null;
        instance.BindPlayer(forceMaxFromCurrent: true);
        if (!instance.built)
            instance.BuildUi();
        instance.ApplyDifficultyOnce();
        instance.SetVisible(LevelEnemyTracker.IsGameplayLevelScene());
        instance.Refresh();
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
        OnSceneChanged();
    }

    void OnSceneChanged()
    {
        difficultyApplied = false;
        attrs = null;

        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
            SetVisible(false);
            return;
        }

        if (!built)
            BuildUi();

        SetVisible(true);
        BindPlayer(forceMaxFromCurrent: true);
        ApplyDifficultyOnce();
        Refresh();
    }

    void Start()
    {
        OnSceneChanged();
    }

    void SetVisible(bool on)
    {
        if (root != null)
            root.SetActive(on);
        else if (transform.childCount > 0)
            transform.GetChild(0).gameObject.SetActive(on);
    }

    void ApplyDifficultyOnce()
    {
        if (difficultyApplied || attrs == null)
            return;

        GameDifficulty.ApplyToPlayer(attrs);

        // Boss Fights: player starts at half HP against every boss.
        if (BossModeState.IsActive)
        {
            int half = Mathf.Max(1, attrs.health / 2);
            attrs.health = half;
            if (attrs.healthBar != null)
                attrs.healthBar.SetMaxHealth(half);
        }

        maxHealth = Mathf.Max(1, attrs.health);
        if (attrs.healthBar != null)
            attrs.healthBar.SetMaxHealth(maxHealth);
        difficultyApplied = true;
    }

    void BindPlayer(bool forceMaxFromCurrent = false)
    {
        Transform player = EnemySmart.FindPlayer();
        if (player == null)
        {
            try
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }
            catch { /* ignore */ }
        }

        if (player == null)
            return;

        attrs = player.GetComponent<AttributesManager>()
                ?? player.GetComponentInParent<AttributesManager>()
                ?? player.GetComponentInChildren<AttributesManager>();

        if (attrs == null)
            return;

        if (forceMaxFromCurrent || maxHealth < attrs.health)
            maxHealth = Mathf.Max(1, attrs.health);

        // Prefer the screen HUD — hide old world/prefab health bars (they stack on top).
        if (attrs.healthBar != null)
        {
            attrs.healthBar.gameObject.SetActive(false);
            var canvas = attrs.healthBar.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvas.gameObject.SetActive(false);
        }
    }

    void BuildUi()
    {
        if (built)
            return;

        root = new GameObject("PlayerHealthCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5100;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("HealthPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(36f, 36f);
        rt.sizeDelta = new Vector2(320f, 70f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        panel.GetComponent<Image>().raycastTarget = false;

        label = CreateText(panel.transform, "HPLabel", "HP 100 / 100", 22, FontStyle.Bold,
            new Vector2(0f, 14f), new Vector2(300f, 28f), Color.white);

        var barBg = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(panel.transform, false);
        var bgRt = barBg.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = new Vector2(0f, -14f);
        bgRt.sizeDelta = new Vector2(280f, 18f);
        barBg.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 1f);
        barBg.GetComponent<Image>().raycastTarget = false;

        var barFill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        barFill.transform.SetParent(barBg.transform, false);
        var fillRt = barFill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill = barFill.GetComponent<Image>();
        fill.color = new Color(0.25f, 0.9f, 0.4f, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        built = true;
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
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    void Refresh()
    {
        if (attrs == null || fill == null)
            return;

        maxHealth = Mathf.Max(maxHealth, 1);
        int current = Mathf.Clamp(attrs.health, 0, Mathf.Max(maxHealth, attrs.health));
        if (attrs.health > maxHealth)
            maxHealth = attrs.health;

        float pct = Mathf.Clamp01(current / (float)maxHealth);
        fill.fillAmount = pct;

        if (pct > 0.55f)
            fill.color = new Color(0.25f, 0.9f, 0.4f, 1f);
        else if (pct > 0.25f)
            fill.color = new Color(1f, 0.75f, 0.2f, 1f);
        else
            fill.color = new Color(1f, 0.3f, 0.25f, 1f);

        if (label != null)
            label.text = "HP  " + current + " / " + maxHealth;

        if (attrs.healthBar != null)
            attrs.healthBar.SetHealth(attrs.health);
    }

    void Update()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
            SetVisible(false);
            return;
        }

        if (!built)
            BuildUi();

        SetVisible(true);

        if (attrs == null || attrs.IsDead())
            BindPlayer(forceMaxFromCurrent: attrs == null);

        if (!difficultyApplied)
            ApplyDifficultyOnce();

        Refresh();
    }
}
