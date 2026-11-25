using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonLoader : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Build Index of the scene to load (single player only).")]
    public int sceneNumber = 0;

    [Header("Multiplayer Settings - only set these on the 9 character buttons")]
    [Tooltip("This character's index. MUST match this character's position in " +
             "BattleSpawner's 'Character Prefabs' list (0 = first entry, etc). " +
             "Leave at -1 on any button that isn't a character-select button " +
             "(e.g. a Back button using this same script).")]
    public int characterIndex = -1;

    [Tooltip("Drag the CharacterSelectNetwork component from this screen here. " +
             "Only needed on buttons with a characterIndex set - if left empty, " +
             "it's found automatically at runtime.")]
    public CharacterSelectNetwork multiplayerSelect;

    private bool InMultiplayerSession =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // Called by the UI Button - same method name, so your existing OnClick()
    // bindings on all 9 buttons keep working without touching them.
    public void LoadScene()
    {
        // Leaving boss / arena mode when returning to the main menu.
        if (sceneNumber == 0)
        {
            BossModeState.Disable();
            ArenaBotModeState.Disable();
        }

        // Campaign Play → Character Select (not boss / arena mode).
        if (sceneNumber == 1 && characterIndex < 0)
        {
            BossModeState.Disable();
            ArenaBotModeState.Disable();
        }

        if (InMultiplayerSession && characterIndex >= 0)
        {
            if (multiplayerSelect == null)
#if UNITY_2023_1_OR_NEWER
                multiplayerSelect = FindFirstObjectByType<CharacterSelectNetwork>();
#else
                multiplayerSelect = FindObjectOfType<CharacterSelectNetwork>();
#endif

            if (multiplayerSelect == null)
            {
                Debug.LogError("No CharacterSelectNetwork found in the scene - " +
                                "can't submit character choice for multiplayer.");
                return;
            }

            // One click both picks this character and marks the player ready,
            // matching your existing one-click-to-proceed flow.
            multiplayerSelect.SubmitChoice(characterIndex);
            multiplayerSelect.SetReady();
            return;
        }

        // Boss Fights: remember fighter, then jump straight into that boss level.
        if (BossModeState.IsActive && characterIndex >= 0)
        {
            BossModeState.SetSelectedCharacter(characterIndex);

            int bossScene = BossModeState.GetBossSceneIndex(sceneNumber);
            if (bossScene >= 0 && bossScene < SceneManager.sceneCountInBuildSettings)
            {
                Debug.Log("BossMode: fighter " + characterIndex
                          + " → boss " + BossModeState.SelectedBossLevel
                          + " (campaign L" + BossModeState.GetCampaignLevel() + ")"
                          + " scene " + bossScene);
                FastSceneLoader.Load(bossScene);
                return;
            }

            Debug.LogError("Boss scene " + bossScene + " is not in Build Settings!");
            return;
        }

        // 1v1 Arena VS Bot: remember fighter, bot picks another, load battle map.
        if (ArenaBotModeState.IsActive && characterIndex >= 0)
        {
            ArenaBotModeState.SetSelectedCharacter(characterIndex);
            Debug.Log("ArenaBot: fighter " + characterIndex
                      + " vs bot " + ArenaBotModeState.BotCharacterIndex
                      + " (" + ArenaBotModeState.Difficulty + ")");
            FastSceneLoader.LoadByName(ArenaBotModeState.BattleSceneName);
            return;
        }

        // Single player: async load so big levels don't freeze the editor/game.
        if (sceneNumber >= 0 && sceneNumber < SceneManager.sceneCountInBuildSettings)
        {
            FastSceneLoader.Load(sceneNumber);
        }
        else
        {
            Debug.LogError("Scene " + sceneNumber + " is not in Build Settings!");
        }
    }
}
