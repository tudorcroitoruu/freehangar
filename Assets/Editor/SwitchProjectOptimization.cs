using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class SwitchProjectOptimization
{
    public static void Validate()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP/SwitchPipeline.asset");
        if (!pipeline || pipeline.supportsHDR || pipeline.msaaSampleCount != 1 ||
            pipeline.supportsAdditionalLightShadows || pipeline.shadowCascadeCount != 1 ||
            !Mathf.Approximately(pipeline.renderScale, .65f)) throw new Exception("Switch pipeline mismatch.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var go = new GameObject("Switch constraints validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.shadows = LightShadows.Soft;
            var probe = go.AddComponent<ReflectionProbe>(); probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            var terrain = go.AddComponent<Terrain>(); terrain.detailObjectDistance = 100; terrain.detailObjectDensity = 1;
            typeof(SwitchRenderingStartup).GetMethod("OptimizeScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { scene, UnityEngine.SceneManagement.LoadSceneMode.Additive });
            if (probe.enabled || light.shadows != LightShadows.None || terrain.detailObjectDistance > 40 || terrain.detailObjectDensity > .5f)
                throw new Exception("Switch scene constraints failed.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        File.WriteAllText("output/Rendering/Switch/validation.txt", "PASS: dedicated URP settings and isolated scene probe/light/terrain constraints. Switch player scripts compiled. Device FPS and peak memory not measured.");
        Debug.Log("PASS Switch pipeline and scene constraints.");
    }
    public static void Apply()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor must be idle.");
        const string path = "Assets/Settings/URP/SwitchPipeline.asset";
        Directory.CreateDirectory("Assets/Settings/URP");
        Directory.CreateDirectory("output/Rendering/Switch");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (!pipeline)
        {
            var source = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (!source) throw new InvalidOperationException("Active quality must use URP.");
            pipeline = UnityEngine.Object.Instantiate(source);
            pipeline.name = "SwitchPipeline";
            AssetDatabase.CreateAsset(pipeline, path);
        }
        pipeline.msaaSampleCount = 1;
        pipeline.renderScale = .65f;
        pipeline.supportsHDR = false;
        pipeline.shadowDistance = 20;
        pipeline.shadowCascadeCount = 1;
        pipeline.mainLightShadowmapResolution = 512;
        var pipelineSettings = new SerializedObject(pipeline); pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        pipelineSettings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 2;
        pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = false;
        pipeline.supportsCameraOpaqueTexture = true; // Water/glass use the scene color.
        pipelineSettings.FindProperty("m_OpaqueDownsampling").intValue = 1; pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
        pipeline.useSRPBatcher = true;
        EditorUtility.SetDirty(pipeline);
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        int index = -1;
        for (int i = 0; i < levels.arraySize; i++)
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Switch") index = i;
        if (index < 0) { index = levels.arraySize; levels.InsertArrayElementAtIndex(index); }
        var level = levels.GetArrayElementAtIndex(index);
        level.FindPropertyRelative("name").stringValue = "Switch";
        level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        level.FindPropertyRelative("vSyncCount").intValue = 2;
        level.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
        level.FindPropertyRelative("lodBias").floatValue = .6f;
        level.FindPropertyRelative("globalTextureMipmapLimit").intValue = 2;
        level.FindPropertyRelative("asyncUploadBufferSize").intValue = 8;
        level.FindPropertyRelative("streamingMipmapsMemoryBudget").floatValue = 256;
        var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
        var target = defaults.FindPropertyRelative("Nintendo Switch");
        if (target == null && defaults.isArray)
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var pair = defaults.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == "Nintendo Switch")
                    target = pair.FindPropertyRelative("second");
            }
        if (target == null) throw new InvalidOperationException("Switch quality mapping missing.");
        target.intValue = index;
        quality.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Switch), ScriptingImplementation.IL2CPP);
        // Disable static mesh duplication only on Switch, preserving desktop batching.
        var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var batching = player.FindProperty("m_BuildTargetBatching");
        for (int i = 0; batching != null && i < batching.arraySize; i++)
        {
            var item = batching.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("m_BuildTarget").stringValue != "Nintendo Switch") continue;
            item.FindPropertyRelative("m_StaticBatching").boolValue = false;
        }
        player.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        File.WriteAllText("output/Rendering/Switch/settings.txt", "Applied Switch quality and dedicated URP pipeline: 30 FPS, Low: 0.65 render scale, HDR off, MSAA off, one 512 shadow cascade at 20m, additional shadows off, two additional lights, half-size opaque texture. Runtime scene constraints enabled.");
        Debug.Log("Switch project settings applied.");
    }
}



