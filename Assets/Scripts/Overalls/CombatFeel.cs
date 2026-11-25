using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Punchier combat juice: hit markers, muzzle flashes, hurt flash,
/// procedural hit/hurt SFX, and light camera punch. Does not touch camera enable/disable.
/// </summary>
public class CombatFeel : MonoBehaviour
{
    static CombatFeel instance;

    AudioSource sfx;
    AudioClip hitClip;
    AudioClip hurtClip;
    AudioClip meatyShotClip;

    Canvas canvas;
    Image hitMarker;
    Image hurtFlash;
    float hitMarkerUntil;
    float hurtFlashAlpha;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject("CombatFeel");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<CombatFeel>();
    }

    void Awake()
    {
        instance = this;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.volume = 1f;

        hitClip = MakeHitClip();
        hurtClip = MakeHurtClip();
        meatyShotClip = MakeMeatyShotClip();

        BuildUi();
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
        // Re-hook player attacks after load.
        StartCoroutine(BoostGunsNextFrame());
    }

    IEnumerator BoostGunsNextFrame()
    {
        yield return null;
        BoostAllPlayerGuns();
    }

    void Start()
    {
        BoostAllPlayerGuns();
    }

    void Update()
    {
        if (hitMarker != null)
        {
            bool show = Time.unscaledTime < hitMarkerUntil;
            hitMarker.enabled = show;
            if (show)
            {
                float t = Mathf.Clamp01((hitMarkerUntil - Time.unscaledTime) / 0.18f);
                var c = hitMarker.color;
                c.a = t;
                hitMarker.color = c;
                hitMarker.rectTransform.localScale = Vector3.one * (1.15f - 0.25f * (1f - t));
            }
        }

        if (hurtFlash != null)
        {
            hurtFlashAlpha = Mathf.MoveTowards(hurtFlashAlpha, 0f, Time.unscaledDeltaTime * 2.8f);
            var c = hurtFlash.color;
            c.a = hurtFlashAlpha;
            hurtFlash.color = c;
            hurtFlash.enabled = hurtFlashAlpha > 0.01f;
        }
    }

    static void BoostAllPlayerGuns()
    {
#if UNITY_2023_1_OR_NEWER
        var guns = Object.FindObjectsByType<AttackPacerLongLegs>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var guns = Object.FindObjectsOfType<AttackPacerLongLegs>();
#endif
        for (int i = 0; i < guns.Length; i++)
        {
            if (guns[i] == null || !guns[i].enabled)
                continue;

            // In MP, only tune the local owner's gun.
            var net = guns[i].GetComponentInParent<Unity.Netcode.NetworkObject>();
            if (net != null && net.IsSpawned && !net.IsOwner)
                continue;

            guns[i].ApplyCombatFeelTuning();
        }
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("CombatFeelCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Full-screen red hurt flash
        var hurtGo = new GameObject("HurtFlash", typeof(RectTransform), typeof(Image));
        hurtGo.transform.SetParent(canvasGo.transform, false);
        var hurtRt = hurtGo.GetComponent<RectTransform>();
        hurtRt.anchorMin = Vector2.zero;
        hurtRt.anchorMax = Vector2.one;
        hurtRt.offsetMin = Vector2.zero;
        hurtRt.offsetMax = Vector2.zero;
        hurtFlash = hurtGo.GetComponent<Image>();
        hurtFlash.color = new Color(0.85f, 0.05f, 0.05f, 0f);
        hurtFlash.raycastTarget = false;
        hurtFlash.enabled = false;

        // Center hit marker (simple X)
        var markerGo = new GameObject("HitMarker", typeof(RectTransform), typeof(Image));
        markerGo.transform.SetParent(canvasGo.transform, false);
        var markerRt = markerGo.GetComponent<RectTransform>();
        markerRt.anchorMin = markerRt.anchorMax = new Vector2(0.5f, 0.5f);
        markerRt.sizeDelta = new Vector2(28f, 28f);
        markerRt.anchoredPosition = Vector2.zero;
        hitMarker = markerGo.GetComponent<Image>();
        hitMarker.sprite = MakeXSprite();
        hitMarker.color = new Color(1f, 1f, 1f, 0f);
        hitMarker.raycastTarget = false;
        hitMarker.enabled = false;
    }

    static Sprite MakeXSprite()
    {
        const int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = Color.white;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, clear);

        for (int i = 2; i < s - 2; i++)
        {
            SetThick(tex, i, i, white);
            SetThick(tex, i, s - 1 - i, white);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
    }

    static void SetThick(Texture2D tex, int x, int y, Color c)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx;
                int yy = y + dy;
                if (xx >= 0 && yy >= 0 && xx < tex.width && yy < tex.height)
                    tex.SetPixel(xx, yy, c);
            }
    }

    // -------- Public API --------

    public static void OnPlayerShot(Transform muzzle)
    {
        Ensure();
        instance.PlayShotPunch();
        if (muzzle != null)
            instance.SpawnMuzzleFlash(muzzle);
    }

    public static void OnDealtDamage(Vector3 worldPos, bool killed)
    {
        Ensure();
        instance.ShowHitMarker(killed);
        instance.PlayHitSound(killed);
        instance.LightShake(killed ? 0.22f : 0.12f, killed ? 0.18f : 0.1f);
    }

    public static void OnPlayerHurt(int amount)
    {
        Ensure();
        instance.FlashHurt(Mathf.Clamp01(amount / 40f) * 0.55f + 0.2f);
        instance.PlayHurtSound();
        instance.LightShake(0.28f, 0.2f);
    }

    static void Ensure()
    {
        if (instance == null)
            Bootstrap();
    }

    void ShowHitMarker(bool killed)
    {
        if (hitMarker == null)
            return;

        hitMarkerUntil = Time.unscaledTime + (killed ? 0.28f : 0.16f);
        hitMarker.color = killed ? new Color(1f, 0.25f, 0.2f, 1f) : new Color(1f, 1f, 1f, 1f);
        hitMarker.enabled = true;
    }

    void FlashHurt(float strength)
    {
        hurtFlashAlpha = Mathf.Max(hurtFlashAlpha, Mathf.Clamp01(strength));
    }

    void PlayShotPunch()
    {
        if (sfx != null && meatyShotClip != null)
            sfx.PlayOneShot(meatyShotClip, 0.55f);
    }

    void PlayHitSound(bool killed)
    {
        if (sfx != null && hitClip != null)
            sfx.PlayOneShot(hitClip, killed ? 0.85f : 0.65f);
    }

    void PlayHurtSound()
    {
        if (sfx != null && hurtClip != null)
            sfx.PlayOneShot(hurtClip, 0.8f);
    }

    void LightShake(float magnitude, float duration)
    {
        if (CameraShakeWhileRunning.instance != null)
        {
            // Reuse heavy shake path with temporary values.
            var shaker = CameraShakeWhileRunning.instance;
            float oldMag = shaker.heavyShakeMagnitude;
            float oldDur = shaker.heavyShakeDuration;
            shaker.heavyShakeMagnitude = magnitude;
            shaker.heavyShakeDuration = duration;
            shaker.HeavyAttackShake();
            // Restore defaults after a beat so later calls aren't stuck high.
            StartCoroutine(RestoreShakeDefaults(shaker, oldMag, oldDur, duration + 0.05f));
            return;
        }

        // Fallback punch if the runner shaker isn't on this camera.
        Camera cam = Camera.main;
        if (cam != null)
            StartCoroutine(FallbackCamPunch(cam.transform, magnitude, duration));
    }

    IEnumerator FallbackCamPunch(Transform cam, float magnitude, float duration)
    {
        Vector3 origin = cam.localPosition;
        float t = 0f;
        while (t < duration && cam != null)
        {
            t += Time.unscaledDeltaTime;
            float falloff = 1f - (t / duration);
            cam.localPosition = origin + Random.insideUnitSphere * (magnitude * falloff);
            yield return null;
        }
        if (cam != null)
            cam.localPosition = origin;
    }

    IEnumerator RestoreShakeDefaults(CameraShakeWhileRunning shaker, float mag, float dur, float wait)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (shaker != null)
        {
            shaker.heavyShakeMagnitude = mag;
            shaker.heavyShakeDuration = dur;
        }
    }

    void SpawnMuzzleFlash(Transform muzzle)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MuzzleFlash";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = muzzle.position + muzzle.forward * 0.15f;
        go.transform.localScale = Vector3.one * Random.Range(0.18f, 0.32f);

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                   ?? Shader.Find("Unlit/Color")
                                   ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.85f, 0.35f, 1f);
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        // Point light punch
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 4f;
        light.intensity = 3.5f;
        light.color = new Color(1f, 0.7f, 0.25f);

        Object.Destroy(go, 0.06f);
    }

    // -------- Procedural audio --------

    static AudioClip MakeHitClip()
    {
        return BuildClip("CombatHit", 0.09f, (t, life) =>
        {
            float e = Mathf.Exp(-28f * t);
            float thump = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.55f;
            float crack = Mathf.Sin(2f * Mathf.PI * 920f * t) * 0.25f * Mathf.Exp(-50f * t);
            float noise = (Random.value * 2f - 1f) * 0.2f * Mathf.Exp(-40f * t);
            return (thump + crack + noise) * e;
        });
    }

    static AudioClip MakeHurtClip()
    {
        return BuildClip("CombatHurt", 0.14f, (t, life) =>
        {
            float e = Mathf.Exp(-12f * t);
            float low = Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.6f;
            float mid = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f * Mathf.Exp(-18f * t);
            float noise = (Random.value * 2f - 1f) * 0.18f;
            return (low + mid + noise) * e;
        });
    }

    static AudioClip MakeMeatyShotClip()
    {
        return BuildClip("CombatShotPunch", 0.11f, (t, life) =>
        {
            float e = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-22f * t);
            float boom = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.5f;
            float snap = Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.3f * Mathf.Exp(-35f * t);
            float noise = (Random.value * 2f - 1f) * 0.22f * Mathf.Exp(-30f * t);
            return (boom + snap + noise) * e;
        });
    }

    static AudioClip BuildClip(string name, float seconds, System.Func<float, float, float> sampleFn)
    {
        int rate = 44100;
        int count = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Clamp(sampleFn(t, seconds), -1f, 1f);
        }

        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
