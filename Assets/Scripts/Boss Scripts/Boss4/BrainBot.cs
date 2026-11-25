using UnityEngine;
using System.Collections;

public class BrainBot : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform firePoint;
    public GameObject projectilePrefab;

    public AudioSource audioSource;
    public AudioClip attackSound;

    private AttributesManager attributes;

    [Header("Attack Settings")]
    public float attackRange = 80f;
    public float attackCooldown = 0.55f;
    public float projectileSpeed = 22f;

    [Header("Projectile Growth")]
    public float growTime = 0.28f;
    public float maxScale = 1.6f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    private bool loopRunning;

    private void Start()
    {
        attributes = GetComponent<AttributesManager>();

        if (player == null)
            player = EnemySmart.FindPlayer();

        // Keep firing fast in Boss Fights balance retunes.
        attackCooldown = Mathf.Min(attackCooldown, 0.55f);
        growTime = Mathf.Min(growTime, 0.28f);
        attackRange = Mathf.Max(attackRange, 80f);
    }

    private void OnEnable()
    {
        loopRunning = false;
    }

    private void Update()
    {
        if (attributes != null && attributes.IsDead())
            return;

        if (player == null)
            player = EnemySmart.FindPlayer();
        if (player == null)
            return;

        RotateTowardsPlayer();

        if (!loopRunning)
            StartCoroutine(FireForever());
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private IEnumerator FireForever()
    {
        loopRunning = true;

        try
        {
            while (enabled && gameObject.activeInHierarchy)
            {
                if (attributes != null && attributes.IsDead())
                    yield break;

                if (player == null)
                    player = EnemySmart.FindPlayer();

                if (player != null && firePoint != null && projectilePrefab != null)
                {
                    float distance = Vector3.Distance(transform.position, player.position);
                    if (distance <= attackRange)
                        yield return FireOneShot();
                }

                yield return new WaitForSeconds(Mathf.Max(0.15f, attackCooldown));
            }
        }
        finally
        {
            // Never stay stuck unable to shoot after an error / disable.
            loopRunning = false;
        }
    }

    private IEnumerator FireOneShot()
    {
        if (firePoint == null || projectilePrefab == null)
            yield break;

        // Short charge telegraph at the muzzle, then launch with shared hit scan.
        GameObject telegraph = Object.Instantiate(
            projectilePrefab,
            firePoint.position,
            firePoint.rotation);
        telegraph.transform.localScale = Vector3.one * 0.2f;

        var teleRb = telegraph.GetComponent<Rigidbody>();
        if (teleRb != null)
        {
            teleRb.useGravity = false;
            teleRb.isKinematic = true;
        }

        var teleCol = telegraph.GetComponent<Collider>();
        if (teleCol != null)
            teleCol.enabled = false;

        var teleEp = telegraph.GetComponent<EnemyProjectile>();
        if (teleEp != null)
            teleEp.enabled = false;

        float timer = 0f;
        float grow = Mathf.Max(0.05f, growTime);
        while (timer < grow)
        {
            if (telegraph != null)
            {
                telegraph.transform.position = firePoint.position;
                float t = timer / grow;
                telegraph.transform.localScale = Vector3.Lerp(
                    Vector3.one * 0.2f,
                    Vector3.one * Mathf.Max(1f, maxScale),
                    t);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (telegraph != null)
            Object.Destroy(telegraph);

        if (player == null)
            player = EnemySmart.FindPlayer();

        Vector3 aim = player != null
            ? BossProjectileLaunch.PlayerAimPoint(player)
            : firePoint.position + firePoint.forward * 10f;

        int dmg = attributes != null ? Mathf.Max(1, attributes.attack) : 24;
        BossProjectileLaunch.Fire(
            projectilePrefab,
            firePoint.position,
            aim,
            projectileSpeed,
            dmg,
            transform,
            Mathf.Max(1.1f, maxScale));

        if (audioSource != null && attackSound != null)
            audioSource.PlayOneShot(attackSound);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
