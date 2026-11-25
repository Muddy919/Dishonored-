using UnityEngine;
using UnityEngine.SceneManagement;
public class backToMenu : MonoBehaviour
{
    public void backtomenu()
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();
        SceneManager.LoadScene(0);
    }
}
