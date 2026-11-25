using TMPro;
using UnityEngine;

/// <summary>
/// Put this on your Join button (or the Multiplayer panel). Drag your join-code
/// TMP_InputField into codeInputField (relabel it from "IP Address" to "Join
/// Code" in the UI). On the Join Button's OnClick(), remove any existing call to
/// GameNetworkManager.JoinGame and replace it with THIS script's
/// OnJoinClicked() instead - it has no parameters, so there's nothing to
/// accidentally hardcode.
/// </summary>
public class JoinButtonHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInputField;

    public void OnJoinClicked()
    {
        if (codeInputField == null)
        {
            Debug.LogError("JoinButtonHandler: codeInputField is not assigned.");
            return;
        }

        string code = codeInputField.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Type a join code before joining.");
            return;
        }

        GameNetworkManager.Instance.JoinGame(code);
    }
}
