using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shuts down Netcode cleanly before singleplayer scene loads so
/// NetworkObject.OnDestroy does not NullRef. Does NOT destroy random
/// NetworkObjects (that could wipe fighters / break AI targeting).
/// </summary>
public class NetworkPlayCleanup : MonoBehaviour
{
    static NetworkPlayCleanup instance;
    static bool shuttingDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        shuttingDown = false;
    }

    public static IEnumerator PrepareRoutine()
    {
        yield return Ensure().ShutdownThenContinue();
    }

    static NetworkPlayCleanup Ensure()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("NetworkPlayCleanup");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<NetworkPlayCleanup>();
        return instance;
    }

    IEnumerator ShutdownThenContinue()
    {
        if (shuttingDown)
        {
            while (shuttingDown)
                yield return null;
            yield break;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null)
            yield break;

        // Already idle — nothing to clean.
        if (!nm.IsListening && !nm.IsConnectedClient && !nm.IsServer && !nm.IsClient)
            yield break;

        shuttingDown = true;
        try
        {
            if (nm.SpawnManager != null)
            {
                List<NetworkObject> snapshot = new List<NetworkObject>();
                try
                {
                    if (nm.SpawnManager.SpawnedObjectsList != null)
                    {
                        foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                        {
                            if (no != null)
                                snapshot.Add(no);
                        }
                    }
                }
                catch
                {
                }

                for (int i = 0; i < snapshot.Count; i++)
                {
                    NetworkObject no = snapshot[i];
                    if (no == null || !no.IsSpawned)
                        continue;
                    if (!(nm.IsServer || nm.IsHost))
                        continue;

                    try
                    {
                        // Keep MatchState alive for a later Host; destroy fighters.
                        bool keep = no.GetComponent<MatchState>() != null;
                        no.Despawn(!keep);
                    }
                    catch
                    {
                    }
                }
            }

            yield return null;

            if (nm != null && (nm.IsListening || nm.IsConnectedClient || nm.IsServer || nm.IsClient))
            {
                try
                {
                    nm.Shutdown();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Network shutdown: " + ex.Message);
                }
            }

            float t = 0f;
            while (nm != null && nm.ShutdownInProgress && t < 2f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            shuttingDown = false;
        }
    }
}
