using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen-space boss HP bar. World-space boss bars often break in Boss Fights
/// when the level Main Camera is disabled and the fighter cam takes over.
/// </summary>
public class BossHealthHud : MonoBehaviour
{
    static BossHealthHud instance;

    AttributesManager boss;
    Image fill;
    Text label;
    bool built;
    int maxHealth = 1;
    GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
        instance.RefreshBinding();
    }

    public static void Ensure()
    {
        if (instance != null)
            return;

        var host = new GameObject("BossHealthHud");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<BossHealthHud>();
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
        boss = null;
        RefreshBinding();
    }

    void LateUpdate()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
            SetVisible(false);
            return;
        }

        if (boss == null || boss.IsDead() || boss.health <= 0)
            RefreshBinding();

        if (boss == null)
        {
            SetVisible(false);
            return;
        }

        if (!built)
            BuildUi();

        SetVisible(true);
        RebindWorldSpaceBars();
        Refresh();
    }

    void RefreshBinding()
    {
        boss = FindLivingBoss();
        if (boss != null)
            maxHealth = Mathf.Max(1, boss.health);
    }

    static AttributesManager FindLivingBoss()
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>();
#endif
        AttributesManager best = null;
        int bestHp = -1;
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null || a.IsDead() || a.health <= 0)
                continue;
            if (!LevelEnemyTracker.IsBossObject(a.gameObject))
                continue;
            if (a.CompareTag("Player") || a.GetComponent<PlayerHealth>() != null)
                continue;

            if (a.health > bestHp)
            {
                bestHp = a.health;
                best = a;
            }
        }

        return best;
    }

    void BuildUi()
    {
        if (built)
            return;
        built = true;

        root = new GameObject("BossHealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5050;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -28f);
        panelRt.sizeDelta = new Vector2(720f, 36f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(panel.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = new Vector2(4f, 4f);
        fillRt.offsetMax = new Vector2(-4f, -4f);
        fill = fillGo.GetComponent<Image>();
        fill.color = new Color(0.85f, 0.2f, 0.15f, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(panel.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        label = labelGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "BOSS";
    }

    void Refresh()
    {
        if (boss == null || fill == null)
            return;

        if (boss.healthBar != null && boss.healthBar.slider != null)
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(boss.healthBar.slider.maxValue));
        else
            maxHealth = Mathf.Max(maxHealth, boss.health, 1);

        float t = Mathf.Clamp01(boss.health / (float)maxHealth);
        fill.fillAmount = t;
        fill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.55f, 0.15f), t);

        if (label != null)
            label.text = "BOSS  " + boss.health + " / " + maxHealth;

        // Keep the prefab bar in sync too when present.
        if (boss.healthBar != null)
            boss.healthBar.SetHealth(boss.health);
    }

    void SetVisible(bool on)
    {
        if (root != null)
            root.SetActive(on);
    }

    /// <summary>
    /// Point world-space boss HealthBar canvases at the live fighter camera.
    /// </summary>
    public static void RebindWorldSpaceBars()
    {
        Camera cam = FindActiveGameplayCamera();
        if (cam == null)
            return;

#if UNITY_2023_1_OR_NEWER
        var bars = Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var bars = Object.FindObjectsOfType<HealthBar>(true);
#endif
        for (int i = 0; i < bars.Length; i++)
        {
            HealthBar bar = bars[i];
            if (bar == null)
                continue;

            var attrs = bar.GetComponentInParent<AttributesManager>();
            if (attrs == null || !LevelEnemyTracker.IsBossObject(attrs.gameObject))
                continue;

            bar.gameObject.SetActive(true);
            var canvas = bar.GetComponentInParent<Canvas>();
            if (canvas == null)
                continue;

            canvas.gameObject.SetActive(true);
            if (canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = cam;
        }
    }

    static Camera FindActiveGameplayCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.enabled && main.gameObject.activeInHierarchy)
            return main;

        Transform player = EnemySmart.FindPlayer();
        if (player != null)
        {
            var cams = player.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].enabled)
                    return cams[i];
            }
        }

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled && all[i].GetComponentInParent<Canvas>() == null)
                return all[i];
        }

        return null;
    }
}
