using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Boss Fights: as soon as a campaign level loads, put the player on the
/// TransportedToBoss empty so the fight starts immediately.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class BossModeQuickStart : MonoBehaviour
{
    static BossModeQuickStart instance;

    // Correct WORLD positions for TransportedToBoss (L1 is parented under BossStuff).
    static readonly Vector3[] ArenaWorldByCampaignLevel =
    {
        Vector3.zero,
        new Vector3(567f, 23.4f, 1646.5f),   // Level 1 — BossStuff(555,0,1500) + local(12,23.4,146.5)
        new Vector3(-1266f, 14.73f, -161f),  // Level 2
        new Vector3(-2663f, 40f, 895f),      // Level 3
        Vector3.zero,
        new Vector3(-150.1f, 8.06f, 908.3f), // Level 5
    };

    Transform lockedPad;
    Vector3 lockedPos;
    Quaternion lockedRot;
    float forceUntil;
    bool spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
        if (BossModeState.IsActive)
            instance.TryBeginForActiveScene();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        var host = new GameObject("BossModeQuickStart");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<BossModeQuickStart>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!BossModeState.IsActive)
            return;
        TryBeginForActiveScene();
    }

    void TryBeginForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name ?? "";
        if (!sceneName.StartsWith("Level"))
            return;
        if (sceneName.Contains("Intro") || sceneName.Contains("Death")
            || sceneName.Contains("Select") || sceneName.Contains("Mission"))
            return;

        StopAllCoroutines();
        spawned = false;
        lockedPad = null;
        forceUntil = 0f;
        StartCoroutine(RunBossEntry());
    }

    IEnumerator RunBossEntry()
    {
        // Kill level-start view immediately.
        DisableAllWorldCameras();
        SoftHideScenePlayers();

        // Resolve pad as early as possible (retry a few frames).
        for (int i = 0; i < 30 && lockedPad == null; i++)
        {
            lockedPad = FindTransportedToBossPad();
            if (lockedPad == null)
                yield return null;
        }

        ResolveLockedPose();

        // Spawn / move player onto the pad NOW.
        PlaceFighterOnPad();
        spawned = true;
        // Only hold the pad briefly so physics can't yeet the fighter back to level start.
        forceUntil = Time.unscaledTime + 0.75f;

        ClearTrashMobs();
        EnableBossScripts();
        BossBalance.ApplyForCurrentBoss();
        RebindBossesToPlayer();
        PlayerHealthHud.RebindToPlayer();
        BossHealthHud.Ensure();
        BossHealthHud.RebindWorldSpaceBars();
        ShowBossBanner();

        // Extra snaps after physics wakes up.
        for (int i = 0; i < 10; i++)
        {
            ForcePlayerToLockedPose();
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.25f);
        ForcePlayerToLockedPose();
        BossBalance.ApplyForCurrentBoss();
        PlayerHealthHud.RebindToPlayer();
        BossHealthHud.Ensure();
        BossHealthHud.RebindWorldSpaceBars();

        Debug.Log("BossModeQuickStart: pad="
                  + (lockedPad != null ? lockedPad.name : "FALLBACK")
                  + " pos=" + lockedPos
                  + " boss=" + BossModeState.SelectedBossLevel
                  + " campaignL" + BossModeState.GetCampaignLevel()
                  + " fighter=" + BossModeState.SelectedCharacterIndex);
    }

    void LateUpdate()
    {
        if (!BossModeState.IsActive || !spawned)
            return;
        if (Time.unscaledTime > forceUntil)
            return;

        ForcePlayerToLockedPose();
    }

    void ResolveLockedPose()
    {
        if (lockedPad != null)
        {
            lockedPos = lockedPad.position + Vector3.up * 0.5f;
            lockedRot = lockedPad.rotation;
            return;
        }

        int campaign = BossModeState.GetCampaignLevel();
        if (campaign >= 1 && campaign < ArenaWorldByCampaignLevel.Length
            && ArenaWorldByCampaignLevel[campaign].sqrMagnitude > 1f)
        {
            lockedPos = ArenaWorldByCampaignLevel[campaign] + Vector3.up * 0.5f;
            lockedRot = Quaternion.identity;
            return;
        }

        Transform boss = FindBossTransform();
        if (boss != null)
        {
            lockedPos = boss.position - boss.forward * 12f + Vector3.up * 1f;
            Vector3 look = boss.position - lockedPos;
            look.y = 0f;
            lockedRot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
            return;
        }

        lockedPos = new Vector3(0f, 2f, 0f);
        lockedRot = Quaternion.identity;
        Debug.LogWarning("BossModeQuickStart: no TransportedToBoss pad found.");
    }

    static Transform FindTransportedToBossPad()
    {
        // Fast path.
        GameObject named = GameObject.Find("TransportedToBoss");
        if (named != null)
            return named.transform;

        // Parent folder used in Level 1.
        GameObject bossStuff = GameObject.Find("BossStuff");
        if (bossStuff != null)
        {
            Transform child = FindChildRecursive(bossStuff.transform, "TransportedToBoss");
            if (child != null)
                return child;
        }

        // Teleporter destination wiring.
#if UNITY_2023_1_OR_NEWER
        var teleporters = Object.FindObjectsByType<PlayerTeleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var teleporters = Object.FindObjectsOfType<PlayerTeleporter>(true);
#endif
        for (int i = 0; i < teleporters.Length; i++)
        {
            if (teleporters[i] == null || teleporters[i].teleportDestination == null)
                continue;
            string n = teleporters[i].teleportDestination.name ?? "";
            if (n.IndexOf("TransportedToBoss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return teleporters[i].teleportDestination;
        }

        for (int i = 0; i < teleporters.Length; i++)
        {
            if (teleporters[i] != null && teleporters[i].teleportDestination != null)
                return teleporters[i].teleportDestination;
        }

        // Brute-force name search (includes inactive).
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<Transform>(true);
#endif
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "TransportedToBoss")
                return all[i];
        }

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null
                && all[i].name.IndexOf("TransportedToBoss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return all[i];
        }

        return null;
    }

    static Transform FindChildRecursive(Transform root, string exactName)
    {
        if (root.name == exactName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), exactName);
            if (found != null)
                return found;
        }
        return null;
    }

    void PlaceFighterOnPad()
    {
        int charIndex = BossModeState.SelectedCharacterIndex;
        var catalog = BossFighterCatalog.Get();
        GameObject prefab = catalog != null ? catalog.GetFighter(charIndex) : null;

        // Always clear baked level-start players (including inactive ones we hid).
        DestroyAllScenePlayers();

        if (prefab != null)
        {
            GameObject fighter = Object.Instantiate(prefab, lockedPos, lockedRot);
            fighter.name = prefab.name + "_BossFight";
            if (!fighter.CompareTag("Player"))
                fighter.tag = "Player";

            int playerLayer = LayerMask.NameToLayer("whatIsPlayer");
            if (playerLayer >= 0)
                SetLayerRecursive(fighter, playerLayer);

            var net = fighter.GetComponent<Unity.Netcode.NetworkObject>();
            if (net != null && (Unity.Netcode.NetworkManager.Singleton == null
                                || !Unity.Netcode.NetworkManager.Singleton.IsListening))
                net.enabled = false;

            foreach (var cam in fighter.GetComponentsInChildren<Camera>(true))
            {
                cam.enabled = true;
                cam.gameObject.SetActive(true);
            }

            ApplyPose(fighter.transform, lockedPos, lockedRot);
            EnemySmart.InvalidatePlayerCache();
            Debug.Log("BossModeQuickStart: spawned " + prefab.name + " at " + lockedPos);
            return;
        }

        // Catalog missing — revive any leftover player and move them to the pad.
        Debug.LogWarning("BossModeQuickStart: no fighter prefab for index " + charIndex
                         + " — moving scene player to pad.");
        Transform leftover = FindAnyPlayerIncludingInactive();
        if (leftover != null)
        {
            leftover.gameObject.SetActive(true);
            if (!leftover.CompareTag("Player"))
                leftover.tag = "Player";
            foreach (var cam in leftover.GetComponentsInChildren<Camera>(true))
            {
                cam.enabled = true;
                cam.gameObject.SetActive(true);
            }
            ApplyPose(leftover, lockedPos, lockedRot);
            EnemySmart.InvalidatePlayerCache();
        }
    }

    void ForcePlayerToLockedPose()
    {
        // Refresh pad position if we have a live transform (handles late parenting).
        if (lockedPad != null)
        {
            lockedPos = lockedPad.position + Vector3.up * 0.5f;
            lockedRot = lockedPad.rotation;
        }

        Transform player = ResolvePlayer();
        if (player == null)
            return;

        // If the player already left the pad on purpose after the first snap frames, stop locking.
        float dist = Vector3.Distance(player.position, lockedPos);
        if (Time.unscaledTime > forceUntil - 0.4f && dist > 8f)
        {
            forceUntil = 0f;
            return;
        }

        ApplyPose(player, lockedPos, lockedRot);
    }

    static void ApplyPose(Transform t, Vector3 pos, Quaternion rot)
    {
        var controller = t.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        var rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
            rb.Sleep();
        }

        t.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();

        if (controller != null)
            controller.enabled = true;
    }

    static void SoftHideScenePlayers()
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>();
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null || LevelEnemyTracker.IsBossObject(a.gameObject) || LevelEnemyTracker.IsCombatEnemy(a.gameObject))
                continue;

            bool isPlayer = a.CompareTag("Player")
                            || a.GetComponent<PlayerHealth>() != null
                            || a.GetComponentInChildren<Move>() != null;
            if (!isPlayer)
                continue;

            foreach (var cam in a.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            a.gameObject.SetActive(false);
        }
    }

    static void DisableAllWorldCameras()
    {
#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var cams = Object.FindObjectsOfType<Camera>();
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

    static void DestroyAllScenePlayers()
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>(true);
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null)
                continue;
            if (LevelEnemyTracker.IsBossObject(a.gameObject))
                continue;
            if (LevelEnemyTracker.IsCombatEnemy(a.gameObject))
                continue;

            bool isPlayer = a.CompareTag("Player")
                            || a.GetComponent<PlayerHealth>() != null
                            || a.GetComponentInChildren<AttackPacerLongLegs>(true) != null
                            || a.GetComponentInChildren<Move>(true) != null;
            if (!isPlayer)
                continue;

            // Skip fighters we just spawned for this boss fight.
            if (a.gameObject.name.IndexOf("_BossFight", System.StringComparison.Ordinal) >= 0)
                continue;

            try { a.tag = "Untagged"; } catch { /* ignore */ }
            a.gameObject.SetActive(false);
            Object.Destroy(a.gameObject);
        }

        try
        {
            var tagged = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] == null)
                    continue;
                if (tagged[i].name.IndexOf("_BossFight", System.StringComparison.Ordinal) >= 0)
                    continue;
                try { tagged[i].tag = "Untagged"; } catch { /* ignore */ }
                tagged[i].SetActive(false);
                Object.Destroy(tagged[i]);
            }
        }
        catch
        {
            // Tag missing — ignore.
        }
    }

    static Transform FindAnyPlayerIncludingInactive()
    {
#if UNITY_2023_1_OR_NEWER
        var attrs = Object.FindObjectsByType<AttributesManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var attrs = Object.FindObjectsOfType<AttributesManager>(true);
#endif
        for (int i = 0; i < attrs.Length; i++)
        {
            AttributesManager a = attrs[i];
            if (a == null || LevelEnemyTracker.IsBossObject(a.gameObject) || LevelEnemyTracker.IsCombatEnemy(a.gameObject))
                continue;
            if (a.GetComponent<PlayerHealth>() != null || a.GetComponentInChildren<Move>(true) != null
                || a.CompareTag("Player"))
                return a.transform;
        }
        return null;
    }

    static Transform ResolvePlayer()
    {
        Transform t = EnemySmart.FindPlayer();
        if (t != null)
            return t;

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                return tagged.transform;
        }
        catch { /* ignore */ }

        return FindAnyPlayerIncludingInactive();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    static void RebindBossesToPlayer()
    {
        Transform player = ResolvePlayer();
        if (player == null)
            return;

#if UNITY_2023_1_OR_NEWER
        var excav = Object.FindObjectsByType<BossAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var excav = Object.FindObjectsOfType<BossAI>(true);
#endif
        for (int i = 0; i < excav.Length; i++)
            if (excav[i] != null) excav[i].player = player;

#if UNITY_2023_1_OR_NEWER
        var guard = Object.FindObjectsByType<FrontGuardAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var guard = Object.FindObjectsOfType<FrontGuardAI>(true);
#endif
        for (int i = 0; i < guard.Length; i++)
            if (guard[i] != null) guard[i].player = player;

#if UNITY_2023_1_OR_NEWER
        var brain = Object.FindObjectsByType<BrainBot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var brain = Object.FindObjectsOfType<BrainBot>(true);
#endif
        for (int i = 0; i < brain.Length; i++)
            if (brain[i] != null) brain[i].player = player;
    }

    static Transform FindBossTransform()
    {
#if UNITY_2023_1_OR_NEWER
        var loops = Object.FindObjectsByType<BossAttackLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var loops = Object.FindObjectsOfType<BossAttackLoop>(true);
#endif
        if (loops != null && loops.Length > 0 && loops[0] != null)
            return loops[0].transform;

#if UNITY_2023_1_OR_NEWER
        var excav = Object.FindObjectsByType<BossAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var excav = Object.FindObjectsOfType<BossAI>(true);
#endif
        if (excav != null && excav.Length > 0 && excav[0] != null)
            return excav[0].transform;

#if UNITY_2023_1_OR_NEWER
        var guard = Object.FindObjectsByType<FrontGuardAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var guard = Object.FindObjectsOfType<FrontGuardAI>(true);
#endif
        if (guard != null && guard.Length > 0 && guard[0] != null)
            return guard[0].transform;

#if UNITY_2023_1_OR_NEWER
        var brain = Object.FindObjectsByType<BrainBot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var brain = Object.FindObjectsOfType<BrainBot>(true);
#endif
        if (brain != null && brain.Length > 0 && brain[0] != null)
            return brain[0].transform;

        return null;
    }

    static void ClearTrashMobs()
    {
        GameObject enemiesRoot = GameObject.Find("Enemies");
        if (enemiesRoot != null)
            enemiesRoot.SetActive(false);

        LevelEnemyTracker.ForEachAliveEnemy(t =>
        {
            if (t == null)
                return;
            if (LevelEnemyTracker.IsBossObject(t.gameObject))
                return;
            t.gameObject.SetActive(false);
        });

        LevelEnemyTracker.InvalidateCache();
    }

    static void EnableBossScripts()
    {
#if UNITY_2023_1_OR_NEWER
        var activators = Object.FindObjectsByType<ActivateScriptsOnTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var activators = Object.FindObjectsOfType<ActivateScriptsOnTrigger>(true);
#endif
        for (int i = 0; i < activators.Length; i++)
        {
            var activator = activators[i];
            if (activator == null || activator.scriptsToEnable == null)
                continue;

            for (int s = 0; s < activator.scriptsToEnable.Length; s++)
            {
                if (activator.scriptsToEnable[s] != null)
                    activator.scriptsToEnable[s].enabled = true;
            }

            activator.gameObject.SetActive(false);
        }

        EnableAllOfType<BossAttackLoop>();
        EnableAllOfType<BossAI>();
        EnableAllOfType<FrontGuardAI>();
        EnableAllOfType<BrainBot>();
        EnableAllOfType<BombSpawner>();
    }

    static void EnableAllOfType<T>() where T : Behaviour
    {
#if UNITY_2023_1_OR_NEWER
        var list = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var list = Object.FindObjectsOfType<T>(true);
#endif
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null)
                continue;
            if (!list[i].gameObject.activeSelf)
                list[i].gameObject.SetActive(true);
            list[i].enabled = true;
        }
    }

    static void ShowBossBanner()
    {
        string title = BossModeState.SelectedBossLevel switch
        {
            1 => "BOSS 1 — RAILWAY PROTECTOR",
            2 => "BOSS 2 — EXCAVATOR",
            3 => "BOSS 3 — FRONT GUARD",
            4 => "BOSS 4 — BRAIN BOT",
            _ => "BOSS FIGHT"
        };

        var go = new GameObject("BossFightBanner", typeof(Canvas), typeof(CanvasScaler));
        Object.DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.72f);
        rt.anchorMax = new Vector2(0.5f, 0.72f);
        rt.sizeDelta = new Vector2(1100f, 80f);

        var text = labelGo.GetComponent<Text>();
        text.text = title;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 42;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.85f, 0.25f, 1f);
        text.raycastTarget = false;

        Object.Destroy(go, 3.2f);
    }
}
