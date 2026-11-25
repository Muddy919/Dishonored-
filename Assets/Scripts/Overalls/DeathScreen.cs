using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // =========================
    // LOAD PREVIOUS SCENE
    // =========================
    public void LoadPreviousScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        FastSceneLoader.Load(currentIndex - 1);
    }

    // =========================
    // LOAD SPECIFIC SCENE
    // =========================
    public void LoadSceneByIndex(int sceneIndex)
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();
        SceneManager.LoadScene(0);
    }
}