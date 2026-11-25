using UnityEngine;
using UnityEngine.AI;

public class SniperEnemyAI : MonoBehaviour
{
    public LayerMask whatIsGround;
    public LayerMask whatIsPlayer;

    public NavMeshAgent agent;
    public Transform player;

    private AttributesManager attributes;

    [Header("Animation")]
    public Animator animator;
    private bool wasInSightRange = false;

    [Header("Detection")]
    public float sightRange = 15f;
    public float attackRange = 7f;

    [Header("Patrol")]
    public float walkPointRange = 10f;

    private Vector3 walkPoint;
    private bool walkPointSet;
    float sensedUntil;

    [Header("Sniper")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Projectile Stats")]
    public int projectileDamage = 25;
    public float projectileSpeed = 40f;

    [Header("Attack")]
    public float timeBetweenAttacks = 1.2f;

    private bool alreadyAttacked;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip shootSound;

    private enum State
    {
        Idle,
        Chase,
        Attack
    }

    private State currentState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        attributes = GetComponent<AttributesManager>();

        if (animator == null)
            animator = GetComponent<Animator>();

        Transform p = EnemySmart.FindPlayer();
        if (p != null)
            player = p;
    }

    private void Update()
    {
        if (attributes != null && attributes.IsDead())
            return;

        if (player == null)
        {
            player = EnemySmart.FindPlayer();
            if (player == null)
                return;
        }

        if (sightRange < 32f) sightRange = 32f;
        if (attackRange < 16f) attackRange = 18f;

        EnemyTactics tactics = EnemyTactics.Ensure(this);
        tactics.TickRetreat(player);
        tactics.ApplyAggression(agent);

        if (tactics.ShouldStayHidden(player))
        {
            tactics.HoldAmbush(agent);
            if (animator != null)
            {
                animator.SetBool("IsMoving", EnemyNav.IsMoving(agent));
                animator.SetBool("IsAttacking", false);
            }
            HandleWalkingSound();
            return;
        }

        if (tactics.IsRetreating)
        {
            if (!EnemyNav.Ready(agent)) EnemyNav.TryPlace(agent);
            EnemyNav.Resume(agent);
            EnemyNav.Go(agent, tactics.RetreatDestination());
            if (animator != null)
            {
                animator.SetBool("IsMoving", true);
                animator.SetBool("IsAttacking", false);
            }
            HandleWalkingSound();
            return;
        }

        float effectiveSight = tactics.EffectiveSight(sightRange);
        bool playerInSight = EnemySmart.SensesPlayer(transform, player, effectiveSight, ref sensedUntil);

        if (playerInSight && !wasInSightRange)
        {
            if (animator != null)
                animator.SetTrigger("Alert");
        }

        wasInSightRange = playerInSight;

        if (!playerInSight)
        {
            currentState = State.Idle;
        }
        else
        {
            float distance = Vector3.Distance(
                transform.position,
                player.position
            );

            if (distance > attackRange)
                currentState = State.Chase;
            else
                currentState = State.Attack;
        }

        switch (currentState)
        {
            case State.Idle:
                Patroling();
                break;

            case State.Chase:
                ChasePlayer();
                break;

            case State.Attack:
                AttackPlayer();
                break;
        }

        HandleWalkingSound();
    }

    //================ PATROL ================

    private void Patroling()
    {
        if (!EnemyNav.Ready(agent)) { EnemyNav.TryPlace(agent); if (!EnemyNav.Ready(agent)) return; }

        EnemyNav.Resume(agent);

        if (!walkPointSet)
            SearchWalkPoint();

        if (walkPointSet)
            EnemyNav.Go(agent, walkPoint);

        if (animator != null)
        {
            bool isMoving =
                EnemyNav.IsMoving(agent);

            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsAttacking", false);
        }

        if (EnemyNav.Arrived(agent))
        {
            walkPointSet = false;
        }
    }

    private void SearchWalkPoint()
    {
        float randomX = Random.Range(-walkPointRange, walkPointRange);
        float randomZ = Random.Range(-walkPointRange, walkPointRange);

        Vector3 randomPoint = new Vector3(
            transform.position.x + randomX,
            transform.position.y,
            transform.position.z + randomZ
        );

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            randomPoint,
            out hit,
            5f,
            NavMesh.AllAreas))
        {
            walkPoint = hit.position;
            walkPointSet = true;
        }
    }

    //================ CHASE ================

    private void ChasePlayer()
    {
        if (!EnemyNav.Ready(agent)) { EnemyNav.TryPlace(agent); if (!EnemyNav.Ready(agent)) return; }

        if (animator != null)
        {
            bool isMoving =
                EnemyNav.IsMoving(agent);

            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsAttacking", false);
        }

        walkPointSet = false;

        EnemyNav.Resume(agent);
        agent.updateRotation = true;
        if (EnemyNav.Ready(agent))
        {
            agent.stoppingDistance = 2f;
            var tac = GetComponent<EnemyTactics>();
            float floor = tac != null && tac.lastStand ? 7.5f : 5f;
            agent.speed = Mathf.Max(agent.speed, floor);
        }
        var tactics = GetComponent<EnemyTactics>();
        Vector3 dest = tactics != null
            ? tactics.CombatDestination(player, attackRange * 0.85f, true)
            : EnemySmart.CombatPoint(transform, player, attackRange * 0.85f, true);
        EnemyNav.Go(agent, dest);
    }

    //================ ATTACK ================

    private void AttackPlayer()
    {
        if (!EnemyNav.Ready(agent))
            EnemyNav.TryPlace(agent);

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAttacking", true);
        }

        if (EnemyNav.Ready(agent))
        {
            EnemyNav.Halt(agent);
            agent.updateRotation = false;
        }

        FaceTarget(player.position);
        EnemySmart.AimAtPlayer(firePoint, player, projectileSpeed);

        if (!alreadyAttacked)
        {
            ShootProjectile();

            if (audioSource != null && shootSound != null)
                audioSource.PlayOneShot(shootSound);

            alreadyAttacked = true;

            float cd = GetComponent<EnemyTactics>() != null
                ? GetComponent<EnemyTactics>().EffectiveAttackCooldown(timeBetweenAttacks)
                : timeBetweenAttacks;
            Invoke(nameof(ResetAttack), cd);
        }
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || firePoint == null)
            return;

        GameObject bullet = Instantiate(
            projectilePrefab,
            firePoint.position,
            firePoint.rotation
        );

        EnemyProjectile projectile =
            bullet.GetComponent<EnemyProjectile>();

        if (projectile != null)
        {
            projectile.SetDamage(projectileDamage);
            projectile.SetSpeed(projectileSpeed);
        }
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * 50f
        );
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;

        if (animator != null)
            animator.SetBool("IsAttacking", false);
    }

    private void HandleWalkingSound()
    {
        if (audioSource == null || walkSound == null)
            return;

        bool walking =
            currentState == State.Chase &&
            EnemyNav.IsMoving(agent);

        if (walking)
        {
            if (!audioSource.isPlaying || audioSource.clip != walkSound)
            {
                audioSource.clip = walkSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying && audioSource.clip == walkSound)
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.loop = false;
            }
        }
    }

    public void PlayRandomDeathAnimation()
    {
        if (animator == null)
            return;

        int randomDeath = Random.Range(1, 4);

        switch (randomDeath)
        {
            case 1:
                animator.SetTrigger("Death1");
                break;

            case 2:
                animator.SetTrigger("Death2");
                break;

            case 3:
                animator.SetTrigger("Death3");
                break;
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        if (agent != null)
            EnemyNav.Halt(agent);

        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange);
    }
}