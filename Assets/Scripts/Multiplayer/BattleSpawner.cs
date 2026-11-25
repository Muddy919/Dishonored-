using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Put this on an empty GameObject in the Battle scene, with a NetworkObject
/// component (it gets spawned automatically because it's part of the scene the
/// host loads via NetworkManager.SceneManager).
///
/// In the Inspector:
///   - characterPrefabs: drag in your 9 fighter prefabs, in the SAME order they
///     appear on the select screen. Each prefab needs a NetworkObject component.
///   - hostSpawnPoint / clientSpawnPoint: two empty Transforms marking where each
///     player appears on the fight plane.
/// </summary>
public class BattleSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private Transform hostSpawnPoint;
    [SerializeField] private Transform clientSpawnPoint;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (MatchState.Instance == null)
        {
            Debug.LogError("BattleSpawner: MatchState missing — return to Menu and Host again.");
            return;
        }

        // Order must match CharacterSelect button characterIndex:
        // 0 Pacer, 1 Buster, 2 Gastino, 3 Xebec, 4 Hendrix,
        // 5 Bigbata, 6 Macky, 7 Rodeo, 8 Glamour

        // Rematch safety — despawn any leftover player objects first.
        PlayerHealth.DespawnAllFighters();

        SpawnFighter(NetworkManager.ServerClientId,
            MatchState.Instance.HostCharacterIndex.Value, hostSpawnPoint);

        foreach (var clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId) continue;
            SpawnFighter(clientId, MatchState.Instance.ClientCharacterIndex.Value, clientSpawnPoint);
        }
    }

    private void SpawnFighter(ulong ownerClientId, int characterIndex, Transform spawnPoint)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("BattleSpawner: characterPrefabs list is empty.");
            return;
        }

        if (characterIndex < 0 || characterIndex >= characterPrefabs.Length)
            characterIndex = 0;

        if (characterPrefabs[characterIndex] == null)
        {
            Debug.LogError("BattleSpawner: prefab at index " + characterIndex + " is null.");
            return;
        }

        var go = Instantiate(characterPrefabs[characterIndex], spawnPoint.position, spawnPoint.rotation);
        var net = go.GetComponent<NetworkObject>();
        if (net == null)
        {
            Debug.LogError("BattleSpawner: fighter prefab missing NetworkObject.");
            Destroy(go);
            return;
        }

        net.SpawnAsPlayerObject(ownerClientId);
    }
}
