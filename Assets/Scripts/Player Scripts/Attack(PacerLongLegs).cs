using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class AttackPacerLongLegs : MonoBehaviour
{
    [Header("General")]
    public LayerMask enemyLayer;

    private AttributesManager attributes;
    private Animator animator;
    private Collider playerCollider;

    [Header("Gun (LMB) - Shotgun")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float projectileSpeed = 25f;
    public float shootDamageMultiplier = 2f;
    public float fireRate = 0.5f;

    [Header("Ammo")]
    public int maxShots = 6;
    public float reloadTime = 0.55f;

    private int currentShots;
    private bool isReloading = false;
    private float reloadDoneAt = -1f;

    public int CurrentShots => currentShots;
    public int MaxShots => maxShots;
    public bool IsReloading => isReloading;

    [Header("Shotgun Settings")]
    public int pelletCount = 7;
    public float spreadAngle = 5f;

    [Header("Recoil Settings")]
    public Camera firstPersonCamera;
    public Camera thirdPersonCamera;
    public float firstPersonVerticalRecoil = 4f;
    public float firstPersonHorizontalRecoil = 2f;
    public float thirdPersonRecoilAngle = 1.5f;
    public float recoilRecoverySpeed = 8f;

    private float nextShootTime;

    private Vector2 recoilAmount;

    /// <summary>When true, mouse input is ignored — Arena bot drives shooting.</summary>
    public bool BotControlled { get; set; }

    /// <summary>Optional aim target for bot shots.</summary>
    public Transform BotAimTarget { get; set; }

    private void Awake()
    {
        attributes = GetComponent<AttributesManager>();
        animator = GetComponentInChildren<Animator>();
        playerCollider = GetComponent<Collider>();

        currentShots = maxShots;
        // Prefabs had 1–2s reloads; force a snappy one.
        if (reloadTime > 0.55f || reloadTime <= 0f)
            reloadTime = 0.55f;

        ApplyCombatFeelTuning();

        if (attributes == null)
            Debug.LogError("Player has NO AttributesManager!");

        if (animator == null)
            Debug.LogError("No Animator found!");

        // Guarantees the ammo side panel appears for every fighter.
        AmmoSidePanel.EnsureFor(this);
    }

    /// <summary>Called by CombatFeel — stronger kick without editing every prefab.</summary>
    public void ApplyCombatFeelTuning()
    {
        firstPersonVerticalRecoil = Mathf.Max(firstPersonVerticalRecoil, 7.5f);
        firstPersonHorizontalRecoil = Mathf.Max(firstPersonHorizontalRecoil, 3.2f);
        thirdPersonRecoilAngle = Mathf.Max(thirdPersonRecoilAngle, 2.4f);
        recoilRecoverySpeed = Mathf.Clamp(recoilRecoverySpeed, 5.5f, 9f);
    }

    private void Update()
    {
        if (PauseMenu.IsPaused || GameplayDeathOverlay.IsOpen)
            return;

        if (isReloading && Time.unscaledTime >= reloadDoneAt)
            FinishReload();

        if (!BotControlled)
        {
            // SHOOT
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                TryShoot();

            // Manual reload
            if (!isReloading && currentShots < maxShots)
            {
                bool reloadPressed =
                    (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    || Input.GetKeyDown(KeyCode.R);
                if (reloadPressed)
                    StartReload();
            }
        }

        ApplyRecoilRecovery();
    }

    public void BotTryShoot() => TryShoot();

    // =========================
    // SHOOT
    // =========================
    void TryShoot()
    {
        if (isReloading) return;

        if (Time.time < nextShootTime) return;

        if (currentShots <= 0)
        {
            StartReload();
            return;
        }

        if (projectilePrefab == null || shootPoint == null) return;

        nextShootTime = Time.time + fireRate;

        animator.SetTrigger("Shoot");

        currentShots--;

        // Aim where the crosshair points (camera center), not just gun barrel forward.
        Quaternion aimRotation = GetCrosshairAimRotation();

        // Spawn slightly ahead of the muzzle so pellets don't hit the gun mesh.
        Vector3 muzzle = shootPoint.position + shootPoint.forward * 0.35f;
        int damage = Mathf.RoundToInt(attributes.attack * shootDamageMultiplier);
        int count = Mathf.Max(1, pelletCount);

        bool inMp = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        var netHealth = GetComponent<PlayerHealth>();
        bool useNetworkFire = inMp && netHealth != null && netHealth.IsOwner;

        var dirs = new Vector3[count];
        var burstCols = new Collider[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = muzzle + shootPoint.forward * (0.05f * i);

            Quaternion spreadRotation = aimRotation *
                Quaternion.Euler(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0f
                );

            dirs[i] = spreadRotation * Vector3.forward;

            GameObject projectile = Instantiate(projectilePrefab, spawnPos, spreadRotation);

            Rigidbody rb = projectile.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = dirs[i] * projectileSpeed;
            }

            Collider projCollider = projectile.GetComponent<Collider>();
            burstCols[i] = projCollider;

            if (projCollider != null)
            {
                var ownCols = GetComponentsInChildren<Collider>();
                for (int c = 0; c < ownCols.Length; c++)
                {
                    if (ownCols[c] != null)
                        Physics.IgnoreCollision(projCollider, ownCols[c], true);
                }
            }

            for (int j = 0; j < i; j++)
            {
                if (projCollider != null && burstCols[j] != null)
                    Physics.IgnoreCollision(projCollider, burstCols[j], true);
            }

            Projectile projScript = projectile.GetComponent<Projectile>();
            if (projScript != null)
            {
                projScript.SetOwnerRoot(transform);
                if (useNetworkFire)
                    projScript.SetVisualOnly(true); // server hitscan owns MP damage
                else
                    projScript.SetDamage(damage);
            }
        }

        if (useNetworkFire)
            netHealth.RequestGunfireServerRpc(muzzle, dirs, projectileSpeed, damage);

        ApplyRecoil();
        CombatFeel.OnPlayerShot(shootPoint);

        if (currentShots <= 0)
        {
            StartReload();
        }
    }

    Quaternion GetCrosshairAimRotation()
    {
        if (BotControlled)
        {
            Vector3 botAim;
            var botAi = GetComponent<ArenaFighterBotAI>();
            if (botAi != null)
                botAim = botAi.GetAimPoint();
            else if (BotAimTarget != null)
                botAim = BotAimTarget.position + Vector3.up * 1.2f;
            else
                return shootPoint.rotation;

            Vector3 botDir = botAim - shootPoint.position;
            if (botDir.sqrMagnitude < 0.0001f)
                return shootPoint.rotation;
            return Quaternion.LookRotation(botDir.normalized);
        }

        Camera cam = null;
        if (firstPersonCamera != null && firstPersonCamera.enabled)
            cam = firstPersonCamera;
        else if (thirdPersonCamera != null && thirdPersonCamera.enabled)
            cam = thirdPersonCamera;
        else
            cam = Camera.main;

        if (cam == null)
            return shootPoint.rotation;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.GetPoint(200f);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;

        Vector3 direction = aimPoint - shootPoint.position;
        if (direction.sqrMagnitude < 0.0001f)
            return shootPoint.rotation;

        return Quaternion.LookRotation(direction.normalized);
    }

    // =========================
    // RELOAD
    // =========================
    void StartReload()
    {
        if (isReloading) return;

        isReloading = true;
        reloadDoneAt = Time.unscaledTime + Mathf.Max(0.2f, reloadTime);

        if (animator != null)
            animator.SetTrigger("Reload");
    }

    void FinishReload()
    {
        currentShots = maxShots;
        isReloading = false;
        reloadDoneAt = -1f;
    }

    /// <summary>Ammo loot pickup. Returns false if magazine already full.</summary>
    public bool AddShots(int amount)
    {
        if (amount <= 0)
            return false;

        if (currentShots >= maxShots && !isReloading)
            return false;

        if (isReloading)
        {
            isReloading = false;
            reloadDoneAt = -1f;
        }

        currentShots = Mathf.Min(maxShots, currentShots + amount);
        return true;
    }

    // =========================
    // RECOIL SYSTEM
    // =========================
    private void ApplyRecoil()
    {
        if (firstPersonCamera != null)
        {
            float verticalKick = Random.Range(
                firstPersonVerticalRecoil * 0.8f,
                firstPersonVerticalRecoil
            );

            float horizontalKick = Random.Range(
                -firstPersonHorizontalRecoil,
                 firstPersonHorizontalRecoil
            );

            recoilAmount += new Vector2(verticalKick, horizontalKick);
        }

        if (thirdPersonCamera != null)
        {
            float wiggleX = Random.Range(
                -thirdPersonRecoilAngle,
                 thirdPersonRecoilAngle
            );

            float wiggleY = Random.Range(
                -thirdPersonRecoilAngle,
                 thirdPersonRecoilAngle
            );

            thirdPersonCamera.transform.localRotation *=
                Quaternion.Euler(wiggleX, wiggleY, 0f);
        }
    }

    private void ApplyRecoilRecovery()
    {
        if (firstPersonCamera != null)
        {
            recoilAmount = Vector2.Lerp(
                recoilAmount,
                Vector2.zero,
                Time.deltaTime * recoilRecoverySpeed
            );

            firstPersonCamera.transform.localRotation =
                Quaternion.Euler(-recoilAmount.x, recoilAmount.y, 0f);
        }
    }
}