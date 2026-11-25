using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSFXWithIntervals : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip shootSound;
    public AudioClip jumpSound;

    [Range(0f, 1f)] public float walkVolume = 0.45f;
    [Range(0f, 1f)] public float shootVolume = 0.72f;
    [Range(0f, 1f)] public float jumpVolume = 0.45f;

    public float walkInterval = 0.38f;
    public float shootInterval = 0.25f;
    public float jumpInterval = 0.55f;

    float walkTimer;
    float shootTimer;
    float jumpTimer;

    static AudioClip sharedWalk;
    static AudioClip sharedShoot;
    static AudioClip sharedJump;
    static bool clipsLoaded;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.mute = false;
        audioSource.enabled = true;
        audioSource.volume = 1f;

        walkInterval = 0.38f;
        shootInterval = 0.18f;
        jumpInterval = 0.55f;
        shootVolume = Mathf.Max(shootVolume, 0.72f);

        ForceNewClips();
        CameraSwitcher.EnforceSingleListener();
    }

    void ForceNewClips()
    {
        LoadSharedClipsOnce();
        walkSound = sharedWalk;
        shootSound = sharedShoot;
        jumpSound = sharedJump;
    }

    static void LoadSharedClipsOnce()
    {
        if (clipsLoaded)
            return;
        clipsLoaded = true;

        sharedWalk = Resources.Load<AudioClip>("PlayerSFX/SoftWalk");
        sharedShoot = Resources.Load<AudioClip>("PlayerSFX/SoftShoot");
        sharedJump = Resources.Load<AudioClip>("PlayerSFX/SoftJump");

        if (sharedWalk == null) sharedWalk = ProceduralSfx.MakeFootstep();
        if (sharedShoot == null) sharedShoot = ProceduralSfx.MakeShoot();
        if (sharedJump == null) sharedJump = ProceduralSfx.MakeJump();
    }

    void Update()
    {
        if (audioSource == null)
            return;

        float delta = Time.deltaTime;
        walkTimer += delta;
        shootTimer += delta;
        jumpTimer += delta;

        bool moving = Keyboard.current != null && (
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.dKey.isPressed);

        if (moving && walkSound != null && walkTimer >= walkInterval)
        {
            audioSource.PlayOneShot(walkSound, walkVolume);
            walkTimer = 0f;
        }

        bool shootPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (shootPressed && shootSound != null && shootTimer >= shootInterval)
        {
            audioSource.PlayOneShot(shootSound, shootVolume);
            shootTimer = 0f;
        }

        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed && jumpSound != null && jumpTimer >= jumpInterval)
        {
            audioSource.PlayOneShot(jumpSound, jumpVolume);
            jumpTimer = 0f;
        }
    }
}

public static class ProceduralSfx
{
    public static AudioClip MakeFootstep()
    {
        return Build("ProcWalk", 0.09f, (t, life) =>
        {
            float e = Mathf.Exp(-40f * t);
            return (0.55f * Mathf.Sin(2f * Mathf.PI * 95f * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * 380f * t)
                    + 0.08f * (Random.value * 2f - 1f)) * e;
        });
    }

    public static AudioClip MakeShoot()
    {
        return Build("ProcShoot", 0.12f, (t, life) =>
        {
            float freq = 900f * Mathf.Exp(-7f * t) + 180f;
            float e = Mathf.Exp(-18f * t);
            return (0.5f * Mathf.Sin(2f * Mathf.PI * freq * t)
                    + 0.18f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t)
                    + 0.1f * (Random.value * 2f - 1f) * Mathf.Exp(-30f * t)) * e;
        });
    }

    public static AudioClip MakeJump()
    {
        return Build("ProcJump", 0.16f, (t, life) =>
        {
            float freq = 160f + 480f * (t / life);
            float e = Mathf.Exp(-10f * t);
            return (0.42f * Mathf.Sin(2f * Mathf.PI * freq * t)
                    + 0.12f * (Random.value * 2f - 1f) * Mathf.Exp(-14f * t)) * e;
        });
    }

    public static AudioClip MakeDash()
    {
        return Build("ProcDash", 0.14f, (t, life) =>
        {
            float freq = 240f * Mathf.Exp(-3f * t) + 100f;
            float e = Mathf.Exp(-14f * t);
            return (0.35f * Mathf.Sin(2f * Mathf.PI * freq * t)
                    + 0.22f * (Random.value * 2f - 1f) * Mathf.Exp(-16f * t)) * e;
        });
    }

    static AudioClip Build(string name, float seconds, System.Func<float, float, float> sample)
    {
        int sr = 44100;
        int count = Mathf.CeilToInt(sr * seconds);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sr;
            data[i] = Mathf.Clamp(sample(t, seconds), -1f, 1f) * 0.85f;
        }

        AudioClip clip = AudioClip.Create(name, count, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
