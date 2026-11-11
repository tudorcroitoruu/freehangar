using UnityEngine;

/// <summary>Apply console rendering constraints before scene probes can render.</summary>
public static class SwitchRenderingStartup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        // Realtime cubemap rendering exhausted device memory during menu startup.
        // Baked/custom probe textures remain available without six extra camera renders.
        QualitySettings.realtimeReflectionProbes = false;
#endif
    }
}
