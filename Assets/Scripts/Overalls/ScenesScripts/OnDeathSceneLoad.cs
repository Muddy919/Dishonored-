using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnDeath : MonoBehaviour
{
    [Header("References")]
    public AttributesManager attributesManager;

    [Header("Scene Settings")]
    [Tooltip("Build Index of the scene to load. Ignored for player deaths — GameplayDeathOverlay handles those.")]
    public int sceneNumber = 0;

    private bool sceneLoaded = false;

    private void Update()
    {
        if (sceneLoaded || attributesManager == null)
            return;

        // Player deaths use the quick-restart overlay instead of hopping death screens.
        if (attributesManager.CompareTag("Player")
            || attributesManager.GetComponent<PlayerHealth>() != null)
            return;

        if (attributesManager.health <= 0)
        {
            sceneLoaded = true;

            // Main-menu Boss Fights only: go straight back to Menu.
            // Campaign single-player keeps its normal next-scene flow.
            if (BossModeState.IsActive)
            {
                BossModeState.Disable();
                FastSceneLoader.Load(0);
                return;
            }

            if (sceneNumber >= 0 && sceneNumber < SceneManager.sceneCountInBuildSettings)
                FastSceneLoader.Load(sceneNumber);
            else
                Debug.LogError("Scene " + sceneNumber + " is not in Build Settings!");
        }
    }
}
