using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Authoritative multiplayer health + gunfire. Singleplayer still uses AttributesManager.
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private HealthBar healthBar;

    [Tooltip("Fall off the arena = instant round loss.")]
    [SerializeField] private float deathYLevel = -20f;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(100);

    AttackPacerLongLegs cachedGun;
    Attack cachedMelee;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            CurrentHealth.Value = maxHealth;

        cachedGun = GetComponentInChildren<AttackPacerLongLegs>(true);
        cachedMelee = GetComponentInChildren<Attack>(true);

        EnsureHealthBar();
        CurrentHealth.OnValueChanged += OnHealthChanged;
        OnHealthChanged(0, CurrentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void EnsureHealthBar()
    {
        if (healthBar != null || BattleUIRefs.Instance == null)
            return;

        healthBar = OwnerClientId == NetworkManager.ServerClientId
            ? BattleUIRefs.Instance.hostHealthBar
            : BattleUIRefs.Instance.clientHealthBar;

        if (healthBar != null)
            healthBar.SetMaxHealth(maxHealth);
    }

    private void Update()
    {
        if (!IsServer)
            return;
        if (CurrentHealth.Value <= 0)
            return;

        if (transform.position.y <= deathYLevel)
        {
            CurrentHealth.Value = 0;
            if (RoundManager.Instance != null)
                RoundManager.Instance.OnFighterDefeatedServer(OwnerClientId);
        }
    }

    private void OnHealthChanged(int previous, int current)
    {
        EnsureHealthBar();

        if (healthBar != null)
            healthBar.SetHealth(current);

        // Local hurt juice when you take damage in MP.
        if (IsOwner && current < previous && current > 0)
            CombatFeel.OnPlayerHurt(previous - current);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        // Ignore self-damage RPCs.
        if (rpcParams.Receive.SenderClientId == OwnerClientId)
            return;

        ApplyDamageServer(amount);
    }

    public void ApplyDamageServer(int amount)
    {
        if (!IsServer)
            return;
        if (amount <= 0)
            return;
        if (CurrentHealth.Value <= 0)
            return;
        if (RoundManager.Instance != null && RoundManager.Instance.IsRoundLocked)
            return;

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - amount);
        if (CurrentHealth.Value == 0 && RoundManager.Instance != null)
            RoundManager.Instance.OnFighterDefeatedServer(OwnerClientId);
    }

    public void RespawnServer(Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
            return;

        CurrentHealth.Value = maxHealth;

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        MoveToSpawnClientRpc(position, rotation, rpcParams);
    }

    [ClientRpc]
    private void MoveToSpawnClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        transform.SetPositionAndRotation(position, rotation);

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // -------- Networked gunfire (server hitscan + synced visuals) --------

    /// <summary>Owner calls this after building pellet directions.</summary>
    [ServerRpc]
    public void RequestGunfireServerRpc(Vector3 origin, Vector3[] directions, float speed, int damagePerPellet)
    {
        if (!IsServer)
            return;
        if (directions == null || directions.Length == 0)
            return;
        if (RoundManager.Instance != null && RoundManager.Instance.IsRoundLocked)
            return;

        int count = Mathf.Min(directions.Length, 16);
        var trimmed = new Vector3[count];
        for (int i = 0; i < count; i++)
            trimmed[i] = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : transform.forward;

        // Visuals on remotes (owner already spawned local cosmetics).
        SpawnShotVisualsClientRpc(origin, trimmed, speed);

        // Authoritative hits.
        for (int i = 0; i < count; i++)
        {
            if (Physics.SphereCast(origin, 0.14f, trimmed[i], out RaycastHit hit, 140f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<PlayerHealth>();
                if (target == null || target == this)
                    continue;

                target.ApplyDamageServer(damagePerPellet);
                NotifyHitClientRpc(hit.point, target.CurrentHealth.Value <= 0);
            }
        }
    }

    [ServerRpc]
    public void RequestThrowServerRpc(Vector3 origin, Vector3 direction, float speed, int damage)
    {
        if (!IsServer)
            return;
        if (RoundManager.Instance != null && RoundManager.Instance.IsRoundLocked)
            return;

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        SpawnThrowVisualClientRpc(origin, dir, speed);

        if (Physics.SphereCast(origin, 0.2f, dir, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<PlayerHealth>();
            if (target != null && target != this)
            {
                target.ApplyDamageServer(damage);
                NotifyHitClientRpc(hit.point, target.CurrentHealth.Value <= 0);
            }
        }
    }

    [ClientRpc]
    void SpawnShotVisualsClientRpc(Vector3 origin, Vector3[] directions, float speed)
    {
        // Owner already spawned local pellets for responsiveness.
        if (IsOwner)
            return;

        SpawnVisualPellets(origin, directions, speed);
    }

    [ClientRpc]
    void SpawnThrowVisualClientRpc(Vector3 origin, Vector3 direction, float speed)
    {
        if (IsOwner)
            return;

        SpawnVisualPellets(origin, new[] { direction }, speed);
    }

    [ClientRpc]
    void NotifyHitClientRpc(Vector3 point, bool killed)
    {
        // Only the shooter should hear/see the hit confirm strongly.
        if (!IsOwner)
            return;
        CombatFeel.OnDealtDamage(point, killed);
    }

    void SpawnVisualPellets(Vector3 origin, Vector3[] directions, float speed)
    {
        GameObject prefab = null;
        if (cachedGun == null)
            cachedGun = GetComponentInChildren<AttackPacerLongLegs>(true);
        if (cachedGun != null)
            prefab = cachedGun.projectilePrefab;

        if (prefab == null && cachedMelee != null)
            prefab = cachedMelee.projectilePrefab;

        if (prefab == null || directions == null)
            return;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 dir = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : transform.forward;
            Quaternion rot = Quaternion.LookRotation(dir);
            GameObject go = Instantiate(prefab, origin + dir * 0.05f * i, rot);

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = dir * speed;
            }

            Projectile proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SetVisualOnly(true);
                proj.SetOwnerRoot(transform);
            }

            // Ignore shooter colliders.
            Collider pc = go.GetComponent<Collider>();
            if (pc != null)
            {
                var own = GetComponentsInChildren<Collider>();
                for (int c = 0; c < own.Length; c++)
                {
                    if (own[c] != null)
                        Physics.IgnoreCollision(pc, own[c], true);
                }
            }
        }
    }

    /// <summary>Despawn every player's fighter (rematch / leave cleanup).</summary>
    public static void DespawnAllFighters()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;

        foreach (var kvp in nm.ConnectedClients)
        {
            var po = kvp.Value.PlayerObject;
            if (po != null && po.IsSpawned)
            {
                try { po.Despawn(true); }
                catch { /* teardown */ }
            }
        }
    }
}
