using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    public void Back()
    {
        BossModeState.Disable();
        ArenaBotModeState.Disable();
        SceneManager.LoadScene(0);
    }
}
