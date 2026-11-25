using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to your existing player-select screen (needs a NetworkObject and must
/// be spawned, e.g. by the host right after StartHost(), similar to MatchState).
///
/// Keep your existing UI and character-picking logic exactly as it is - just call
/// these two methods from the handlers you already have:
///   - When the player clicks/confirms a character: SubmitChoice(characterIndex)
///   - When the player presses "Ready"/"Start":       SetReady()
///
/// Once BOTH players have called SetReady(), the host loads the Battle scene for
/// everyone via NetworkManager's scene management, so no manual scene sync needed.
/// </summary>
public class CharacterSelectNetwork : NetworkBehaviour
{
    [SerializeField] private string battleSceneName = "1V1 Map";

    private bool hostReady;
    private bool clientReady;

    public override void OnNetworkSpawn()
    {
        ResetReady();

        // Older scenes stored "1v1 MAP" — Build Settings name is "1V1 Map".
        if (string.IsNullOrEmpty(battleSceneName)
            || battleSceneName.Equals("1v1 MAP", System.StringComparison.OrdinalIgnoreCase)
            || battleSceneName.Equals("Battle", System.StringComparison.OrdinalIgnoreCase))
            battleSceneName = "1V1 Map";
    }

    public void ResetReady()
    {
        hostReady = false;
        clientReady = false;
    }

    public void SubmitChoice(int characterIndex)
    {
        SubmitChoiceServerRpc(characterIndex);
    }

    public void SetReady()
    {
        SetReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitChoiceServerRpc(int characterIndex, ServerRpcParams rpcParams = default)
    {
        if (MatchState.Instance == null)
        {
            Debug.LogError("CharacterSelectNetwork: MatchState missing — host again from the Menu.");
            return;
        }

        bool isHostCaller = rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId;
        if (isHostCaller)
            MatchState.Instance.HostCharacterIndex.Value = characterIndex;
        else
            MatchState.Instance.ClientCharacterIndex.Value = characterIndex;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        bool isHostCaller = rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId;
        if (isHostCaller) hostReady = true; else clientReady = true;

        if (hostReady && clientReady)
            NetworkManager.SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
    }
}
