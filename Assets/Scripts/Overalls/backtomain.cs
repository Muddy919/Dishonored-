using UnityEngine;
using UnityEngine.SceneManagement;

public class backtomain : MonoBehaviour
{
    public void BackToMain()
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();
        SceneManager.LoadScene(0);
    }
}
