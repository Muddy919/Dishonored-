using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Offline VS Bot: spawns player + AI fighter on the 1V1 Map using BossFighterCatalog.
/// </summary>
public class LocalArenaSpawner : MonoBehaviour
{
    static readonly Vector3 FallbackHostPos = new Vector3(-234.1f, 21.7f, -1185.4f);
    static readonly Vector3 FallbackClientPos = new Vector3(71f, 21.3f, -845f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!ArenaBotModeState.IsActive)
            return;

        if (!LevelEnemyTracker.IsMultiplayerBattleScene())
            return;

#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<LocalArenaSpawner>() != null)
            return;
#else
        if (FindObjectOfType<LocalArenaSpawner>() != null)
            return;
#endif

        var host = new GameObject("LocalArenaSpawner");
        host.AddComponent<LocalArenaSpawner>();
    }

    bool spawned;

    void Start()
    {
        if (spawned)
            return;
        spawned = true;
        StartCoroutine(SpawnMatchRoutine());
    }

    IEnumerator SpawnMatchRoutine()
    {
        // Let scene finish activating; clear stuck loading UI.
        yield return null;
        FastSceneLoader.ForceHideOverlay();

        var catalog = BossFighterCatalog.Get();
        if (catalog == null)
        {
            Debug.LogError("LocalArenaSpawner: BossFighterCatalog missing.");
            yield break;
        }

        int playerIndex = ArenaBotModeState.SelectedCharacterIndex;
        int botIndex = ArenaBotModeState.BotCharacterIndex;
        if (playerIndex < 0)
            playerIndex = 0;
        if (botIndex < 0 || botIndex == playerIndex)
            botIndex = (playerIndex + 1) % 9;

        ResolveSpawns(out Vector3 hostPos, out Quaternion hostRot, out Vector3 clientPos, out Quaternion clientRot);

        DestroyScenePlayers();
        DisableNetworkBattleBits();
        DisableSceneCameras();

        // Wait one frame so Destroy() clears old fighters before we spawn.
        yield return null;

        GameObject playerPrefab = catalog.GetFighter(playerIndex);
        GameObject botPrefab = catalog.GetFighter(botIndex);
        if (playerPrefab == null)
        {
            Debug.LogError("LocalArenaSpawner: player prefab missing for index " + playerIndex);
            yield break;
        }

        GameObject player = SpawnFighter(playerPrefab, hostPos, hostRot, isBot: false);
        GameObject bot = botPrefab != null
            ? SpawnFighter(botPrefab, clientPos, clientRot, isBot: true)
            : null;

        if (player != null)
        {
            GameDifficulty.ApplyToPlayer(player.GetComponent<AttributesManager>());
            EnsurePlayerVision(player);
            PlayerHealthHud.Ensure();
            PlayerHealthHud.RebindToPlayer();
        }

        if (bot != null)
        {
            GameDifficulty.ApplyToEnemy(bot);
            var ai = bot.AddComponent<ArenaFighterBotAI>();
            ai.Configure(player != null ? player.transform : null, ArenaBotModeState.Difficulty);
        }

        var round = gameObject.GetComponent<ArenaRoundController>();
        if (round == null)
            round = gameObject.AddComponent<ArenaRoundController>();
        round.Bind(player, bot);

        FastSceneLoader.ForceHideOverlay();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("LocalArenaSpawner: player=" + playerIndex
                  + " bot=" + botIndex
                  + " difficulty=" + ArenaBotModeState.Difficulty
                  + " cams=" + CountEnabledCameras());
    }

    static int CountEnabledCameras()
    {
        int n = 0;
#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                n++;
        }
        return n;
    }

    static GameObject SpawnFighter(GameObject prefab, Vector3 pos, Quaternion rot, bool isBot)
    {
        if (prefab == null)
        {
            Debug.LogError("LocalArenaSpawner: missing fighter prefab.");
            return null;
        }

        GameObject go = Object.Instantiate(prefab, pos, rot);
        go.name = prefab.name + (isBot ? "_ArenaBot" : "_ArenaPlayer");
        go.SetActive(true);

        if (!isBot)
        {
            if (!go.CompareTag("Player"))
                go.tag = "Player";
        }
        else
        {
            try { go.tag = "Enemy"; }
            catch { /* tag may not exist */ }
        }

        // Keep Default layer on cameras — only body colliders need gameplay layers.
        int bodyLayer = LayerMask.NameToLayer(isBot ? "whatIsEnemy" : "whatIsPlayer");
        if (bodyLayer < 0)
            bodyLayer = LayerMask.NameToLayer(isBot ? "Enemy" : "Player");
        if (bodyLayer < 0)
            bodyLayer = LayerMask.NameToLayer("whatIsPlayer");
        if (bodyLayer >= 0)
            SetBodyLayerOnly(go, bodyLayer);

        var net = go.GetComponent<Unity.Netcode.NetworkObject>();
        if (net != null)
            net.enabled = false;

        var netHealth = go.GetComponent<PlayerHealth>();
        if (netHealth != null)
            netHealth.enabled = false;

        // Hide prefab world-space health bars — PlayerHealthHud owns HP UI.
        HideLegacyHealthBars(go);

        if (isBot)
            StripPlayerControl(go);

        ApplyPose(go.transform, pos, rot);
        return go;
    }

    static void HideLegacyHealthBars(GameObject go)
    {
        var attrs = go.GetComponent<AttributesManager>();
        if (attrs != null && attrs.healthBar != null)
        {
            attrs.healthBar.gameObject.SetActive(false);
            var canvas = attrs.healthBar.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                canvas.gameObject.SetActive(false);
        }

        foreach (var hb in go.GetComponentsInChildren<HealthBar>(true))
        {
            if (hb != null)
                hb.gameObject.SetActive(false);
        }
    }

    static void StripPlayerControl(GameObject go)
    {
        foreach (var cam in go.GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
            cam.gameObject.SetActive(false);
        }

        foreach (var listener in go.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        var move = go.GetComponent<Move>();
        if (move != null)
            move.enabled = false;

        var look = go.GetComponent<MouseLook>();
        if (look != null)
            look.enabled = false;

        var jump = go.GetComponent<jump>();
        if (jump != null)
            jump.enabled = false;

        var switcher = go.GetComponent<CameraSwitcher>();
        if (switcher != null)
            switcher.enabled = false;

        var attack = go.GetComponent<Attack>();
        if (attack != null)
            attack.BotControlled = true;

        var shotgun = go.GetComponent<AttackPacerLongLegs>();
        if (shotgun != null)
            shotgun.BotControlled = true;
    }

    static void EnsurePlayerVision(GameObject go)
    {
        // Activate whole hierarchy so nested camera rigs wake up.
        ActivateHierarchy(go.transform);

        var switcher = go.GetComponent<CameraSwitcher>();
        Camera[] cams = go.GetComponentsInChildren<Camera>(true);

        if (cams.Length == 0)
        {
            // Last resort: create a follow camera so the fight is never black.
            var camGo = new GameObject("ArenaFallbackCamera");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 2.2f, -5.5f);
            camGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cams = new[] { cam };
        }

        // Prefer CameraSwitcher bindings when present.
        if (switcher != null)
        {
            if (switcher.camera1 == null && cams.Length > 0)
                switcher.camera1 = cams[0];
            if (switcher.camera2 == null && cams.Length > 1)
                switcher.camera2 = cams[1];
            switcher.enabled = true;
            switcher.ApplyCameraState();
        }

        Camera keep = null;
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] == null)
                continue;
            ActivateHierarchy(cams[i].transform);
            cams[i].enabled = false;
        }

        if (switcher != null && switcher.camera1 != null)
            keep = switcher.camera1;
        else if (cams.Length > 0)
            keep = cams[0];

        if (keep != null)
        {
            ActivateHierarchy(keep.transform);
            keep.enabled = true;
            keep.gameObject.SetActive(true);
            if (!keep.CompareTag("MainCamera"))
            {
                try { keep.tag = "MainCamera"; }
                catch { /* ignore */ }
            }

            var listener = keep.GetComponent<AudioListener>();
            if (listener == null)
                listener = keep.gameObject.AddComponent<AudioListener>();
            listener.enabled = true;
        }

        CameraSwitcher.EnforceSingleListener();

        // One more pass next frame — CameraSwitcher.Start can fight us.
        go.AddComponent<ArenaCameraFixup>();
    }

    static void ActivateHierarchy(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            if (!cur.gameObject.activeSelf)
                cur.gameObject.SetActive(true);
            cur = cur.parent;
        }
    }

    static void ResolveSpawns(out Vector3 hostPos, out Quaternion hostRot, out Vector3 clientPos, out Quaternion clientRot)
    {
        hostPos = FallbackHostPos;
        hostRot = Quaternion.identity;
        clientPos = FallbackClientPos;
        clientRot = Quaternion.LookRotation((hostPos - clientPos).normalized);

        Transform host = FindSpawnNamed("SpawnPoint");
        Transform client = FindSpawnNamed("SpawnPoint (1)");
        if (host == null)
            host = FindSpawnNamed("HostSpawn");
        if (client == null)
            client = FindSpawnNamed("ClientSpawn");

        if (host != null)
        {
            hostPos = host.position;
            hostRot = host.rotation;
        }

        if (client != null)
        {
            clientPos = client.position;
            clientRot = client.rotation;
        }
        else if (host != null)
        {
            clientRot = Quaternion.LookRotation((hostPos - clientPos).normalized);
        }
    }

    static Transform FindSpawnNamed(string exactName)
    {
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = Resources.FindObjectsOfTypeAll<Transform>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].name != exactName)
                continue;
            if (!all[i].gameObject.scene.IsValid())
                continue;
            return all[i];
        }
        return null;
    }

    static void DestroyScenePlayers()
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var attrs = Resources.FindObjectsOfTypeAll<AttributesManager>();
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            if (attrs[i] == null)
                continue;
            if (!attrs[i].gameObject.scene.IsValid())
                continue;
            if (!attrs[i].CompareTag("Player") && attrs[i].GetComponent<PlayerHealth>() == null)
                continue;
            // Don't destroy arena fighters we just spawned (name suffix).
            string n = attrs[i].gameObject.name;
            if (n.EndsWith("_ArenaPlayer") || n.EndsWith("_ArenaBot"))
                continue;
            Object.Destroy(attrs[i].gameObject);
        }
    }

    static void DisableNetworkBattleBits()
    {
#if UNITY_2023_1_OR_NEWER
        var spawners = Object.FindObjectsByType<BattleSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var spawners = Object.FindObjectsOfType<BattleSpawner>();
#endif
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
                spawners[i].enabled = false;
        }
    }

    static void DisableSceneCameras()
    {
#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>(true);
#endif
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] == null)
                continue;
            if (cams[i].GetComponentInParent<Canvas>() != null)
                continue;
            cams[i].enabled = false;
        }
    }

    static void ApplyPose(Transform t, Vector3 pos, Quaternion rot)
    {
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }

        t.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
    }

    static void SetBodyLayerOnly(GameObject go, int layer)
    {
        // Don't put Camera / AudioListener objects on gameplay layers.
        if (go.GetComponent<Camera>() == null && go.GetComponent<AudioListener>() == null)
            go.layer = layer;

        for (int i = 0; i < go.transform.childCount; i++)
            SetBodyLayerOnly(go.transform.GetChild(i).gameObject, layer);
    }
}

/// <summary>Re-applies camera enable after CameraSwitcher.Start runs.</summary>
public class ArenaCameraFixup : MonoBehaviour
{
    int frames = 3;

    void LateUpdate()
    {
        if (--frames < 0)
        {
            Destroy(this);
            return;
        }

        var cams = GetComponentsInChildren<Camera>(true);
        Camera keep = null;
        var switcher = GetComponent<CameraSwitcher>();
        if (switcher != null && switcher.camera1 != null)
            keep = switcher.camera1;
        else if (cams.Length > 0)
            keep = cams[0];

        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] == null)
                continue;
            bool on = cams[i] == keep;
            cams[i].gameObject.SetActive(true);
            cams[i].enabled = on;
            var listener = cams[i].GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = on;
            else if (on)
                cams[i].gameObject.AddComponent<AudioListener>();
        }

        CameraSwitcher.EnforceSingleListener();
        FastSceneLoader.ForceHideOverlay();
    }
}
