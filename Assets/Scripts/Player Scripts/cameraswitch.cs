using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    public Camera camera1;
    public Camera camera2;

    private bool usingCamera1 = true;

    void Start()
    {
        ApplyCameraState();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            usingCamera1 = !usingCamera1;
            ApplyCameraState();
        }
    }

    public void ApplyCameraState()
    {
        if (camera1 != null)
        {
            camera1.gameObject.SetActive(true);
            camera1.enabled = usingCamera1;
            SetListener(camera1, usingCamera1);
        }

        if (camera2 != null)
        {
            camera2.gameObject.SetActive(true);
            camera2.enabled = !usingCamera1;
            SetListener(camera2, !usingCamera1);
        }

        EnforceSingleListener();
    }

    static void SetListener(Camera cam, bool enabled)
    {
        if (cam == null)
            return;

        var listener = cam.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = enabled;
    }

    public static void EnforceSingleListener()
    {
#if UNITY_2023_1_OR_NEWER
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
        var listeners = Object.FindObjectsOfType<AudioListener>();
#endif
        AudioListener keep = null;

        foreach (var l in listeners)
        {
            if (l == null || !l.isActiveAndEnabled)
                continue;

            var cam = l.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                keep = l;
                break;
            }
        }

        if (keep == null)
        {
            foreach (var l in listeners)
            {
                if (l != null && l.enabled)
                {
                    keep = l;
                    break;
                }
            }
        }

        foreach (var l in listeners)
        {
            if (l == null)
                continue;
            l.enabled = (l == keep);
        }
    }
}
