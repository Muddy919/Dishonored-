using UnityEngine;

public class ActivateScriptsOnTrigger : MonoBehaviour
{
    [Header("Scripts to Enable")]
    public MonoBehaviour[] scriptsToEnable;

    [Header("Optional")]
    public bool disableAfterTrigger = true;

    [Header("Clear Condition")]
    public bool requireAllEnemiesDead = true;

    float nextHintTime;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (requireAllEnemiesDead && !BossModeState.IsActive)
        {
            int remaining = LevelEnemyTracker.CountAliveEnemies();
            if (remaining > 0)
            {
                if (Time.time >= nextHintTime)
                {
                    nextHintTime = Time.time + 1.5f;
                    LevelObjectiveHud.ShowBlockedHint(remaining);
                }
                return;
            }
        }

        foreach (MonoBehaviour script in scriptsToEnable)
        {
            if (script != null)
                script.enabled = true;
        }

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }
}
