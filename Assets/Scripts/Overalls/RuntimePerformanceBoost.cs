using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Light FPS trims that do NOT blur the image (no render-scale downsampling).
/// </summary>
public class RuntimePerformanceBoost : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 2;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 40f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.lodBias = 1f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

        SoftenUrp();

        var host = new GameObject("RuntimePerformanceBoost");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<RuntimePerformanceBoost>();
    }

    static void SoftenUrp()
    {
        var asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null)
            return;

        // Keep full resolution — blur was from renderScale < 1.
        SetProp(asset, "renderScale", 1f);
        SetProp(asset, "shadowDistance", 40f);
        SetProp(asset, "mainLightShadowmapResolution", 2048);
        SetProp(asset, "additionalLightsShadowmapResolution", 1024);
        SetProp(asset, "maxAdditionalLightsCount", 4);
        SetProp(asset, "supportsCameraDepthTexture", false);
        SetProp(asset, "supportsCameraOpaqueTexture", false);
        SetProp(asset, "supportsAdditionalLightShadows", false);
        SetProp(asset, "msaaSampleCount", 2);
    }

    static void SetProp(Object target, string name, object value)
    {
        var prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (prop == null || !prop.CanWrite)
            return;

        try
        {
            prop.SetValue(target, value, null);
        }
        catch
        {
        }
    }
}
