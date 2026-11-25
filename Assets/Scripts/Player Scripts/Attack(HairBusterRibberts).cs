using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Attack : MonoBehaviour
{
    [Header("References")]
    public LayerMask enemyLayer;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    private AttributesManager attributes;
    private Animator animator;

    // =========================
    // SLASH
    // =========================
    [Header("Slash")]
    public float slashRange = 2f;
    public float slashCooldown = 0.8f;
    private float nextSlashTime;

    // =========================
    // THROW
    // =========================
    [Header("Throw")]
    public float throwForce = 20f;
    public float throwCooldown = 0.5f;
    private float nextThrowTime;

    // =========================
    // KICK
    // =========================
    [Header("Kick")]
    public float kickRange = 1.5f;
    public float kickCooldown = 1f;
    private float nextKickTime;

    /// <summary>When true, mouse/keyboard input is ignored — Arena bot drives attacks.</summary>
    public bool BotControlled { get; set; }

    /// <summary>Optional aim point for bot throws (look-at target).</summary>
    public Transform BotAimTarget { get; set; }

    private void Awake()
    {
        attributes = GetComponent<AttributesManager>();
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError("Animator missing!");
    }

    private void Update()
    {
        HandleInput();
    }

    // =========================
    // INPUT
    // =========================
    void HandleInput()
    {
        if (BotControlled)
            return;

        if (PauseMenu.IsPaused || GameplayDeathOverlay.IsOpen)
            return;

        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryThrow();

        if (Mouse.current.rightButton.wasPressedThisFrame)
            TrySlash();

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            TryKick();
    }

    public void BotTryThrow() => TryThrow();
    public void BotTrySlash() => TrySlash();
    public void BotTryKick() => TryKick();

    // =========================
    // ANIMATION CONTROL
    // =========================
    void SetAttacking(bool value)
    {
        if (animator != null)
            animator.SetBool("IsAttacking", value);
    }

    void StopAttacking()
    {
        SetAttacking(false);
    }

    // =========================
    // SLASH
    // =========================
    void TrySlash()
    {
        if (Time.time < nextSlashTime) return;

        nextSlashTime = Time.time + slashCooldown;

        SetAttacking(true);
        animator.SetTrigger("Slash");

        Invoke(nameof(StopAttacking), 0.5f);

        Collider[] hits = Physics.OverlapSphere(transform.position, slashRange, MeleeHitMask());

        ApplyMeleeHits(hits, attributes.attack);
    }

    // =========================
    // THROW
    // =========================
    void TryThrow()
    {
        if (Time.time < nextThrowTime) return;
        if (projectilePrefab == null || projectileSpawnPoint == null) return;

        nextThrowTime = Time.time + throwCooldown;

        SetAttacking(true);
        animator.SetTrigger("Throw");

        Invoke(nameof(StopAttacking), 0.5f);

        Quaternion aimRot = projectileSpawnPoint.rotation;

        if (BotControlled && BotAimTarget != null)
        {
            Vector3 aimPoint = BotAimTarget.position + Vector3.up * 1.2f;
            var botAi = GetComponent<ArenaFighterBotAI>();
            if (botAi != null)
                aimPoint = botAi.GetAimPoint();

            Vector3 toAim = aimPoint - projectileSpawnPoint.position;
            if (toAim.sqrMagnitude > 0.0001f)
                aimRot = Quaternion.LookRotation(toAim.normalized);
        }
        else
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Vector3 aimPoint = ray.GetPoint(200f);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                    aimPoint = hit.point;

                Vector3 toAim = aimPoint - projectileSpawnPoint.position;
                if (toAim.sqrMagnitude > 0.0001f)
                    aimRot = Quaternion.LookRotation(toAim.normalized);
            }
        }

        Vector3 origin = projectileSpawnPoint.position;
        Vector3 dir = aimRot * Vector3.forward;

        bool inMp = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        var netHealth = GetComponent<PlayerHealth>();
        bool useNetwork = inMp && netHealth != null && netHealth.IsOwner;

        GameObject proj = Instantiate(projectilePrefab, origin, aimRot);

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = dir * throwForce;
        }

        Projectile p = proj.GetComponent<Projectile>();
        if (p != null)
        {
            p.SetOwnerRoot(transform);
            if (useNetwork)
                p.SetVisualOnly(true);
            else
                p.SetDamage(attributes.attack);
        }

        if (useNetwork)
            netHealth.RequestThrowServerRpc(origin, dir, throwForce, attributes.attack);
    }

    // =========================
    // KICK
    // =========================
    void TryKick()
    {
        if (Time.time < nextKickTime) return;

        nextKickTime = Time.time + kickCooldown;

        SetAttacking(true);
        animator.SetTrigger("Kick");

        Invoke(nameof(StopAttacking), 0.5f);

        Collider[] hits = Physics.OverlapSphere(transform.position, kickRange, MeleeHitMask());

        ApplyMeleeHits(hits, Mathf.RoundToInt(attributes.attack * 1.5f));
    }

    int MeleeHitMask()
    {
        int mask = enemyLayer.value;
        // Ensure multiplayer fighters (whatIsPlayer / Player layers) are hittable.
        int a = LayerMask.NameToLayer("whatIsPlayer");
        int b = LayerMask.NameToLayer("Player");
        if (a >= 0) mask |= 1 << a;
        if (b >= 0) mask |= 1 << b;
        if (mask == 0) mask = ~0;
        return mask;
    }

    void ApplyMeleeHits(Collider[] hits, int damageAmount)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            // Multiplayer fighters.
            PlayerHealth netHealth = hit.GetComponentInParent<PlayerHealth>();
            if (netHealth != null && netHealth.gameObject != gameObject)
            {
                netHealth.TakeDamageServerRpc(damageAmount);
                CombatFeel.OnDealtDamage(hit.transform.position, false);
                continue;
            }

            AttributesManager enemy = hit.GetComponentInParent<AttributesManager>();
            if (enemy == null || enemy.gameObject == gameObject)
                continue;

            // Campaign: don't AttributesManager-damage other fighters in MP.
            // Arena VS Bot: allow Player ↔ bot damage via AttributesManager.
            bool isFighter = enemy.CompareTag("Player") || enemy.GetComponent<PlayerHealth>() != null;
            if (isFighter && !ArenaBotModeState.IsActive)
                continue;

            bool wasAlive = !enemy.IsDead();
            enemy.TakeDamage(damageAmount);
            CombatFeel.OnDealtDamage(hit.transform.position, wasAlive && enemy.IsDead());
        }
    }

    // =========================
    // DEBUG VISUALS
    // =========================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slashRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, kickRange);
    }
}