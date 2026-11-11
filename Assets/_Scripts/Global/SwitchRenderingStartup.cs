using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>Apply console rendering constraints before scene probes can render.</summary>
public static class SwitchRenderingStartup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        int level = System.Array.IndexOf(QualitySettings.names, "Switch");
        if (level >= 0) QualitySettings.SetQualityLevel(level, true);
        var pipeline = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        Debug.Log($"[Switch rendering v2] quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pipeline={pipeline?.name} additionalShadows={pipeline?.supportsAdditionalLightShadows} scale={pipeline?.renderScale} targetFPS=30");
        if (!pipeline || pipeline.supportsAdditionalLightShadows)
            Debug.LogError("Switch build is not using the optimized pipeline. Check the build profile's quality overrides.");
        QualitySettings.vSyncCount = 2;
        Application.targetFrameRate = 30;
        // Realtime cubemap rendering exhausted device memory during menu startup.
        // Baked/custom probe textures remain available without six extra camera renders.
        QualitySettings.realtimeReflectionProbes = false;
        SceneManager.sceneLoaded -= OptimizeScene;
        SceneManager.sceneLoaded += OptimizeScene;
#endif
    }

    private static void OptimizeScene(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var probe in root.GetComponentsInChildren<ReflectionProbe>(true))
                if (probe.mode == ReflectionProbeMode.Realtime) probe.enabled = false;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type != LightType.Directional) light.shadows = LightShadows.None;
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                terrain.heightmapPixelError = Mathf.Max(terrain.heightmapPixelError, 10);
                terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, 40);
                terrain.detailObjectDensity = Mathf.Min(terrain.detailObjectDensity, .5f);
                terrain.treeDistance = Mathf.Min(terrain.treeDistance, 250);
                terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, 40);
            }
        }
    }
}
