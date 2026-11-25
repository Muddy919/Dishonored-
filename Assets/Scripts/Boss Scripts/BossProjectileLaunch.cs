using UnityEngine;

/// <summary>
/// Shared launcher so every boss shot uses EnemyProjectile player-scan hit logic.
/// </summary>
public static class BossProjectileLaunch
{
    public static GameObject Fire(
        GameObject prefab,
        Vector3 origin,
        Vector3 aimPoint,
        float speed,
        int damage,
        Transform shooterRoot,
        float scale = 1f)
    {
        if (prefab == null)
            return null;

        Vector3 dir = aimPoint - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        GameObject projectile = Object.Instantiate(prefab, origin, Quaternion.LookRotation(dir));
        projectile.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);

        var rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
        }

        var ep = projectile.GetComponent<EnemyProjectile>();
        if (ep == null)
            ep = projectile.AddComponent<EnemyProjectile>();

        ep.IgnoreShooter(shooterRoot);
        ep.SetDamage(Mathf.Max(1, damage));
        ep.SetSpeed(speed);
        ep.hitRadius = Mathf.Max(1.1f, 0.9f * scale);

        return projectile;
    }

    public static Vector3 PlayerAimPoint(Transform player)
    {
        if (player == null)
            return Vector3.zero;
        return player.position + Vector3.up * 1.35f;
    }
}
