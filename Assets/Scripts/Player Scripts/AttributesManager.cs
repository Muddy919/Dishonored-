using UnityEngine;

public class AttributesManager : MonoBehaviour
{
    [Header("Attributes")]
    public int health = 100;
    public int attack = 15;

    public HealthBar healthBar;

    private bool isDead = false;

    [Header("Fall Death")]
    public float deathYLevel = -20f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip deathSound;

    [Header("Death Icon")]
    public GameObject deathIconPrefab;
    public float deathIconHeight = 2f;
    public float deathIconLifetime = 3f;

    private void Start()
    {
        if (healthBar != null)
            healthBar.SetMaxHealth(health);
    }

    private void Update()
    {
        if (isDead) return;

        // In multiplayer, PlayerHealth owns fall-death for networked rounds.
        bool inMp = Unity.Netcode.NetworkManager.Singleton != null
                    && Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (GetComponent<PlayerHealth>() != null && inMp)
            return;

        if (transform.position.y <= deathYLevel)
        {
            health = 0;
            Die();
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        health -= amount;

        if (health < 0)
            health = 0;

        Debug.Log(gameObject.name + " Health: " + health);

        if (healthBar != null)
            healthBar.SetHealth(health);

        // Player hurt juice; enemies flinch/stagger (Projectile may call Apply first for better hit dir).
        if (CompareTag("Player"))
        {
            CombatFeel.OnPlayerHurt(amount);
        }
        else
        {
            EnemyFlinch.Apply(gameObject, transform.position - transform.forward * 0.5f, amount);
            if (LevelEnemyTracker.IsBossObject(gameObject))
                CancelBossPhysicsKnock();
            var tactics = GetComponent<EnemyTactics>();
            if (tactics != null)
                tactics.NotifyDamaged();
        }

        if (health <= 0)
            Die();
    }

    public void DealDamage(GameObject target)
    {
        DealDamage(target, attack);
    }

    public void DealDamage(GameObject target, int damageAmount)
    {
        if (isDead || target == null)
            return;

        AttributesManager targetAttributes = target.GetComponent<AttributesManager>();

        if (targetAttributes != null)
        {
            targetAttributes.TakeDamage(damageAmount);
        }
    }

    public void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log(gameObject.name + " died");

        // Player death: quick restart overlay (skip death-screen hopping in campaign).
        if (CompareTag("Player") || GetComponent<PlayerHealth>() != null)
        {
            // Offline arena uses ArenaRoundController win/lose UI instead.
            if (ArenaBotModeState.IsActive)
                return;

            // Solo campaign only — networked fights use RoundManager.
            bool inMp = Unity.Netcode.NetworkManager.Singleton != null
                        && Unity.Netcode.NetworkManager.Singleton.IsListening;
            if (!inMp)
                GameplayDeathOverlay.Show();
            return;
        }

        // Offline arena bot: freeze in place; ArenaRoundController shows the result.
        if (ArenaBotModeState.IsActive && GetComponent<ArenaFighterBotAI>() != null)
        {
            ArenaRoundController.NotifyBotDied();
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            return;
        }

        // Encounter pacing + ammo loot (skip bosses).
        if (!LevelEnemyTracker.IsBossObject(gameObject))
        {
            EnemyEncounterDirector.NotifyEnemyDied(transform.position);
        }

        // Spawn death icon
        if (deathIconPrefab != null)
        {
            GameObject icon = Instantiate(
                deathIconPrefab,
                transform.position + Vector3.up * deathIconHeight,
                Quaternion.identity
            );

            Destroy(icon, deathIconLifetime);
        }

        // Play death sound
        if (audioSource != null && deathSound != null)
            audioSource.PlayOneShot(deathSound);

        // Shotgun Enemy
        ShotgunEnemyAI shotgun = GetComponent<ShotgunEnemyAI>();
        if (shotgun != null)
        {
            shotgun.PlayRandomDeathAnimation();
            Destroy(gameObject, 3f);
            return;
        }

        // Sniper Enemy
        SniperEnemyAI sniper = GetComponent<SniperEnemyAI>();
        if (sniper != null)
        {
            sniper.PlayRandomDeathAnimation();
            Destroy(gameObject, 3f);
            return;
        }

        // Machine Gun Enemy
        MachineGunEnemyAI machineGun = GetComponent<MachineGunEnemyAI>();
        if (machineGun != null)
        {
            machineGun.PlayRandomDeathAnimation();
            Destroy(gameObject, 3f);
            return;
        }

        // Melee Enemy
        MeleeEnemyAI melee = GetComponent<MeleeEnemyAI>();
        if (melee != null)
        {
            melee.PlayRandomDeathAnimation();
            Destroy(gameObject, 3f);
            return;
        }

        // Default destruction - skipped for networked fighters (see Update()
        // above for why); RoundManager handles their respawn instead.
        if (GetComponent<PlayerHealth>() == null)
            Destroy(gameObject, 0.2f);
    }

    public bool IsDead()
    {
        return isDead;
    }

    void CancelBossPhysicsKnock()
    {
        var rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null || rbs[i].isKinematic)
                continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
        }
    }
}