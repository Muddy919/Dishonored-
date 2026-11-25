using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked state shared for the whole match: character picks and scores.
/// Needs a NetworkObject component. GameNetworkManager spawns this
/// automatically when hosting starts - see GameNetworkManager.cs.
/// </summary>
public class MatchState : NetworkBehaviour
{
    public static MatchState Instance { get; private set; }

    public NetworkVariable<int> HostCharacterIndex = new NetworkVariable<int>(-1);
    public NetworkVariable<int> ClientCharacterIndex = new NetworkVariable<int>(-1);

    public NetworkVariable<int> HostScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> ClientScore = new NetworkVariable<int>(0);

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);
    }

    // Called by GameNetworkManager when players return to select for a
    // rematch, so the new match starts from a clean slate.
    public void ResetForNewMatchServer()
    {
        if (!IsServer) return;

        HostCharacterIndex.Value = -1;
        ClientCharacterIndex.Value = -1;
        HostScore.Value = 0;
        ClientScore.Value = 0;
    }
}
