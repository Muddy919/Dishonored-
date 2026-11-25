using UnityEngine;

public class MultiplayerUI : MonoBehaviour
{
    public GameObject multiplayerPanel;

    public void OpenMultiplayerPanel()
    {
        multiplayerPanel.SetActive(true);
    }

    public void CloseMultiplayerPanel()
    {
        multiplayerPanel.SetActive(false);
    }
}