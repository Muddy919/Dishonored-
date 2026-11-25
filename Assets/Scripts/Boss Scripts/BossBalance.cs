using UnityEngine;

/// <summary>
/// Retunes boss AttributesManager (and a few attack fields) so bosses match
/// player HP (~100–250) and normal enemy HP (~300), instead of 10k–20k sponges.
/// Indexed by Boss Fights button 1..4 (Railway, Excavator, Front Guard, Brain Bot).
/// </summary>
public static class BossBalance
{
    // Tuned for: player ~150–250 HP, attack ~6–60, regular bots ~300 HP / ~12 dmg.
    struct BossTune
    {
        public int health;
        public int attack;
        public float cooldownMult;
    }

    static readonly BossTune[] ByBossButton =
    {
        new BossTune { health = 0, attack = 0, cooldownMult = 1f }, // unused index 0
        new BossTune { health = 3500, attack = 18, cooldownMult = 1.0f }, // Railway
        new BossTune { health = 3800, attack = 20, cooldownMult = 0.95f }, // Excavator
        new BossTune { health = 4200, attack = 22, cooldownMult = 0.9f }, // Front Guard
        new BossTune { health = 4500, attack = 24, cooldownMult = 0.85f }, // Brain Bot
    };

    public static void ApplyForCurrentBoss()
    {
        int boss = Mathf.Clamp(BossModeState.SelectedBossLevel, 1, 4);
        BossTune tune = ByBossButton[boss];

#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>(true);
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null || a.IsDead())
                continue;
            if (!LevelEnemyTracker.IsBossObject(a.gameObject))
                continue;

            // Skip tiny helper objects mistakenly under a Boss parent.
            if (a.health > 0 && a.health < 50 && a.GetComponentInParent<BossAttackLoop>() == null
                && a.GetComponentInParent<BossAI>() == null
                && a.GetComponentInParent<FrontGuardAI>() == null
                && a.GetComponentInParent<BrainBot>() == null)
                continue;

            a.health = tune.health;
            a.attack = tune.attack;
            if (a.healthBar != null)
            {
                a.healthBar.gameObject.SetActive(true);
                var canvas = a.healthBar.GetComponentInParent<Canvas>(true);
                if (canvas != null)
                    canvas.gameObject.SetActive(true);
                a.healthBar.SetMaxHealth(tune.health);
                a.healthBar.SetHealth(tune.health);
            }

            ApplyAttackCadence(a.gameObject, tune.cooldownMult);
            Debug.Log("BossBalance: " + a.gameObject.name + " → HP " + tune.health + ", ATK " + tune.attack);
        }
    }

    static void ApplyAttackCadence(GameObject go, float cooldownMult)
    {
        var loop = go.GetComponentInParent<BossAttackLoop>() ?? go.GetComponent<BossAttackLoop>();
        if (loop != null)
            loop.timeBetweenAttacks = Mathf.Max(0.8f, loop.timeBetweenAttacks * cooldownMult);

        var excav = go.GetComponentInParent<BossAI>() ?? go.GetComponent<BossAI>();
        if (excav != null)
            excav.attackCooldown = Mathf.Max(0.8f, excav.attackCooldown * cooldownMult);

        var guard = go.GetComponentInParent<FrontGuardAI>() ?? go.GetComponent<FrontGuardAI>();
        if (guard != null)
        {
            guard.fireRate = Mathf.Max(0.05f, guard.fireRate * Mathf.Lerp(1f, 1.15f, 1f - cooldownMult));
            guard.punchCooldown = Mathf.Max(1f, guard.punchCooldown * cooldownMult);
        }

        var brain = go.GetComponentInParent<BrainBot>() ?? go.GetComponent<BrainBot>();
        if (brain != null)
        {
            // Keep Brain Bot spamming projectiles — never slow it back up.
            brain.attackCooldown = Mathf.Min(0.45f, Mathf.Max(0.2f, brain.attackCooldown * cooldownMult));
            brain.growTime = Mathf.Min(0.25f, brain.growTime);
            brain.attackRange = Mathf.Max(80f, brain.attackRange);
            brain.projectileSpeed = Mathf.Max(22f, brain.projectileSpeed);
        }
    }
}
