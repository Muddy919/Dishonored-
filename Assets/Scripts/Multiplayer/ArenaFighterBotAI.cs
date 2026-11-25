using UnityEngine;

/// <summary>
/// Drives a fighter prefab like a player: move, face target, shoot/melee.
/// </summary>
public class ArenaFighterBotAI : MonoBehaviour
{
    Transform target;
    Rigidbody rb;
    Animator animator;
    AttributesManager attrs;
    Attack melee;
    AttackPacerLongLegs shotgun;

    float moveSpeed = 34f;
    float preferDistance = 18f;
    float shootRange = 55f;
    float meleeRange = 4.5f;
    float reaction = 0.35f;
    float aimJitter = 2.5f;
    float nextDecisionAt;
    float nextAttackAt;
    float strafeSign = 1f;
    Vector3 moveDir;

    public Transform AimTarget => target;

    public void Configure(Transform player, DifficultyPreset difficulty)
    {
        target = player;
        ApplyDifficulty(difficulty);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        attrs = GetComponent<AttributesManager>();
        melee = GetComponent<Attack>();
        shotgun = GetComponent<AttackPacerLongLegs>();

        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (melee != null)
        {
            melee.BotControlled = true;
            melee.BotAimTarget = null;
        }

        if (shotgun != null)
        {
            shotgun.BotControlled = true;
            shotgun.BotAimTarget = null;
        }
    }

    void ApplyDifficulty(DifficultyPreset difficulty)
    {
        switch (difficulty)
        {
            case DifficultyPreset.Easy:
                moveSpeed = 26f;
                preferDistance = 22f;
                reaction = 0.55f;
                aimJitter = 6f;
                shootRange = 42f;
                break;
            case DifficultyPreset.Chaotic:
                moveSpeed = 42f;
                preferDistance = 14f;
                reaction = 0.12f;
                aimJitter = 1.2f;
                shootRange = 70f;
                break;
            default:
                moveSpeed = 34f;
                preferDistance = 18f;
                reaction = 0.28f;
                aimJitter = 2.8f;
                shootRange = 55f;
                break;
        }
    }

    void Update()
    {
        if (PauseMenu.IsPaused || GameplayDeathOverlay.IsOpen || ArenaRoundController.IsFinished)
            return;

        if (attrs != null && attrs.IsDead())
            return;

        if (target == null)
        {
            Retarget();
            if (target == null)
                return;
        }

        if (melee != null)
            melee.BotAimTarget = target;
        if (shotgun != null)
            shotgun.BotAimTarget = target;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 flatDir = dist > 0.01f ? toTarget / dist : transform.forward;

        if (Time.time >= nextDecisionAt)
        {
            nextDecisionAt = Time.time + reaction;
            DecideMove(flatDir, dist);
            if (Random.value < 0.35f)
                strafeSign = -strafeSign;
        }

        FaceToward(flatDir);
        ApplyMove();

        if (Time.time >= nextAttackAt)
            TryAttack(dist);
    }

    void DecideMove(Vector3 flatDir, float dist)
    {
        Vector3 desired = Vector3.zero;
        if (dist > preferDistance + 4f)
            desired = flatDir;
        else if (dist < preferDistance - 4f)
            desired = -flatDir;
        else
            desired = Vector3.Cross(Vector3.up, flatDir) * strafeSign;

        if (desired.sqrMagnitude > 0.001f)
            moveDir = desired.normalized;
        else
            moveDir = Vector3.zero;
    }

    void ApplyMove()
    {
        Vector3 planar = moveDir * moveSpeed;
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = planar.x;
            vel.z = planar.z;
            rb.linearVelocity = vel;
        }
        else
        {
            transform.position += planar * Time.deltaTime;
        }

        if (animator != null)
            animator.SetFloat("Speed", planar.magnitude / Mathf.Max(moveSpeed, 0.01f));
    }

    void FaceToward(Vector3 flatDir)
    {
        if (flatDir.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(flatDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
        if (rb != null)
            rb.MoveRotation(transform.rotation);
    }

    void TryAttack(float dist)
    {
        float cooldown = reaction * 0.85f;
        nextAttackAt = Time.time + cooldown;

        if (dist <= meleeRange && melee != null)
        {
            if (Random.value < 0.55f)
                melee.BotTrySlash();
            else if (Random.value < 0.5f)
                melee.BotTryKick();
            else
                melee.BotTryThrow();
            return;
        }

        if (dist <= shootRange)
        {
            if (shotgun != null)
            {
                shotgun.BotTryShoot();
                return;
            }

            if (melee != null)
                melee.BotTryThrow();
        }
    }

    void Retarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
    }

    public Vector3 GetAimPoint()
    {
        if (target == null)
            return transform.position + transform.forward * 20f;

        Vector3 aim = target.position + Vector3.up * 1.2f;
        aim += Random.insideUnitSphere * aimJitter;
        return aim;
    }
}
