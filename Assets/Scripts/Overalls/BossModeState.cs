using UnityEngine;

/// <summary>
/// Boss Fights flow: pick boss → Character Select → load that boss arena with the chosen fighter.
/// Persists across scene loads via PlayerPrefs.
/// </summary>
public static class BossModeState
{
    const string PrefActive = "BusterBots.BossMode.Active";
    const string PrefBoss = "BusterBots.BossMode.Boss";
    const string PrefChar = "BusterBots.BossMode.Character";

    // Fixed Pacer campaign Level build indices (boss arenas live here).
    const int PacerLevel1 = 4;
    const int PacerLevel2 = 7;
    const int PacerLevel3 = 10;
    const int PacerLevel5 = 16;

    public static bool IsActive { get; private set; }

    /// <summary>1..4 matching the four boss-fight buttons.</summary>
    public static int SelectedBossLevel { get; private set; } = 1;

    /// <summary>0..8 matching Character Select / BattleSpawner fighter order.</summary>
    public static int SelectedCharacterIndex { get; private set; } = -1;

    static readonly string[] FighterNames =
    {
        "Pacer", "Buster", "Gastino", "Xebec", "Hendrix", "Bigbata", "Macky", "Rodeo", "Glamour"
    };

    /// <summary>
    /// Boss button 1..4 → campaign Level 1, 2, 3, 5 (Brain Bot). Level 4 has no boss arena.
    /// </summary>
    public static int GetCampaignLevel()
    {
        return SelectedBossLevel switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 5,
            _ => 1
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsActive = PlayerPrefs.GetInt(PrefActive, 0) == 1;
        SelectedBossLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefBoss, 1), 1, 4);
        SelectedCharacterIndex = PlayerPrefs.GetInt(PrefChar, -1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RestoreAfterSceneLoad()
    {
        if (PlayerPrefs.GetInt(PrefActive, 0) != 1)
            return;

        IsActive = true;
        SelectedBossLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefBoss, 1), 1, 4);
        SelectedCharacterIndex = PlayerPrefs.GetInt(PrefChar, -1);
    }

    public static void Enable(int bossLevel)
    {
        ArenaBotModeState.Disable();
        SelectedBossLevel = Mathf.Clamp(bossLevel, 1, 4);
        SelectedCharacterIndex = -1;
        IsActive = true;
        PlayerPrefs.SetInt(PrefActive, 1);
        PlayerPrefs.SetInt(PrefBoss, SelectedBossLevel);
        PlayerPrefs.SetInt(PrefChar, -1);
        PlayerPrefs.Save();
    }

    public static void SetSelectedCharacter(int characterIndex)
    {
        SelectedCharacterIndex = characterIndex;
        PlayerPrefs.SetInt(PrefChar, characterIndex);
        PlayerPrefs.Save();
    }

    public static void Disable()
    {
        IsActive = false;
        SelectedBossLevel = 1;
        SelectedCharacterIndex = -1;
        PlayerPrefs.SetInt(PrefActive, 0);
        PlayerPrefs.SetInt(PrefChar, -1);
        PlayerPrefs.Save();
    }

    public static string GetSelectedFighterName()
    {
        int i = SelectedCharacterIndex;
        if (i < 0 || i >= FighterNames.Length)
            return null;
        return FighterNames[i];
    }

    /// <summary>
    /// Always loads the Pacer boss-arena scenes (build indices 4/7/10/16).
    /// Character choice only picks which fighter prefab to spawn — not which level file.
    /// </summary>
    public static int GetBossSceneIndex(int levelSelectBuildIndex)
    {
        return GetCampaignLevel() switch
        {
            1 => PacerLevel1,
            2 => PacerLevel2,
            3 => PacerLevel3,
            5 => PacerLevel5,
            _ => PacerLevel1
        };
    }
}
