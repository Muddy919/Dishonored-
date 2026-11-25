using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Forces true borderless fullscreen at native resolution, and keeps the game
/// alive when Windows screenshot tools / overlays steal focus.
/// Alt+Enter toggles fullscreen.
/// </summary>
public class DisplayModeBootstrap : MonoBehaviour
{
    static DisplayModeBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("DisplayModeBootstrap");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<DisplayModeBootstrap>();
        ApplyBorderlessFullscreen();
    }

    void Update()
    {
        bool altEnterNew =
            Keyboard.current != null
            && Keyboard.current.altKey.isPressed
            && Keyboard.current.enterKey.wasPressedThisFrame;

        bool altEnterLegacy =
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            && Input.GetKeyDown(KeyCode.Return);

        if (altEnterNew || altEnterLegacy)
            ToggleFullscreen();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // Screenshot / Snipping Tool / Game Bar steal focus briefly.
        // Never leave Time.timeScale stuck; only restore cursor when focus returns
        // during actual gameplay (not menus / death overlays).
        if (!hasFocus)
            return;

        if (PauseMenu.IsPaused || GameplayDeathOverlay.IsOpen || ArenaRoundController.IsFinished)
            return;

        // Soft restore — avoid fighting Character Select / Menu cursors.
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
        if (scene.Equals("Menu", System.StringComparison.OrdinalIgnoreCase)
            || scene.IndexOf("Select", System.StringComparison.OrdinalIgnoreCase) >= 0
            || scene.IndexOf("Intro", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void ApplyBorderlessFullscreen()
    {
        Resolution native = Screen.currentResolution;
        // FullScreenWindow = borderless (no title-bar min/max/close). Prefer this
        // over ExclusiveFullScreen so Windows screenshots don't hang the swapchain.
        Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        Screen.fullScreen = true;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
    }

    static void ToggleFullscreen()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            ApplyBorderlessFullscreen();
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }
    }
}
