using UnityEngine;
using UnityEngine.AI;

public enum EncounterRole
{
    Standard,
    Ambush,
    Flank,
    Retreat
}

/// <summary>
/// Per-enemy encounter role: ambush hold, flank offsets, low-HP retreat, last-stand rush.
/// Auto-added by EnemyEncounterDirector — AIs query it each frame.
/// </summary>
public class EnemyTactics : MonoBehaviour
{
    public EncounterRole role = EncounterRole.Standard;
    public bool lastStand;

    Vector3 homePos;
    float revealRange = 9f;
    bool ambushBroken;
    bool retreatActive;
    float retreatUntil;
    Vector3 retreatPoint;
    float baseAgentSpeed = -1f;
    int maxHealth = 100;
    bool retreatedOnce;
    AttributesManager attributes;
    NavMeshAgent agent;

    public static EnemyTactics Ensure(Component host)
    {
        if (host == null)
            return null;
        return Ensure(host.gameObject);
    }

    public static EnemyTactics Ensure(GameObject go)
    {
        if (go == null)
            return null;
        var t = go.GetComponent<EnemyTactics>();
        if (t == null)
            t = go.AddComponent<EnemyTactics>();
        return t;
    }

    void Awake()
    {
        homePos = transform.position;
        attributes = GetComponent<AttributesManager>();
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            baseAgentSpeed = agent.speed;
        if (attributes != null)
            maxHealth = Mathf.Max(1, attributes.health);
    }

    public void Configure(EncounterRole assigned, float ambushReveal = 9f)
    {
        role = assigned;
        revealRange = ambushReveal;
        ambushBroken = assigned != EncounterRole.Ambush;
        homePos = transform.position;
    }

    public void NotifyDamaged()
    {
        BreakAmbush();
        TryStartRetreat();
    }

    public void EnterLastStand()
    {
        lastStand = true;
        BreakAmbush();
        retreatActive = false;
        ApplySpeedNow();
    }

    public void BreakAmbush()
    {
        ambushBroken = true;
    }

    public bool ShouldStayHidden(Transform player)
    {
        if (role != EncounterRole.Ambush || ambushBroken || lastStand)
            return false;

        if (player == null)
            return true;

        float d = Vector3.Distance(transform.position, player.position);
        if (d <= revealRange)
        {
            BreakAmbush();
            return false;
        }

        return true;
    }

    public void HoldAmbush(NavMeshAgent nav)
    {
        if (nav == null)
            return;
        EnemyNav.TryPlace(nav);
        EnemyNav.Halt(nav);
        // Drift back toward home so they don't wander into the open.
        if (EnemyNav.Ready(nav) && (transform.position - homePos).sqrMagnitude > 2.25f)
        {
            EnemyNav.Resume(nav);
            EnemyNav.Go(nav, homePos);
        }
    }

    public float EffectiveSight(float baseSight)
    {
        float s = baseSight;
        if (role == EncounterRole.Ambush && !ambushBroken)
            s = Mathf.Min(s, revealRange + 1.5f);
        if (role == EncounterRole.Flank)
            s *= 1.1f;
        if (lastStand)
            s *= 1.4f;
        return s;
    }

    public float EffectiveAttackCooldown(float baseCooldown)
    {
        float c = baseCooldown;
        if (lastStand)
            c *= 0.55f;
        else if (role == EncounterRole.Flank)
            c *= 0.9f;
        return Mathf.Max(0.25f, c);
    }

    public bool IsRetreating => retreatActive && Time.time < retreatUntil && !lastStand;

    public void TickRetreat(Transform player)
    {
        if (!retreatActive)
            return;

        if (Time.time >= retreatUntil || lastStand)
        {
            retreatActive = false;
            return;
        }

        if (player != null && (retreatPoint - player.position).sqrMagnitude < 25f)
            retreatPoint = ComputeRetreatPoint(player);
    }

    public Vector3 RetreatDestination() => retreatPoint;

    public Vector3 CombatDestination(Transform player, float preferredRange, bool wantStrafe)
    {
        if (IsRetreating)
            return retreatPoint;

        bool strafe = wantStrafe || role == EncounterRole.Flank || lastStand;
        float range = preferredRange;

        if (role == EncounterRole.Flank)
            range = Mathf.Max(preferredRange, preferredRange * 1.15f);
        if (lastStand)
            range = Mathf.Min(preferredRange, 2.2f); // close the gap hard

        float flankBias = role == EncounterRole.Flank ? 9f : (strafe ? 5f : 1.2f);
        if (lastStand)
            flankBias = 2.5f;

        return EnemySmart.CombatPoint(transform, player, range, strafe, flankBias);
    }

    public void ApplyAggression(NavMeshAgent nav)
    {
        if (nav == null)
            return;

        if (baseAgentSpeed < 0f)
            baseAgentSpeed = nav.speed;

        ApplySpeedNow();
    }

    void ApplySpeedNow()
    {
        if (agent == null || baseAgentSpeed < 0f)
            return;

        float mult = 1f;
        if (role == EncounterRole.Flank)
            mult = 1.12f;
        if (role == EncounterRole.Retreat && IsRetreating)
            mult = 1.35f;
        if (lastStand)
            mult = 1.55f;

        agent.speed = baseAgentSpeed * mult;
    }

    void TryStartRetreat()
    {
        if (retreatedOnce || lastStand || role != EncounterRole.Retreat)
            return;
        if (attributes == null)
            return;

        if (attributes.health > maxHealth * 0.35f)
            return;

        retreatedOnce = true;
        retreatActive = true;
        retreatUntil = Time.time + 2.4f;

        Transform player = EnemySmart.FindPlayer();
        retreatPoint = ComputeRetreatPoint(player);
    }

    Vector3 ComputeRetreatPoint(Transform player)
    {
        Vector3 away = transform.forward;
        if (player != null)
        {
            away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -player.forward;
        }

        Vector3 side = Vector3.Cross(Vector3.up, away.normalized);
        Vector3 desired = transform.position + away.normalized * 12f + side * Random.Range(-4f, 4f);

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            return hit.position;

        return transform.position + away.normalized * 6f;
    }
}
