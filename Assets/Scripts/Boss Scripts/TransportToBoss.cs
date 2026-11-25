using UnityEngine;

public class PlayerTeleporter : MonoBehaviour
{
    [Header("Teleport Destination")]
    public Transform teleportDestination;

    [Header("Clear Condition")]
    [Tooltip("If true, player can only enter the boss arena after all regular enemies are dead.")]
    public bool requireAllEnemiesDead = true;

    private int playerLayer;
    float nextHintTime;

    private void Awake()
    {
        playerLayer = LayerMask.NameToLayer("whatIsPlayer");
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform root = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        bool isPlayer =
            other.CompareTag("Player")
            || root.CompareTag("Player")
            || other.gameObject.layer == playerLayer
            || root.gameObject.layer == playerLayer;

        if (!isPlayer)
            return;

        Transform player = root;

        if (requireAllEnemiesDead && !BossModeState.IsActive)
        {
            int remaining = LevelEnemyTracker.CountAliveEnemies();
            if (remaining > 0)
            {
                if (Time.time >= nextHintTime)
                {
                    nextHintTime = Time.time + 1.5f;
                    Debug.Log("Clear all enemies before the boss fight. Remaining: " + remaining);
                    LevelObjectiveHud.ShowBlockedHint(remaining);
                }
                return;
            }
        }

        if (teleportDestination == null)
            return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.position = teleportDestination.position;
        player.rotation = teleportDestination.rotation;

        if (controller != null)
            controller.enabled = true;
    }
}
