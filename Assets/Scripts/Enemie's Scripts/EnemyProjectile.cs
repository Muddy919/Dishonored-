using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private int damage = 10;
    private Rigidbody rb;
    private bool consumed;

    public float lifetime = 8f;
    public float speed = 30f;
    public float hitRadius = 1.25f;

    Vector3 lastPosition;
    Collider myCollider;
    static readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    static readonly Collider[] overlapBuffer = new Collider[24];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();
        if (myCollider != null)
            myCollider.isTrigger = true;
    }

    private void Start()
    {
        if (rb != null)
        {
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        lastPosition = transform.position;
        float life = lifetime > 0f ? lifetime : 8f;
        Destroy(gameObject, Mathf.Min(life, 12f));
    }

    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(1f, newSpeed);

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }
    }

    public void IgnoreShooter(Transform shooterRoot)
    {
        if (shooterRoot == null)
            return;

        if (myCollider == null)
            myCollider = GetComponent<Collider>();
        if (myCollider == null)
            return;

        var cols = shooterRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                Physics.IgnoreCollision(myCollider, cols[i], true);
        }
    }

    private void FixedUpdate()
    {
        if (consumed)
            return;

        if (rb != null && !rb.isKinematic)
        {
            rb.useGravity = false;
            rb.linearVelocity = transform.forward * speed;
        }

        // 1) Direct player scan around the projectile.
        if (TryHitPlayerNearby(hitRadius))
            return;

        // 2) Sweep along travel — prefer player over world geometry.
        Vector3 current = transform.position;
        Vector3 delta = current - lastPosition;
        float dist = delta.magnitude;
        if (dist > 0.001f)
        {
            int count = Physics.SphereCastNonAlloc(
                lastPosition,
                Mathf.Max(0.4f, hitRadius * 0.45f),
                delta.normalized,
                hitBuffer,
                dist + 0.05f,
                ~0,
                QueryTriggerInteraction.Collide);

            if (count > 0 && ProcessHits(count))
                return;
        }

        // 3) Beam toward the player: if they're near the shot line, count it as a hit.
        if (TryScanBeamToPlayer(35f, hitRadius * 1.1f))
            return;

        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            HandleHit(collision.collider);
    }

    bool ProcessHits(int count)
    {
        RaycastHit? playerHit = null;
        RaycastHit? worldHit = null;
        float playerDist = float.MaxValue;
        float worldDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || hit.collider == myCollider)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            if (IsPlayerCollider(hit.collider))
            {
                if (hit.distance < playerDist)
                {
                    playerDist = hit.distance;
                    playerHit = hit;
                }
                continue;
            }

            if (ShouldPassThrough(hit.collider))
                continue;

            if (hit.distance < worldDist)
            {
                worldDist = hit.distance;
                worldHit = hit;
            }
        }

        // Always favor tagging the player if the sweep saw them.
        if (playerHit.HasValue)
        {
            HandleHit(playerHit.Value.collider);
            return consumed;
        }

        if (worldHit.HasValue)
        {
            HandleHit(worldHit.Value.collider);
            return consumed;
        }

        return false;
    }

    bool TryHitPlayerNearby(float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            if (c == null || c == myCollider)
                continue;
            if (!IsPlayerCollider(c))
                continue;

            HandleHit(c);
            return consumed;
        }

        return false;
    }

    bool TryScanBeamToPlayer(float maxRange, float lineRadius)
    {
        Transform player = EnemySmart.FindPlayer();
        if (player == null)
            return false;

        Vector3 chest = player.position + Vector3.up * 1.2f;
        Vector3 toPlayer = chest - transform.position;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f || dist > maxRange)
            return false;

        // Only "pull" hits when the player is roughly in front of the shot.
        float align = Vector3.Dot(transform.forward, toPlayer.normalized);
        if (align < 0.35f)
            return false;

        // Distance from player to the projectile's forward ray.
        float along = Vector3.Dot(toPlayer, transform.forward);
        if (along < 0f)
            return false;

        Vector3 closest = transform.position + transform.forward * along;
        float sideDist = Vector3.Distance(closest, chest);
        if (sideDist > lineRadius)
            return false;

        var attrs = player.GetComponent<AttributesManager>()
                    ?? player.GetComponentInParent<AttributesManager>();
        if (attrs == null || attrs.IsDead())
            return false;

        attrs.TakeDamage(Mathf.Max(1, damage));
        CombatFeel.OnPlayerHurt(Mathf.Max(1, damage));
        Consume();
        return true;
    }

    static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        try
        {
            if (other.CompareTag("Player"))
                return true;
        }
        catch { /* tag missing */ }

        int layer = other.gameObject.layer;
        int whatIsPlayer = LayerMask.NameToLayer("whatIsPlayer");
        int playerLayer = LayerMask.NameToLayer("Player");
        if ((whatIsPlayer >= 0 && layer == whatIsPlayer) || (playerLayer >= 0 && layer == playerLayer))
            return true;

        AttributesManager attrs = other.GetComponentInParent<AttributesManager>();
        if (attrs == null)
            return false;

        if (LevelEnemyTracker.IsBossObject(attrs.gameObject) || LevelEnemyTracker.IsCombatEnemy(attrs.gameObject))
            return false;

        return attrs.CompareTag("Player")
               || attrs.GetComponent<PlayerHealth>() != null
               || attrs.GetComponentInChildren<Move>() != null;
    }

    static bool ShouldPassThrough(Collider other)
    {
        if (other.GetComponentInParent<EnemyProjectile>() != null)
            return true;
        if (other.GetComponentInParent<Projectile>() != null)
            return true;
        if (other.GetComponentInParent<Boss1Bomb>() != null)
            return true;

        if (LevelEnemyTracker.IsBossObject(other.gameObject) || LevelEnemyTracker.IsCombatEnemy(other.gameObject))
            return true;

        AttributesManager attrs = other.GetComponentInParent<AttributesManager>();
        if (attrs != null && !IsPlayerCollider(other))
            return true;

        return false;
    }

    void HandleHit(Collider other)
    {
        if (consumed || other == null)
            return;

        if (other.GetComponentInParent<EnemyProjectile>() != null)
            return;
        if (other.GetComponentInParent<Projectile>() != null)
            return;

        if (IsPlayerCollider(other))
        {
            AttributesManager maybePlayer = other.GetComponentInParent<AttributesManager>();
            if (maybePlayer != null && !maybePlayer.IsDead())
            {
                maybePlayer.TakeDamage(Mathf.Max(1, damage));
                CombatFeel.OnPlayerHurt(Mathf.Max(1, damage));
            }

            Consume();
            return;
        }

        if (ShouldPassThrough(other))
            return;

        Consume();
    }

    void Consume()
    {
        if (consumed)
            return;

        consumed = true;
        Destroy(gameObject);
    }
}
