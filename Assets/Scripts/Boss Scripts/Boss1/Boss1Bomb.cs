using UnityEngine;

public class Boss1Bomb : MonoBehaviour
{
    [Header("Bomb Settings")]
    public int damage = 20;
    public float destroyAfter = 10f;

    [Header("Movement")]
    public float bombDropSpeed = 10f;
    public Vector3 bombRotationSpeed = new Vector3(180f, 180f, 180f);

    private Rigidbody rb;
    private bool exploded = false;
    static readonly Collider[] overlapBuffer = new Collider[16];

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.down * bombDropSpeed;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        Destroy(gameObject, destroyAfter);
    }

    private void Update()
    {
        transform.Rotate(bombRotationSpeed * Time.deltaTime, Space.Self);

        if (!exploded)
            TryHitPlayerNearby(1.6f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryExplode(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryExplode(other);
    }

    void TryHitPlayerNearby(float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            if (overlapBuffer[i] == null)
                continue;
            if (IsPlayer(overlapBuffer[i]))
            {
                TryExplode(overlapBuffer[i]);
                return;
            }
        }
    }

    void TryExplode(Collider other)
    {
        if (exploded || other == null)
            return;

        exploded = true;

        if (IsPlayer(other))
        {
            AttributesManager attributes = other.GetComponentInParent<AttributesManager>();
            if (attributes != null && !attributes.IsDead())
            {
                attributes.TakeDamage(Mathf.Max(1, damage));
                CombatFeel.OnPlayerHurt(Mathf.Max(1, damage));
            }
        }

        Destroy(gameObject);
    }

    static bool IsPlayer(Collider other)
    {
        try
        {
            if (other.CompareTag("Player"))
                return true;
        }
        catch { /* ignore */ }

        int layer = other.gameObject.layer;
        int whatIsPlayer = LayerMask.NameToLayer("whatIsPlayer");
        if (whatIsPlayer >= 0 && layer == whatIsPlayer)
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
}
