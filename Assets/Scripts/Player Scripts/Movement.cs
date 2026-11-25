using UnityEngine;
using UnityEngine.InputSystem;

public class Move : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 40f;
    public float acceleration = 80f;
    public float deceleration = 90f;

    Rigidbody rb;
    Animator animator;
    Vector3 currentPlanarVelocity;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();

        // Force fast movement on every fighter.
        speed = 40f;
        acceleration = 80f;
        deceleration = 90f;

        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (animator == null)
            Debug.LogError("No Animator found on Player or its children!");

        LevelObjectiveHud.Ensure();
    }

    void Update()
    {
        if (PauseMenu.IsPaused || GameplayDeathOverlay.IsOpen)
            return;

        // Read from BOTH Input System + legacy — D was silently failing when
        // Keyboard.current existed but didn't report dKey correctly.
        bool forward = IsPressed(Key.W, KeyCode.W) || IsPressed(Key.UpArrow, KeyCode.UpArrow);
        bool back = IsPressed(Key.S, KeyCode.S) || IsPressed(Key.DownArrow, KeyCode.DownArrow);
        bool left = IsPressed(Key.A, KeyCode.A) || IsPressed(Key.LeftArrow, KeyCode.LeftArrow);
        bool right = IsPressed(Key.D, KeyCode.D) || IsPressed(Key.RightArrow, KeyCode.RightArrow);

        GetMoveAxes(out Vector3 flatForward, out Vector3 flatRight);

        Vector3 moveDir = Vector3.zero;
        if (forward) moveDir += flatForward;
        if (back) moveDir -= flatForward;
        if (left) moveDir -= flatRight;
        if (right) moveDir += flatRight;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();
        else if (moveDir.sqrMagnitude > 0.0001f)
            moveDir.Normalize();

        Vector3 targetVelocity = moveDir * speed;

        float rate = moveDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            targetVelocity,
            rate * Time.deltaTime);

        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = currentPlanarVelocity.x;
            vel.z = currentPlanarVelocity.z;
            rb.linearVelocity = vel;
        }
        else
        {
            transform.position += currentPlanarVelocity * Time.deltaTime;
        }

        if (animator != null)
            animator.SetFloat("Speed", currentPlanarVelocity.magnitude / Mathf.Max(speed, 0.01f));
    }

    static bool IsPressed(Key key, KeyCode code)
    {
        bool neo = Keyboard.current != null && Keyboard.current[key].isPressed;
        bool legacy = Input.GetKey(code);
        return neo || legacy;
    }

    void GetMoveAxes(out Vector3 flatForward, out Vector3 flatRight)
    {
        // Prefer camera axes so A/D always strafe screen-left/right even if
        // the body mesh / MouseLook orientation is weird.
        Transform cam = null;
        if (Camera.main != null && Camera.main.enabled)
            cam = Camera.main.transform;
        else
        {
            var cams = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].enabled)
                {
                    cam = cams[i].transform;
                    break;
                }
            }
        }

        if (cam != null)
        {
            flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            flatRight = Vector3.ProjectOnPlane(cam.right, Vector3.up);
        }
        else
        {
            flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        else
            flatForward.Normalize();

        if (flatRight.sqrMagnitude < 0.0001f)
            flatRight = Vector3.right;
        else
            flatRight.Normalize();
    }
}
