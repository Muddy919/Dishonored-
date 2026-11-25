using UnityEngine;
using UnityEngine.InputSystem;

public class jump : MonoBehaviour
{
    public Rigidbody rb;
    Animator animator;
    Collider[] colliders;

    [Header("Jump")]
    [Tooltip("Initial upward speed in m/s. ~7-9 feels natural.")]
    public float jumpForce = 8.5f;
    public float jumpInterval = 0.4f;

    float nextJumpTime;
    float groundedGraceUntil;

    [Header("Gravity Feel")]
    [Tooltip("1 = real gravity while rising.")]
    public float risingGravityMultiplier = 1.15f;
    [Tooltip("Higher = snappier, natural-feeling fall.")]
    public float fallingGravityMultiplier = 3.6f;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>();

        // Natural arcade jump — not floaty, not rocket.
        jumpForce = 14f;
        jumpInterval = 0.4f;
        risingGravityMultiplier = 1.25f;
        fallingGravityMultiplier = 3.4f;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    void Update()
    {
        if (IsGrounded())
            groundedGraceUntil = Time.time + 0.15f;

        bool pressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || Input.GetKeyDown(KeyCode.Space);

        if (!pressed || rb == null)
            return;

        if (Time.time < nextJumpTime)
            return;

        bool canJump = Time.time <= groundedGraceUntil || Mathf.Abs(rb.linearVelocity.y) < 0.35f;
        if (!canJump)
            return;

        DoJump();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        // Stronger pull once past the apex so landing feels like real gravity.
        float multiplier = rb.linearVelocity.y > 0.05f
            ? risingGravityMultiplier
            : fallingGravityMultiplier;

        rb.AddForce(Physics.gravity * multiplier, ForceMode.Acceleration);

        // Soft terminal velocity so fall stays readable, not endless float.
        Vector3 v = rb.linearVelocity;
        if (v.y < -28f)
        {
            v.y = -28f;
            rb.linearVelocity = v;
        }
    }

    bool IsGrounded()
    {
        float footY = transform.position.y;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled || c.isTrigger)
                    continue;
                footY = Mathf.Min(footY, c.bounds.min.y);
            }
        }

        Vector3 origin = new Vector3(transform.position.x, footY + 0.08f, transform.position.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 0.55f, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null)
                continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            return true;
        }

        return false;
    }

    void DoJump()
    {
        Vector3 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;

        if (animator != null)
            animator.SetTrigger("Jump");

        nextJumpTime = Time.time + jumpInterval;
        groundedGraceUntil = 0f;
    }
}
