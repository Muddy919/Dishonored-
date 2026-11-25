using UnityEngine;

public class billboardscript : MonoBehaviour
{
    public Transform cam;

    void Start()
    {
        ResolveCamera();
    }

    void LateUpdate()
    {
        if (cam == null)
            ResolveCamera();

        if (cam == null)
            return;

        transform.LookAt(transform.position + cam.forward);
    }

    void ResolveCamera()
    {
        if (cam != null)
            return;

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        var anyCam = FindFirstObjectByType<Camera>();
#else
        var anyCam = FindObjectOfType<Camera>();
#endif
        if (anyCam != null)
            cam = anyCam.transform;
    }
}
