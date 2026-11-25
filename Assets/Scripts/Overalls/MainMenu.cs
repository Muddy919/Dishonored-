using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();
        FastSceneLoader.Load(1);
    }

    public void BossFights()
    {
        // Prefer the on-screen Boss Fights picker (BossFightsMenu).
        // Fallback: open character select for boss 1 if the picker isn't present.
        BossModeState.Enable(1);
        FastSceneLoader.Load(1);
    }

    public void QuitGame()
    {
        Debug.Log("QUIT");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Multiplayer()
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();

        // Open the Host/Join panel — do NOT load a campaign intro scene.
#if UNITY_2023_1_OR_NEWER
        var ui = FindFirstObjectByType<MultiplayerUI>();
#else
        var ui = FindObjectOfType<MultiplayerUI>();
#endif
        if (ui != null)
        {
            ui.OpenMultiplayerPanel();
            return;
        }

        Debug.LogWarning("MainMenu.Multiplayer: MultiplayerUI panel not found on Menu.");
    }
}