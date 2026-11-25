using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Small ammo pack players can walk over. Spawned from enemy deaths.
/// </summary>
public class AmmoPickup : MonoBehaviour
{
    public int shots = 2;
    public float lifeSeconds = 22f;
    public float bobSpeed = 2.4f;
    public float bobHeight = 0.18f;
    public float spinSpeed = 110f;

    Vector3 basePos;
    bool taken;
    static int dropsThisLevel;
    static string dropsScene;

    public static void TrySpawnDrop(Vector3 worldPos)
    {
        if (!LevelEnemyTracker.IsGameplayLevelScene())
            return;

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? "";
        if (dropsScene != scene)
        {
            dropsScene = scene;
            dropsThisLevel = 0;
        }

        if (dropsThisLevel >= 14)
            return;

        if (Random.value > 0.42f)
            return;

        Vector3 pos = worldPos + Vector3.up * 0.45f;
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            pos = hit.position + Vector3.up * 0.45f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "AmmoPickup";
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.28f, 0.22f, 0.28f);

        Object.Destroy(go.GetComponent<Collider>());
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.15f;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                   ?? Shader.Find("Unlit/Color")
                                   ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(0.25f, 0.95f, 0.4f, 1f);
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 3.5f;
        light.intensity = 1.4f;
        light.color = new Color(0.4f, 1f, 0.55f);

        var pickup = go.AddComponent<AmmoPickup>();
        pickup.shots = Random.Range(0, 3) == 0 ? 3 : 2;
        Object.Destroy(go, pickup.lifeSeconds);
        dropsThisLevel++;
    }

    void Awake()
    {
        basePos = transform.position;
    }

    void Update()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = basePos + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        TryPickup(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryPickup(other);
    }

    void TryPickup(Collider other)
    {
        if (taken || other == null)
            return;

        AttackPacerLongLegs gun = other.GetComponentInParent<AttackPacerLongLegs>();
        if (gun == null)
            gun = other.transform.root.GetComponentInChildren<AttackPacerLongLegs>();
        if (gun == null)
            return;

        if (!gun.AddShots(shots))
            return;

        taken = true;
        Destroy(gameObject);
    }
}
