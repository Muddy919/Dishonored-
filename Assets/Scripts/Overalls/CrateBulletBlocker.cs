using UnityEngine;

/// <summary>
/// Ensures crate props have solid colliders so bullets and the player can't pass through them.
/// </summary>
public class CrateBulletBlocker : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<CrateBulletBlocker>() != null)
            return;
#else
        if (FindObjectOfType<CrateBulletBlocker>() != null)
            return;
#endif

        var host = new GameObject("CrateBulletBlocker");
        host.AddComponent<CrateBulletBlocker>();
    }

    void Start()
    {
        EnsureCrateColliders();
    }

    void EnsureCrateColliders()
    {
#if UNITY_2023_1_OR_NEWER
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var transforms = FindObjectsOfType<Transform>();
#endif
        int checkedCount = 0;
        foreach (var t in transforms)
        {
            if (t == null)
                continue;

            checkedCount++;
            if (checkedCount > 4000)
                break;

            string n = t.name;
            if (n.IndexOf("Crate", System.StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("crate", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            // Prefer the object that has a renderer (the visible crate mesh).
            if (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>() == null)
                continue;

            Collider col = t.GetComponent<Collider>();
            if (col == null)
                col = t.GetComponentInChildren<Collider>();

            if (col == null)
            {
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
            }
            else if (col.isTrigger)
            {
                // Solid cover should block bullets.
                col.isTrigger = false;
            }
        }
    }
}
