using UnityEngine;

/// <summary>
/// Offline 1v1 Arena → VS Bot: Character Select → 1V1 Map with a local AI fighter.
/// </summary>
public static class ArenaBotModeState
{
    const string PrefActive = "BusterBots.ArenaBot.Active";
    const string PrefChar = "BusterBots.ArenaBot.Character";
    const string PrefBotChar = "BusterBots.ArenaBot.BotCharacter";
    const string PrefDiff = "BusterBots.ArenaBot.Difficulty";

    public const string BattleSceneName = "1V1 Map";

    static readonly string[] FighterNames =
    {
        "Pacer", "Buster", "Gastino", "Xebec", "Hendrix", "Bigbata", "Macky", "Rodeo", "Glamour"
    };

    public static bool IsActive { get; private set; }

    /// <summary>0..8 player fighter index.</summary>
    public static int SelectedCharacterIndex { get; private set; } = -1;

    /// <summary>0..8 bot fighter index (picked randomly, != player).</summary>
    public static int BotCharacterIndex { get; private set; } = -1;

    public static DifficultyPreset Difficulty { get; private set; } = DifficultyPreset.Normal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsActive = PlayerPrefs.GetInt(PrefActive, 0) == 1;
        SelectedCharacterIndex = PlayerPrefs.GetInt(PrefChar, -1);
        BotCharacterIndex = PlayerPrefs.GetInt(PrefBotChar, -1);
        Difficulty = (DifficultyPreset)Mathf.Clamp(PlayerPrefs.GetInt(PrefDiff, (int)DifficultyPreset.Normal), 0, 2);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RestoreAfterSceneLoad()
    {
        if (PlayerPrefs.GetInt(PrefActive, 0) != 1)
            return;

        IsActive = true;
        SelectedCharacterIndex = PlayerPrefs.GetInt(PrefChar, -1);
        BotCharacterIndex = PlayerPrefs.GetInt(PrefBotChar, -1);
        Difficulty = (DifficultyPreset)Mathf.Clamp(PlayerPrefs.GetInt(PrefDiff, (int)DifficultyPreset.Normal), 0, 2);
    }

    public static void Enable(DifficultyPreset difficulty)
    {
        BossModeState.Disable();
        Difficulty = difficulty;
        GameDifficulty.Current = difficulty;
        SelectedCharacterIndex = -1;
        BotCharacterIndex = -1;
        IsActive = true;
        PlayerPrefs.SetInt(PrefActive, 1);
        PlayerPrefs.SetInt(PrefDiff, (int)difficulty);
        PlayerPrefs.SetInt(PrefChar, -1);
        PlayerPrefs.SetInt(PrefBotChar, -1);
        PlayerPrefs.Save();
    }

    public static void SetSelectedCharacter(int characterIndex)
    {
        SelectedCharacterIndex = characterIndex;
        BotCharacterIndex = PickRandomBotCharacter(characterIndex);
        PlayerPrefs.SetInt(PrefChar, characterIndex);
        PlayerPrefs.SetInt(PrefBotChar, BotCharacterIndex);
        PlayerPrefs.Save();
    }

    public static void Disable()
    {
        IsActive = false;
        SelectedCharacterIndex = -1;
        BotCharacterIndex = -1;
        PlayerPrefs.SetInt(PrefActive, 0);
        PlayerPrefs.SetInt(PrefChar, -1);
        PlayerPrefs.SetInt(PrefBotChar, -1);
        PlayerPrefs.Save();
    }

    public static string GetSelectedFighterName()
    {
        int i = SelectedCharacterIndex;
        if (i < 0 || i >= FighterNames.Length)
            return null;
        return FighterNames[i];
    }

    public static string GetBotFighterName()
    {
        int i = BotCharacterIndex;
        if (i < 0 || i >= FighterNames.Length)
            return null;
        return FighterNames[i];
    }

    static int PickRandomBotCharacter(int playerIndex)
    {
        int pick = Random.Range(0, FighterNames.Length);
        if (FighterNames.Length <= 1)
            return pick;

        int guard = 0;
        while (pick == playerIndex && guard++ < 20)
            pick = Random.Range(0, FighterNames.Length);
        return pick;
    }
}
