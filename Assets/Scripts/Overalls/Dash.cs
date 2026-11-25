using UnityEngine;

public class Dash : MonoBehaviour
{
    public Rigidbody rb;
    private Animator animator;

    public float dashForce = 10f;
    public float dashCooldown = 1f;

    private float nextDashTime = 0f;
    AudioSource audioSource;
    AudioClip dashClip;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogError("No Animator found on child mesh!");

        audioSource = GetComponent<AudioSource>();
        dashClip = Resources.Load<AudioClip>("PlayerSFX/SoftDash");
        if (dashClip == null)
            dashClip = ProceduralSfx.MakeDash();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= nextDashTime)
            DashForward();
    }

    void DashForward()
    {
        if (rb != null)
            rb.AddForce(transform.forward * dashForce, ForceMode.Impulse);

        if (animator != null)
            animator.Play("Dash", 0, 0f);

        if (audioSource != null && dashClip != null)
            audioSource.PlayOneShot(dashClip, 0.3f);

        nextDashTime = Time.time + dashCooldown;
    }
}
