using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Counts living combat enemies. Does NOT destroy or relocate enemies.
/// </summary>
public static class LevelEnemyTracker
{
    static int cachedCount;
    static float nextCountAt = -1f;
    const float CountInterval = 0.35f;

    public static bool IsMultiplayerBattleScene(string sceneName = null)
    {
        if (string.IsNullOrEmpty(sceneName))
            sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            return false;
        return sceneName.IndexOf("1V1", System.StringComparison.OrdinalIgnoreCase) >= 0
               || sceneName.IndexOf("1v1", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsGameplayLevelScene(string sceneName = null)
    {
        if (string.IsNullOrEmpty(sceneName))
            sceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(sceneName))
            return false;

        // Offline 1v1 Arena uses the multiplayer battle map with local fighters.
        if (ArenaBotModeState.IsActive && IsMultiplayerBattleScene(sceneName))
            return true;

        if (!sceneName.StartsWith("Level"))
            return false;

        if (sceneName.Contains("Intro")
            || sceneName.Contains("Death")
            || sceneName.Contains("Select")
            || sceneName.Contains("MissionOver")
            || sceneName == "LevelSelect")
            return false;

        return true;
    }

    public static bool ShouldShowHud()
    {
        string n = SceneManager.GetActiveScene().name ?? "";
        string l = n.ToLowerInvariant();
        if (l.Contains("menu") || l.Contains("select") || l.Contains("intro")
            || l.Contains("death") || l.Contains("mission") || l.Contains("character"))
            return false;

        return IsGameplayLevelScene(n);
    }

    public static bool IsBossObject(GameObject go)
    {
        if (go == null)
            return false;

        Transform t = go.transform;
        while (t != null)
        {
            string name = t.name;
            if (name.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Excavator", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("FrontGuard", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("BrainBot", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("RailWay", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Railway", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (t.GetComponent<BossAI>() != null
                || t.GetComponent<FrontGuardAI>() != null
                || t.GetComponent<BrainBot>() != null
                || t.GetComponent<BossAttackLoop>() != null)
                return true;

            t = t.parent;
        }

        return false;
    }

    public static bool IsCombatEnemy(GameObject go)
    {
        if (go == null || !go.activeInHierarchy)
            return false;

        if (IsBossObject(go))
            return false;

        return go.GetComponent<MeleeEnemyAI>() != null
               || go.GetComponent<MachineGunEnemyAI>() != null
               || go.GetComponent<ShotgunEnemyAI>() != null
               || go.GetComponent<SniperEnemyAI>() != null
               || go.GetComponent<DroneEnemyAI>() != null
               || go.GetComponent<ExplodingEnemyAI>() != null;
    }

    public static void InvalidateCache()
    {
        nextCountAt = -1f;
    }

    public static int CountAliveEnemies()
    {
        if (Time.unscaledTime < nextCountAt)
            return cachedCount;

        nextCountAt = Time.unscaledTime + CountInterval;
        cachedCount = CountAliveEnemiesUncached();
        return cachedCount;
    }

    public static int CountAliveEnemiesUncached()
    {
        int count = 0;
        ForEachAliveEnemy(_ => count++);
        return count;
    }

    public static void ForEachAliveEnemy(System.Action<Transform> action)
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>();
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null || a.IsDead())
                continue;

            // Prefer AI-tagged combat enemies, but fall back to any living
            // non-player AttributesManager so counts never go empty by mistake.
            GameObject go = a.gameObject;
            if (go.CompareTag("Player") || a.GetComponent<PlayerHealth>() != null)
                continue;

            if (IsBossObject(go))
                continue;

            bool combat = IsCombatEnemy(go);
            if (!combat)
            {
                // Keep older enemy prefabs that only have AttributesManager.
                // Skip obvious non-enemies (no collider / tiny helpers).
                if (go.GetComponentInChildren<Collider>() == null)
                    continue;
                if (go.name.IndexOf("Enemy", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Meele", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Melee", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Sniper", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Shotgun", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Machine", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Drone", System.StringComparison.OrdinalIgnoreCase) < 0
                    && go.name.IndexOf("Explod", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            action(go.transform);
        }
    }

    public static Transform GetNearestEnemy(Vector3 from, out float distance)
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        ForEachAliveEnemy(t =>
        {
            float d = Vector3.Distance(from, t.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        });

        distance = best != null ? bestDist : 0f;
        return best;
    }
}
