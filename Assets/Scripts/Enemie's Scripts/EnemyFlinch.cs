using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Brief stagger + knockback + material flash when an enemy takes damage.
/// </summary>
public class EnemyFlinch : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorIdFallback = Shader.PropertyToID("_Color");

    Coroutine flinchRoutine;
    Vector3 knockDir;
    NavMeshAgent agent;
    Renderer[] renderers;
    Color[] originalColors;
    bool cached;
    float lastFlinchAt = -1f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        CacheRenderers();
    }

    void CacheRenderers()
    {
        if (cached)
            return;
        cached = true;
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].material == null)
            {
                originalColors[i] = Color.white;
                continue;
            }

            var mat = renderers[i].material;
            if (mat.HasProperty(ColorId))
                originalColors[i] = mat.GetColor(ColorId);
            else if (mat.HasProperty(ColorIdFallback))
                originalColors[i] = mat.GetColor(ColorIdFallback);
            else
                originalColors[i] = Color.white;
        }
    }

    public static void Apply(GameObject enemy, Vector3 hitFrom, int damage)
    {
        if (enemy == null)
            return;

        // Don't flinch the player.
        if (enemy.CompareTag("Player"))
            return;

        var flinch = enemy.GetComponent<EnemyFlinch>();
        if (flinch == null)
            flinch = enemy.AddComponent<EnemyFlinch>();

        bool isBoss = LevelEnemyTracker.IsBossObject(enemy);
        flinch.DoFlinch(hitFrom, damage, isBoss);
    }

    void DoFlinch(Vector3 hitFrom, int damage, bool noKnockback)
    {
        // Debounce stacked calls (Projectile + TakeDamage in one frame).
        if (Time.time - lastFlinchAt < 0.05f)
            return;
        lastFlinchAt = Time.time;

        CacheRenderers();

        // Cancel any physics shove from the projectile impact.
        if (noKnockback)
            CancelPhysicsKnock();

        Vector3 dir = transform.position - hitFrom;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = -transform.forward;
        knockDir = dir.normalized;

        if (flinchRoutine != null)
            StopCoroutine(flinchRoutine);
        flinchRoutine = StartCoroutine(FlinchRoutine(damage, noKnockback));
    }

    void CancelPhysicsKnock()
    {
        var rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null || rbs[i].isKinematic)
                continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
        }
    }

    IEnumerator FlinchRoutine(int damage, bool noKnockback)
    {
        // Flash white/red
        SetFlash(new Color(1f, 0.35f, 0.35f, 1f));

        // Bosses: hit flash only — never stagger or shove them.
        if (noKnockback)
        {
            float fadeBoss = 0f;
            while (fadeBoss < 0.12f)
            {
                fadeBoss += Time.deltaTime;
                CancelPhysicsKnock();
                LerpFlash(fadeBoss / 0.12f);
                yield return null;
            }
            RestoreColors();
            flinchRoutine = null;
            yield break;
        }

        // Pause nav briefly so the stagger reads.
        bool wasStopped = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            wasStopped = agent.isStopped;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        float knockStrength = Mathf.Clamp(0.35f + damage * 0.02f, 0.35f, 1.1f);
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.deltaTime;
            Vector3 step = knockDir * (knockStrength * Time.deltaTime * 6f);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Move(step);
            else
                transform.position += step;
            yield return null;
        }

        // Return color
        float fade = 0f;
        while (fade < 0.12f)
        {
            fade += Time.deltaTime;
            float u = fade / 0.12f;
            LerpFlash(u);
            yield return null;
        }
        RestoreColors();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = wasStopped;

        flinchRoutine = null;
    }

    void SetFlash(Color c)
    {
        if (renderers == null)
            return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            var mat = renderers[i].material;
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, c);
            else if (mat.HasProperty(ColorIdFallback))
                mat.SetColor(ColorIdFallback, c);
        }
    }

    void LerpFlash(float u)
    {
        if (renderers == null)
            return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            var mat = renderers[i].material;
            Color from = new Color(1f, 0.35f, 0.35f, 1f);
            Color to = originalColors[i];
            Color c = Color.Lerp(from, to, u);
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, c);
            else if (mat.HasProperty(ColorIdFallback))
                mat.SetColor(ColorIdFallback, c);
        }
    }

    void RestoreColors()
    {
        if (renderers == null)
            return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            var mat = renderers[i].material;
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, originalColors[i]);
            else if (mat.HasProperty(ColorIdFallback))
                mat.SetColor(ColorIdFallback, originalColors[i]);
        }
    }
}
