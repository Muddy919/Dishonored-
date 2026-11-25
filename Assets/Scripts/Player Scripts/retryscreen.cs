using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneSwitch : MonoBehaviour
{
    public void LoadNextScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;

        FastSceneLoader.Load(currentIndex + 1);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}