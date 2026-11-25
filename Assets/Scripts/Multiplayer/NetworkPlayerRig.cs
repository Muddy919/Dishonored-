using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Put this on the ROOT of each multiplayer fighter prefab, alongside its
/// NetworkObject, NetworkTransform, and PlayerHealth components.
///
/// On spawn, the player who OWNS this fighter keeps everything enabled. On every
/// other machine this fighter is just a remote body driven by NetworkTransform.
/// Does nothing in singleplayer (network not listening).
/// </summary>
public class NetworkPlayerRig : NetworkBehaviour
{
    [Header("Disable on remote (non-owner) instances")]
    [SerializeField] private Behaviour[] ownerOnlyScripts;
    [SerializeField] private GameObject[] ownerOnlyCameraRigs;

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        bool isLocalPlayer = IsOwner;

        if (ownerOnlyScripts != null)
        {
            foreach (var script in ownerOnlyScripts)
            {
                if (script != null)
                    script.enabled = isLocalPlayer;
            }
        }

        if (ownerOnlyCameraRigs != null)
        {
            foreach (var rig in ownerOnlyCameraRigs)
            {
                if (rig == null)
                    continue;
                if (!rig.transform.IsChildOf(transform) && rig.transform != transform)
                    continue;
                rig.SetActive(isLocalPlayer);
            }
        }

        if (!isLocalPlayer)
        {
            foreach (var cam in GetComponentsInChildren<Camera>(true))
                cam.gameObject.SetActive(false);

            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

            // Prefabs often omit these from ownerOnlyScripts — force-disable so
            // remotes don't play your footsteps / shoot SFX / drive attacks.
            foreach (var sfx in GetComponentsInChildren<PlayerSFXWithIntervals>(true))
                sfx.enabled = false;

            var attack = GetComponentInChildren<AttackPacerLongLegs>(true);
            if (attack != null)
                attack.enabled = false;

            var melee = GetComponentInChildren<Attack>(true);
            if (melee != null)
                melee.enabled = false;

            var look = GetComponentInChildren<MouseLook>(true);
            if (look != null)
                look.enabled = false;

            var move = GetComponentInChildren<Move>(true);
            if (move != null)
                move.enabled = false;

            var jumpCtrl = GetComponentInChildren<jump>(true);
            if (jumpCtrl != null)
                jumpCtrl.enabled = false;

            var camSwitch = GetComponentInChildren<CameraSwitcher>(true);
            if (camSwitch != null)
                camSwitch.enabled = false;
        }
    }
}
