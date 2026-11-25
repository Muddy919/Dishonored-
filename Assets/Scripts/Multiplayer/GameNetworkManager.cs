using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on a GameObject in your very first scene (e.g. MainMenu), on the SAME
/// object as the Unity NetworkManager + UnityTransport components (or any object -
/// it just needs to exist before you Host/Join).
///
/// Uses Unity Relay instead of a direct IP connection: the host gets a short join
/// code back after hosting starts, shares it with the other player, and that
/// player types the CODE (not an IP) into Join. Neither side needs port
/// forwarding or to know their IP at all - Unity's relay servers sit in between.
///
/// Wire your existing "Multiplayer" button to open a small Host/Join panel, then:
///   - Host button  -> GameNetworkManager.Instance.HostGame()
///   - Join button  -> use JoinButtonHandler, not this directly (see that script)
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [Tooltip("Exact name of your character-select scene (case-sensitive), must " +
             "be in Build Settings. The host automatically loads this for both " +
             "players once the second player connects.")]
    [SerializeField] private string selectSceneName = "CharacterSelect";

    [Tooltip("Drag the MatchState GameObject (the one with a NetworkObject + " +
             "MatchState script) here. This gets spawned automatically the " +
             "moment hosting starts - no extra code needed.")]
    [SerializeField] private NetworkObject matchStateNetworkObject;

    [Tooltip("Shows the join code to the host after hosting starts, so they can " +
             "share it. Optional - leave empty if you don't want this.")]
    [SerializeField] private TMP_Text joinCodeDisplayText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private bool sceneManagerHooked;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // NetworkManager.SceneManager doesn't exist yet at this point - it's only
        // created once the network actually starts (after StartHost/StartClient),
        // not at scene load. So this hook is attached from HostGame()/JoinGame()
        // instead, once it's actually safe to do so - see HookSceneManagerEvents().
    }

    private void OnApplicationQuit()
    {
        // Shut down cleanly before Unity destroys NetworkManager, otherwise
        // NetworkSceneManager.Dispose can NRE during quit.
        SafeShutdownNetwork();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            if (sceneManagerHooked && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }

        SafeShutdownNetwork();

        if (Instance == this)
            Instance = null;
    }

    void SafeShutdownNetwork()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            // Despawn first so NetworkObject.OnDestroy doesn't NRE on a torn-down SpawnManager.
            if (nm.SpawnManager != null && nm.SpawnManager.SpawnedObjectsList != null)
            {
                var snapshot = new System.Collections.Generic.List<NetworkObject>(nm.SpawnManager.SpawnedObjectsList);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var no = snapshot[i];
                    if (no != null && no.IsSpawned && (nm.IsServer || nm.IsHost))
                    {
                        try
                        {
                            // Keep MatchState GameObject so a second Host can re-spawn it.
                            bool keep = no.GetComponent<MatchState>() != null;
                            no.Despawn(!keep);
                        }
                        catch { /* ignore teardown races */ }
                    }
                }
            }

            if (nm.IsListening || nm.IsConnectedClient || nm.IsServer || nm.IsClient)
                nm.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Safe network shutdown skipped: " + ex.Message);
        }
    }

    // Called right after StartHost()/StartClient() succeeds, once SceneManager
    // is guaranteed to actually exist.
    private void HookSceneManagerEvents()
    {
        if (sceneManagerHooked) return;

        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            sceneManagerHooked = true;
        }
        else
        {
            Debug.LogError("NetworkManager.SceneManager is still null after starting - " +
                            "check that 'Enable Scene Management' is ticked on the " +
                            "NetworkManager component.");
        }
    }

    private async Task EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async void HostGame()
    {
        try
        {
            await EnsureSignedInAsync();

            // maxConnections = 1 because this allocation is for the HOST plus
            // 1 other player (1v1) - it does not count the host itself.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            NetworkManager.Singleton.StartHost();
            HookSceneManagerEvents();

            if (joinCodeDisplayText != null)
                joinCodeDisplayText.text = $"Join Code: {joinCode}";

            Debug.Log($"Hosting started. Join code: {joinCode}");

            EnsureMatchStateSpawned();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to host via Relay: {e.Message}");
        }
    }

    void EnsureMatchStateSpawned()
    {
        // Prefer existing live MatchState (survives leave if we despawn without destroy).
        if (MatchState.Instance != null)
        {
            var existing = MatchState.Instance.GetComponent<NetworkObject>();
            if (existing != null)
            {
                matchStateNetworkObject = existing;
                if (!existing.IsSpawned)
                    existing.Spawn();
                return;
            }
        }

        if (matchStateNetworkObject != null)
        {
            // Unity fake-null after destroy.
            try
            {
                if (!matchStateNetworkObject.IsSpawned)
                    matchStateNetworkObject.Spawn();
                return;
            }
            catch
            {
                matchStateNetworkObject = null;
            }
        }

        Debug.LogError("MatchState missing after leave — reload the Menu scene, then Host again.");
    }

    public async void JoinGame(string joinCode)
    {
        try
        {
            await EnsureSignedInAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.StartClient();
            HookSceneManagerEvents();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join via Relay (check the code is correct): {e.Message}");
        }
    }

    // Fires on the SERVER only when any client (including the host itself)
    // finishes connecting. Once both players (host + 1 client = 2 total) are
    // in, move everyone to the select scene automatically.
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 2)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(selectSceneName, LoadSceneMode.Single);
        }
    }

    // Fires every time ANY networked scene load finishes - including rematches,
    // not just the first time. Whenever that scene is the select screen, reset
    // the match state and make sure a fresh CharacterSelectNetwork exists for it.
    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (sceneName != selectSceneName) return;

        if (MatchState.Instance != null)
            MatchState.Instance.ResetForNewMatchServer();

#if UNITY_2023_1_OR_NEWER
        var selectNetwork = FindFirstObjectByType<CharacterSelectNetwork>(FindObjectsInactive.Include);
#else
        var selectNetwork = FindObjectOfType<CharacterSelectNetwork>(true);
#endif
        var selectNetworkObject = selectNetwork != null ? selectNetwork.GetComponent<NetworkObject>() : null;

        if (selectNetwork != null)
            selectNetwork.ResetReady();

        if (selectNetworkObject != null && !selectNetworkObject.IsSpawned)
            selectNetworkObject.Spawn();
    }
}
