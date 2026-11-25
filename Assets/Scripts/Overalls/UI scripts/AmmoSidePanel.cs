using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ammo HUD — only while inside a real gameplay level.
/// </summary>
public class AmmoSidePanel : MonoBehaviour
{
    AttackPacerLongLegs attack;
    Text ammoText;
    Text statusText;
    Image fill;
    GameObject panelRoot;
    bool uiBuilt;

    public static void EnsureFor(AttackPacerLongLegs owner)
    {
        if (owner == null)
            return;

        if (!LevelEnemyTracker.IsGameplayLevelScene())
            return;

#if UNITY_2023_1_OR_NEWER
        var existing = FindFirstObjectByType<AmmoSidePanel>();
#else
        var existing = FindObjectOfType<AmmoSidePanel>();
#endif
        if (existing == null)
        {
            var host = new GameObject("AmmoSidePanel");
            existing = host.AddComponent<AmmoSidePanel>();
        }

        existing.attack = owner;
        if (!existing.uiBuilt)
            existing.BuildUi();

        existing.SetVisible(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // Never create ammo UI on menus / select / intros.
        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
#if UNITY_2023_1_OR_NEWER
            var leftovers = FindObjectsByType<AmmoSidePanel>(FindObjectsSortMode.None);
#else
            var leftovers = FindObjectsOfType<AmmoSidePanel>();
#endif
            for (int i = 0; i < leftovers.Length; i++)
            {
                if (leftovers[i] != null)
                    Object.Destroy(leftovers[i].gameObject);
            }
            return;
        }

#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<AmmoSidePanel>() != null)
            return;
#else
        if (FindObjectOfType<AmmoSidePanel>() != null)
            return;
#endif

        var host = new GameObject("AmmoSidePanel");
        host.AddComponent<AmmoSidePanel>();
    }

    void Start()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
            Destroy(gameObject);
            return;
        }

        if (!uiBuilt)
            BuildUi();
        ResolveAttack();
        SetVisible(true);
    }

    void ResolveAttack()
    {
        if (attack != null)
            return;

#if UNITY_2023_1_OR_NEWER
        attack = FindFirstObjectByType<AttackPacerLongLegs>();
#else
        attack = FindObjectOfType<AttackPacerLongLegs>();
#endif
    }

    void SetVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    void BuildUi()
    {
        if (uiBuilt)
            return;

        var canvasGo = new GameObject(
            "AmmoOverlayCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("AmmoPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvasGo.transform, false);

        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-36f, 36f);
        rt.sizeDelta = new Vector2(240f, 160f);

        var bg = panelRoot.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        bg.raycastTarget = false;

        CreateText(panelRoot.transform, "Title", "AMMO", 26, FontStyle.Bold,
            new Vector2(0f, 52f), new Vector2(220f, 34f), new Color(1f, 0.6f, 0.2f, 1f));

        ammoText = CreateText(panelRoot.transform, "Count", "-- / --", 40, FontStyle.Bold,
            new Vector2(0f, 8f), new Vector2(220f, 48f), Color.white);

        statusText = CreateText(panelRoot.transform, "Status", "WAITING...", 20, FontStyle.Normal,
            new Vector2(0f, -40f), new Vector2(220f, 30f), new Color(0.8f, 0.85f, 0.9f, 1f));

        var barBg = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(panelRoot.transform, false);
        var barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRt.anchoredPosition = new Vector2(0f, -68f);
        barBgRt.sizeDelta = new Vector2(190f, 16f);
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
        fill.color = new Color(1f, 0.55f, 0.15f, 1f);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;

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
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    void Update()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
        {
            Destroy(gameObject);
            return;
        }

        if (!uiBuilt)
            BuildUi();

        if (attack == null)
            ResolveAttack();

        if (attack == null)
        {
            if (ammoText != null) ammoText.text = "-- / --";
            if (statusText != null) statusText.text = "NO PLAYER";
            return;
        }

        int cur = attack.CurrentShots;
        int max = Mathf.Max(1, attack.MaxShots);

        if (ammoText != null)
            ammoText.text = cur + " / " + max;

        if (fill != null)
            fill.fillAmount = cur / (float)max;

        if (statusText != null)
        {
            if (attack.IsReloading)
            {
                statusText.text = "RELOADING...";
                statusText.color = new Color(1f, 0.75f, 0.2f, 1f);
            }
            else if (cur <= 0)
            {
                statusText.text = "EMPTY - press R";
                statusText.color = new Color(1f, 0.35f, 0.25f, 1f);
            }
            else if (cur <= Mathf.Max(1, max / 4))
            {
                statusText.text = "LOW";
                statusText.color = new Color(1f, 0.55f, 0.2f, 1f);
            }
            else
            {
                statusText.text = "READY";
                statusText.color = new Color(0.6f, 1f, 0.65f, 1f);
            }
        }
    }
}
