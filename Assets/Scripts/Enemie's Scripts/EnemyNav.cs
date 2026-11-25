using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Safe NavMeshAgent calls. Agents that aren't on a mesh no longer spam errors.
/// </summary>
public static class EnemyNav
{
    public static bool Ready(NavMeshAgent agent)
    {
        return agent != null && agent.enabled && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    public static void TryPlace(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isActiveAndEnabled)
            return;

        if (agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    public static void Resume(NavMeshAgent agent)
    {
        if (Ready(agent))
            agent.isStopped = false;
    }

    public static void Halt(NavMeshAgent agent)
    {
        if (Ready(agent))
            agent.isStopped = true;
    }

    public static void Go(NavMeshAgent agent, Vector3 destination)
    {
        if (!Ready(agent))
            return;

        agent.SetDestination(destination);
    }

    public static bool IsMoving(NavMeshAgent agent)
    {
        if (!Ready(agent))
            return false;

        return !agent.isStopped && agent.velocity.sqrMagnitude > 0.04f;
    }

    public static bool Arrived(NavMeshAgent agent, float distance = 1.2f)
    {
        if (!Ready(agent))
            return false;

        return !agent.pathPending && agent.remainingDistance <= distance;
    }

    public static void Clear(NavMeshAgent agent)
    {
        if (!Ready(agent))
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }
}

/// <summary>
/// Shared senses: line of sight, short memory, flanking, and shot leading.
/// </summary>
public static class EnemySmart
{
    static Transform cachedPlayer;
    static float cacheUntil;
    static bool tagsSanitized;

    /// <summary>
    /// Fighter prefabs tag both the root AND a nested "Pacer" mesh as Player.
    /// FindGameObjectWithTag can pick the child (~8m below the root), so enemies
    /// chase/aim at the wrong point and feel "different" per character load order.
    /// Always prefer the root that owns AttributesManager / movement.
    /// </summary>
    public static Transform FindPlayer()
    {
        if (cachedPlayer != null && Time.unscaledTime < cacheUntil)
            return cachedPlayer;

        SanitizeDuplicatePlayerTags();

        Transform best = null;
        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag("Player");
        }
        catch
        {
            players = System.Array.Empty<GameObject>();
        }

        for (int i = 0; i < players.Length; i++)
        {
            GameObject go = players[i];
            if (go == null)
                continue;

            // Real player root has AttributesManager (and usually Movement).
            if (go.GetComponent<AttributesManager>() != null)
            {
                best = go.transform;
                break;
            }
        }

        if (best == null && players.Length > 0)
            best = players[0].transform.root;

        cachedPlayer = best;
        cacheUntil = Time.unscaledTime + 0.75f;
        return cachedPlayer;
    }

    public static void InvalidatePlayerCache()
    {
        cachedPlayer = null;
        cacheUntil = -1f;
        tagsSanitized = false;
    }

    static void SanitizeDuplicatePlayerTags()
    {
        if (tagsSanitized)
            return;
        tagsSanitized = true;

        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag("Player");
        }
        catch
        {
            return;
        }

        // Keep Player tag only on objects that own AttributesManager.
        // Nested mesh roots (often named "Pacer") get Untagged.
        for (int i = 0; i < players.Length; i++)
        {
            GameObject go = players[i];
            if (go == null)
                continue;

            if (go.GetComponent<AttributesManager>() != null)
                continue;

            // Child mesh / decoy — strip tag so AI won't lock onto it.
            go.tag = "Untagged";
        }
    }

    public static bool SensesPlayer(Transform self, Transform player, float range, ref float memoryUntil)
    {
        if (self == null || player == null)
            return false;

        if (HasLineOfSight(self, player, range))
        {
            memoryUntil = Time.time + 5f;
            return true;
        }

        return Time.time < memoryUntil;
    }

    public static bool HasLineOfSight(Transform self, Transform player, float range)
    {
        Vector3 eye = self.position + Vector3.up * 1.5f;
        // Aim at torso relative to fighter capsule (center is below root on these prefabs).
        Vector3 target = player.position + Vector3.up * 1.2f;
        Vector3 delta = target - eye;
        float dist = delta.magnitude;
        if (dist > range || dist < 0.05f)
            return false;

        if (Physics.Raycast(eye, delta.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform hitRoot = hit.transform.root;
            return hitRoot == player.root || hit.transform == player || hit.transform.IsChildOf(player);
        }

        return true;
    }

    public static Vector3 CombatPoint(Transform self, Transform player, float preferredRange, bool strafe)
    {
        return CombatPoint(self, player, preferredRange, strafe, strafe ? 5f : 1.2f);
    }

    public static Vector3 CombatPoint(Transform self, Transform player, float preferredRange, bool strafe, float strafeAmount)
    {
        Vector3 to = player.position - self.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f)
            to = self.forward;

        Vector3 dir = to.normalized;
        float side = Mathf.Sin(Time.time * 1.7f + self.GetInstanceID() * 0.013f);
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (!strafe)
            strafeAmount = Mathf.Min(strafeAmount, 1.2f);
        Vector3 desired = player.position - dir * preferredRange + right * side * strafeAmount;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            return hit.position;

        if (NavMesh.SamplePosition(player.position, out hit, 8f, NavMesh.AllAreas))
            return hit.position;

        return player.position;
    }

    public static void AimAtPlayer(Transform firePoint, Transform player, float projectileSpeed)
    {
        if (firePoint == null || player == null)
            return;

        Vector3 target = player.position + Vector3.up * 1.25f;
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null && projectileSpeed > 1f)
        {
            float dist = Vector3.Distance(firePoint.position, target);
            float lead = Mathf.Clamp(dist / projectileSpeed, 0f, 1.1f);
            target += body.linearVelocity * lead * 0.85f;
        }

        Vector3 dir = target - firePoint.position;
        if (dir.sqrMagnitude > 0.001f)
            firePoint.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
