using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Assigns ambush / flank / retreat roles on level load and pushes
/// remaining enemies into last-stand aggression so clears don't drag.
/// </summary>
public class EnemyEncounterDirector : MonoBehaviour
{
    static EnemyEncounterDirector instance;

    int initialCount = -1;
    bool lastStandTriggered;
    bool setupDoneForScene;
    float nextPollAt;
    string setupScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject("EnemyEncounterDirector");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<EnemyEncounterDirector>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        StartCoroutine(SetupNextFrame());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastStandTriggered = false;
        initialCount = -1;
        setupDoneForScene = false;
        setupScene = scene.name;
        StartCoroutine(SetupNextFrame());
    }

    IEnumerator SetupNextFrame()
    {
        yield return null;
        yield return null;

        if (!LevelEnemyTracker.IsGameplayLevelScene())
            yield break;

        if (ArenaBotModeState.IsActive)
            yield break;

        string scene = SceneManager.GetActiveScene().name;
        if (setupDoneForScene && setupScene == scene)
            yield break;

        setupDoneForScene = true;
        setupScene = scene;

        // Ensure AI targets the fighter root, not a nested "Pacer" mesh tagged Player.
        EnemySmart.InvalidatePlayerCache();
        EnemySmart.FindPlayer();

        AssignRoles();
        ApplyDifficultyToAll();
        initialCount = LevelEnemyTracker.CountAliveEnemiesUncached();
        LevelEnemyTracker.InvalidateCache();
    }

    void ApplyDifficultyToAll()
    {
        LevelEnemyTracker.ForEachAliveEnemy(t =>
        {
            if (t != null)
                GameDifficulty.ApplyToEnemy(t.gameObject);
        });
    }

    void Update()
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
            return;

        if (ArenaBotModeState.IsActive)
            return;

        if (Time.unscaledTime < nextPollAt)
            return;

        nextPollAt = Time.unscaledTime + 0.4f;
        MaybeTriggerLastStand();
    }

    void AssignRoles()
    {
        int index = 0;
        LevelEnemyTracker.ForEachAliveEnemy(t =>
        {
            if (t == null)
                return;

            var tactics = EnemyTactics.Ensure(t.gameObject);
            EncounterRole role = PickRole(index, t.gameObject);
            float reveal = Random.Range(7.5f, 11f);
            tactics.Configure(role, reveal);
            index++;
        });
    }

    static EncounterRole PickRole(int index, GameObject go)
    {
        // Weighted mix so Level 2 isn't a wall of identical chasers.
        // Ambush: hold until player is close / they take damage
        // Flank: wider strafe combat points
        // Retreat: peel off once at low HP, then re-engage
        int bucket = index % 10;
        if (bucket <= 2)
            return EncounterRole.Ambush;
        if (bucket <= 5)
            return EncounterRole.Flank;
        if (bucket <= 7)
            return EncounterRole.Retreat;

        // Snipers lean flank; melee lean ambush/rush slightly via name.
        string n = go.name;
        if (n.IndexOf("Sniper", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return EncounterRole.Flank;
        if (n.IndexOf("Meele", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Melee", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return bucket % 2 == 0 ? EncounterRole.Ambush : EncounterRole.Standard;

        return EncounterRole.Standard;
    }

    void MaybeTriggerLastStand()
    {
        if (lastStandTriggered)
            return;

        int alive = LevelEnemyTracker.CountAliveEnemies();
        if (alive <= 0)
            return;

        if (initialCount < 0)
            initialCount = alive;

        bool fewLeft = alive <= GameDifficulty.LastStandCount;
        bool fractionLeft = initialCount >= 8
            && alive <= Mathf.Max(2, Mathf.CeilToInt(initialCount * GameDifficulty.LastStandFraction));

        if (!fewLeft && !fractionLeft)
            return;

        lastStandTriggered = true;
        LevelEnemyTracker.ForEachAliveEnemy(t =>
        {
            if (t == null)
                return;
            EnemyTactics.Ensure(t.gameObject).EnterLastStand();
        });
    }

    public static void NotifyEnemyDied(Vector3 deathPos)
    {
        LevelEnemyTracker.InvalidateCache();
        AmmoPickup.TrySpawnDrop(deathPos);

        if (instance != null)
            instance.MaybeTriggerLastStand();
    }
}
