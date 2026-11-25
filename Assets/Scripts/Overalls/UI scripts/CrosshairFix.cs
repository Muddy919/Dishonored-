using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restores the original aim cursors and removes the temporary redesign overlay.
/// </summary>
public class CrosshairFix : MonoBehaviour
{
    const string OriginalSpriteGuidHint = "Gemini_Generated_Image"; // original pack sprite name fragment

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<CrosshairFix>() != null)
            return;
#else
        if (FindObjectOfType<CrosshairFix>() != null)
            return;
#endif

        var host = new GameObject("CrosshairFix");
        host.AddComponent<CrosshairFix>();
    }

    void Start()
    {
        Invoke(nameof(RestoreOriginal), 0.05f);
    }

    void RestoreOriginal()
    {
        // Remove the temporary redesign overlay if present.
        var clean = GameObject.Find("CleanCrosshair");
        if (clean != null)
            Destroy(clean);

        RestoreCursor("AimCursorFTP", true);
        RestoreCursor("AimCursorTTP", false);
    }

    void RestoreCursor(string objectName, bool showByDefault)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
            return;

        var image = go.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = showByDefault;
            image.raycastTarget = false;
        }

        var pop = go.GetComponent<UIButtonPop>();
        if (pop != null)
            pop.enabled = showByDefault;

        go.SetActive(showByDefault);
    }
}
