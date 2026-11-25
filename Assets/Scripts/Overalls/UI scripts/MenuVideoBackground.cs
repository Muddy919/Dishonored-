using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays the robots fighting-game loop as a fullscreen Menu background.
/// Video lives in StreamingAssets so it always plays at runtime.
/// </summary>
public class MenuVideoBackground : MonoBehaviour
{
    const string MenuSceneName = "Menu";
    const string VideoFileName = "Robots_wiggling_in_fighting_game_a5465ef2.mp4";

    static MenuVideoBackground instance;

    VideoPlayer player;
    RawImage rawImage;
    RenderTexture target;
    GameObject canvasRoot;
    bool prepared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
        instance.OnSceneChanged();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        var host = new GameObject("MenuVideoBackground");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<MenuVideoBackground>();
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
        string name = SceneManager.GetActiveScene().name ?? "";
        if (name.Equals(MenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            StopAllCoroutines();
            StartCoroutine(SetupAndPlay());
        }
        else
        {
            Teardown();
        }
    }

    IEnumerator SetupAndPlay()
    {
        TeardownVisualsOnly();

        string path = Path.Combine(Application.streamingAssetsPath, VideoFileName);
        if (!File.Exists(path))
        {
            Debug.LogError("MenuVideoBackground: missing video at " + path);
            yield break;
        }

        // Background canvas behind the menu UI (menu Canvas sortingOrder = 0).
        canvasRoot = new GameObject("MenuVideoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -50;

        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Don't steal clicks from menu buttons.
        var raycaster = canvasRoot.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        var imageGo = new GameObject("VideoImage", typeof(RectTransform), typeof(RawImage));
        imageGo.transform.SetParent(canvasRoot.transform, false);
        var rt = imageGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        rawImage = imageGo.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        int w = Mathf.Max(1280, Screen.width);
        int h = Mathf.Max(720, Screen.height);
        target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        target.Create();
        rawImage.texture = target;

        player = canvasRoot.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = target;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        player.source = VideoSource.Url;
        // Unity wants a file:// URL on Windows for local StreamingAssets.
        player.url = new System.Uri(path).AbsoluteUri;

        prepared = false;
        player.prepareCompleted += OnPrepared;
        player.errorReceived += OnVideoError;
        player.Prepare();

        // Dim / hide solid menu backdrops so the video shows through.
        SoftenMenuBackdrop();

        float deadline = Time.unscaledTime + 8f;
        while (!prepared && player != null && Time.unscaledTime < deadline)
            yield return null;

        if (player == null)
            yield break;

        if (!prepared)
        {
            Debug.LogWarning("MenuVideoBackground: prepare timed out — forcing Play().");
        }

        player.Play();

        // Keep insisting for a moment in case the editor pauses preparation.
        for (int i = 0; i < 10; i++)
        {
            if (player == null)
                yield break;
            if (!player.isPlaying)
                player.Play();
            yield return null;
        }

        Debug.Log("MenuVideoBackground: playing " + VideoFileName
                  + " (isPlaying=" + player.isPlaying + ")");
    }

    void OnPrepared(VideoPlayer vp)
    {
        prepared = true;
        if (vp != null && !vp.isPlaying)
            vp.Play();
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("MenuVideoBackground: " + message);
    }

    static void SoftenMenuBackdrop()
    {
        // The static "Menu" splash Image sits on the UI canvas — hide it so the video shows.
        var menuSplash = GameObject.Find("Menu");
        if (menuSplash != null)
        {
            var splashImg = menuSplash.GetComponent<Image>();
            if (splashImg != null)
            {
                Color c = splashImg.color;
                c.a = 0f;
                splashImg.color = c;
                splashImg.raycastTarget = false;
            }
        }

#if UNITY_2023_1_OR_NEWER
        var images = Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var images = Object.FindObjectsOfType<Image>();
#endif
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;

            var rect = img.rectTransform;
            if (rect == null)
                continue;

            bool stretches =
                rect.anchorMin.x <= 0.01f && rect.anchorMin.y <= 0.01f
                && rect.anchorMax.x >= 0.99f && rect.anchorMax.y >= 0.99f;

            if (!stretches)
                continue;

            if (img.GetComponent<Button>() != null)
                continue;

            string n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("button") || (n.Contains("panel") && n.Contains("multi")))
                continue;

            if (img.transform.parent != null && img.transform.parent.name == "Canvas"
                || n == "background" || n == "bg" || n == "image" || n == "menu")
            {
                Color c = img.color;
                if (c.a > 0.05f)
                {
                    c.a = 0f;
                    img.color = c;
                }
                img.raycastTarget = false;
            }
        }

#if UNITY_2023_1_OR_NEWER
        var cam = Object.FindFirstObjectByType<Camera>();
#else
        var cam = Object.FindObjectOfType<Camera>();
#endif
        if (cam != null && cam.CompareTag("MainCamera"))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    void TeardownVisualsOnly()
    {
        if (player != null)
        {
            player.prepareCompleted -= OnPrepared;
            player.errorReceived -= OnVideoError;
            player.Stop();
            player = null;
        }

        if (canvasRoot != null)
        {
            Object.Destroy(canvasRoot);
            canvasRoot = null;
        }

        rawImage = null;

        if (target != null)
        {
            target.Release();
            Object.Destroy(target);
            target = null;
        }

        prepared = false;
    }

    void Teardown()
    {
        StopAllCoroutines();
        TeardownVisualsOnly();
    }

    void OnDestroy()
    {
        Teardown();
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        // If something pauses/stops the clip while on Menu, kick it again.
        if (player == null || canvasRoot == null)
            return;

        string name = SceneManager.GetActiveScene().name ?? "";
        if (!name.Equals(MenuSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (prepared && !player.isPlaying)
            player.Play();
    }
}
