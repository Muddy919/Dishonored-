using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private int damage;
    public float lifetime = 3f;
    Transform ownerRoot;
    bool visualOnly;

    static readonly List<Collider> liveColliders = new List<Collider>(64);
    Collider myCollider;

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Destroy(gameObject, lifetime);

        myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            // Cheap registry — no FindObjects scan. Stops shotgun pellets
            // from colliding with each other and vanishing instantly.
            for (int i = liveColliders.Count - 1; i >= 0; i--)
            {
                Collider other = liveColliders[i];
                if (other == null)
                {
                    liveColliders.RemoveAt(i);
                    continue;
                }

                Physics.IgnoreCollision(myCollider, other, true);
            }

            liveColliders.Add(myCollider);
        }

        bool inMultiplayerSession =
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (!inMultiplayerSession && myCollider != null)
        {
            Transform player = EnemySmart.FindPlayer();
            if (player != null)
            {
                Collider playerCol = player.GetComponent<Collider>();
                if (playerCol != null)
                    Physics.IgnoreCollision(myCollider, playerCol, true);

                // Also ignore child colliders on the gun/arms.
                var cols = player.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null && cols[i] != playerCol)
                        Physics.IgnoreCollision(myCollider, cols[i], true);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (myCollider != null)
            liveColliders.Remove(myCollider);
    }

    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    public void SetOwnerRoot(Transform owner)
    {
        ownerRoot = owner;
    }

    /// <summary>Cosmetic pellet in multiplayer — server hitscan owns damage.</summary>
    public void SetVisualOnly(bool on)
    {
        visualOnly = on;
        if (on)
            damage = 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider == null)
            return;

        // Never die on other player bullets.
        if (collision.collider.GetComponentInParent<Projectile>() != null)
            return;

        // Don't hit the shooter (MP + SP).
        if (ownerRoot != null
            && (collision.collider.transform == ownerRoot
                || collision.collider.transform.IsChildOf(ownerRoot)))
            return;

        // MP cosmetics: vanish on impact, never apply damage.
        if (visualOnly)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        bool inMp = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        PlayerHealth networkedTarget = collision.collider.GetComponentInParent<PlayerHealth>();

        if (networkedTarget != null)
        {
            // In MP, damage is server hitscan only — never trust local collision RPCs.
            if (inMp)
            {
                Destroy(gameObject);
                return;
            }

            if (ownerRoot != null
                && (networkedTarget.transform == ownerRoot
                    || networkedTarget.transform.IsChildOf(ownerRoot)))
                return;

            networkedTarget.TakeDamageServerRpc(damage);
            CombatFeel.OnDealtDamage(hitPoint, networkedTarget.CurrentHealth.Value <= damage);
        }
        else
        {
            AttributesManager enemy = collision.collider.GetComponentInParent<AttributesManager>();

            if (enemy != null)
            {
                bool wasAlive = !enemy.IsDead();
                bool isPlayer = enemy.CompareTag("Player");

                if (!isPlayer)
                    EnemyFlinch.Apply(enemy.gameObject, transform.position, damage);

                enemy.TakeDamage(damage);
                bool killed = wasAlive && enemy.IsDead();

                if (!isPlayer)
                    CombatFeel.OnDealtDamage(hitPoint, killed);
            }
        }

        Destroy(gameObject);
    }
}
