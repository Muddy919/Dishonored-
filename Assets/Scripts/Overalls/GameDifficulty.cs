using UnityEngine;

public enum DifficultyPreset
{
    Easy = 0,
    Normal = 1,
    Chaotic = 2
}

/// <summary>
/// Campaign difficulty. Persists via PlayerPrefs; applied to player + enemies on level load.
/// </summary>
public static class GameDifficulty
{
    const string PrefKey = "BusterBots.Difficulty";

    public static DifficultyPreset Current
    {
        get => (DifficultyPreset)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)DifficultyPreset.Normal), 0, 2);
        set
        {
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string Label => Current switch
    {
        DifficultyPreset.Easy => "Easy",
        DifficultyPreset.Chaotic => "Chaotic",
        _ => "Normal"
    };

    public static float EnemyDamageMult => Current switch
    {
        DifficultyPreset.Easy => 0.55f,
        DifficultyPreset.Chaotic => 1.5f,
        _ => 1f
    };

    public static float EnemyHealthMult => Current switch
    {
        DifficultyPreset.Easy => 0.7f,
        DifficultyPreset.Chaotic => 1.25f,
        _ => 1f
    };

    public static float PlayerHealthMult => Current switch
    {
        DifficultyPreset.Easy => 1.5f,
        DifficultyPreset.Chaotic => 0.85f,
        _ => 1f
    };

    public static float EnemySpeedMult => Current switch
    {
        DifficultyPreset.Easy => 0.85f,
        DifficultyPreset.Chaotic => 1.25f,
        _ => 1f
    };

    public static float EnemyCooldownMult => Current switch
    {
        DifficultyPreset.Easy => 1.25f,
        DifficultyPreset.Chaotic => 0.65f,
        _ => 1f
    };

    /// <summary>Last-stand triggers sooner on Chaotic.</summary>
    public static float LastStandFraction => Current switch
    {
        DifficultyPreset.Easy => 0.12f,
        DifficultyPreset.Chaotic => 0.28f,
        _ => 0.18f
    };

    public static int LastStandCount => Current switch
    {
        DifficultyPreset.Easy => 2,
        DifficultyPreset.Chaotic => 5,
        _ => 3
    };

    public static void Cycle()
    {
        int next = ((int)Current + 1) % 3;
        Current = (DifficultyPreset)next;
    }

    public static void ApplyToEnemy(GameObject enemy)
    {
        if (enemy == null)
            return;

        var attrs = enemy.GetComponent<AttributesManager>();
        if (attrs != null && !attrs.CompareTag("Player"))
        {
            attrs.health = Mathf.Max(1, Mathf.RoundToInt(attrs.health * EnemyHealthMult));
            attrs.attack = Mathf.Max(1, Mathf.RoundToInt(attrs.attack * EnemyDamageMult));
            if (attrs.healthBar != null)
                attrs.healthBar.SetMaxHealth(attrs.health);
        }

        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.speed *= EnemySpeedMult;

        // Per-type attack fields
        var melee = enemy.GetComponent<MeleeEnemyAI>();
        if (melee != null)
        {
            melee.damage = Mathf.Max(1, Mathf.RoundToInt(melee.damage * EnemyDamageMult));
            melee.attackCooldown *= EnemyCooldownMult;
        }

        var mg = enemy.GetComponent<MachineGunEnemyAI>();
        if (mg != null)
        {
            mg.projectileDamage = Mathf.Max(1, Mathf.RoundToInt(mg.projectileDamage * EnemyDamageMult));
            mg.timeBetweenAttacks *= EnemyCooldownMult;
        }

        var sniper = enemy.GetComponent<SniperEnemyAI>();
        if (sniper != null)
        {
            sniper.projectileDamage = Mathf.Max(1, Mathf.RoundToInt(sniper.projectileDamage * EnemyDamageMult));
            sniper.timeBetweenAttacks *= EnemyCooldownMult;
        }

        var shotgun = enemy.GetComponent<ShotgunEnemyAI>();
        if (shotgun != null)
        {
            shotgun.pelletDamage = Mathf.Max(1, Mathf.RoundToInt(shotgun.pelletDamage * EnemyDamageMult));
            shotgun.timeBetweenAttacks *= EnemyCooldownMult;
        }

        var boom = enemy.GetComponent<ExplodingEnemyAI>();
        if (boom != null)
        {
            boom.explosionDamage = Mathf.Max(1, Mathf.RoundToInt(boom.explosionDamage * EnemyDamageMult));
            boom.deathExplosionDamage = Mathf.Max(1, Mathf.RoundToInt(boom.deathExplosionDamage * EnemyDamageMult));
            boom.moveSpeed *= EnemySpeedMult;
        }
    }

    public static void ApplyToPlayer(AttributesManager player)
    {
        if (player == null)
            return;

        int boosted = Mathf.Max(1, Mathf.RoundToInt(player.health * PlayerHealthMult));
        player.health = boosted;
        if (player.healthBar != null)
            player.healthBar.SetMaxHealth(boosted);
    }
}
