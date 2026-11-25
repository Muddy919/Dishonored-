using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces enemy gunshots with fun laser clips at runtime.
/// </summary>
public class EnemyLaserSfx : MonoBehaviour
{
    static AudioClip zap;
    static AudioClip burst;
    static AudioClip sniper;
    static bool loaded;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EnemyLaserSfx>() != null)
            return;
#else
        if (FindObjectOfType<EnemyLaserSfx>() != null)
            return;
#endif

        var host = new GameObject("EnemyLaserSfx");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<EnemyLaserSfx>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadClips();
        ApplyToScene();
        StartCoroutine(DelayedApply());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadClips();
        ApplyToScene();
        StartCoroutine(DelayedApply());
    }

    System.Collections.IEnumerator DelayedApply()
    {
        yield return null;
        yield return null;
        ApplyToScene();
    }

    static void LoadClips()
    {
        if (loaded && zap != null && burst != null && sniper != null)
            return;

        loaded = true;
        zap = Resources.Load<AudioClip>("EnemySFX/LaserZap");
        burst = Resources.Load<AudioClip>("EnemySFX/LaserBurst");
        sniper = Resources.Load<AudioClip>("EnemySFX/LaserSniper");

        if (zap == null) zap = MakeLaser("LaserZapProc", 0.15f, 1400f, 260f);
        if (burst == null) burst = MakeLaser("LaserBurstProc", 0.1f, 980f, 180f);
        if (sniper == null) sniper = MakeLaser("LaserSniperProc", 0.2f, 620f, 140f);
    }

    static void ApplyToScene()
    {
        if (zap == null)
            return;

#if UNITY_2023_1_OR_NEWER
        foreach (var e in Object.FindObjectsByType<MachineGunEnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (e != null) e.shootSound = burst;
        foreach (var e in Object.FindObjectsByType<ShotgunEnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (e != null) e.shootSound = zap;
        foreach (var e in Object.FindObjectsByType<SniperEnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (e != null) e.shootSound = sniper;
#else
        foreach (var e in Object.FindObjectsOfType<MachineGunEnemyAI>())
            if (e != null) e.shootSound = burst;
        foreach (var e in Object.FindObjectsOfType<ShotgunEnemyAI>())
            if (e != null) e.shootSound = zap;
        foreach (var e in Object.FindObjectsOfType<SniperEnemyAI>())
            if (e != null) e.shootSound = sniper;
#endif
    }

    static AudioClip MakeLaser(string name, float seconds, float startFreq, float endFreq)
    {
        int sampleRate = 44100;
        int count = Mathf.CeilToInt(sampleRate * seconds);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float life = t / seconds;
            float e = Mathf.Clamp01(life / 0.02f) * Mathf.Clamp01((1f - life) / 0.12f);
            float freq = Mathf.Lerp(startFreq, endFreq, life);
            float sample = 0.45f * Mathf.Sin(2f * Mathf.PI * freq * t)
                           + 0.18f * Mathf.Sin(2f * Mathf.PI * freq * 2.15f * t)
                           + 0.08f * (((i * 1103515245 + 12345) % 1000) / 500f - 1f) * Mathf.Exp(-20f * t);
            data[i] = Mathf.Clamp(sample * e, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
