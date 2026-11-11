using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UrpDependencyCleanup
{
    public static void RefreshGraph()
    {
        foreach (var guid in AssetDatabase.FindAssets("", new[] { "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".shadergraph")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
    public static void ValidateCode()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run validation outside Play Mode.");
        new UrpQualitySettingsTests().PresetsUseRuntimeUrpCloneAndRestoreAuthoredPipeline();
        new DLSSSettingsTests().DLSSPreferenceRoundTripsAndOldSettingsDefaultOff();
        new DLSSSettingsTests().PrefabsAndPipelineHaveDLSSWiredBelowVSync();
        MaterialPipelineConverterValidation.Run();
        HeldToolUrpValidation.Validate();
        Directory.CreateDirectory("output/Rendering/URPOnly");
        File.WriteAllText("output/Rendering/URPOnly/code-tests.txt", "PASS URP quality presets, preserved preferences, camera setup, material conversion and held-tool rendering.");
        Debug.Log("PASS URP dependency removal regression checks.");
    }
    public static void ConvertLegacyMaterials()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before material migration.");
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !material.shader) continue;
            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            if (!shaderPath.Contains("Polygonmaker")) continue;
            string backup = "DisabledAssets/HDRPMaterialBackups/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(path, backup);
            Directory.CreateDirectory("Assets/URPConverted");
            var converted = MaterialPipelineConverter.ConvertMaterial(material, MaterialPipelineConverter.Target.URP, "Assets/URPConverted", guid);
            try { EditorUtility.CopySerialized(converted, material); material.name = Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(material); changed++; }
            finally { UnityEngine.Object.DestroyImmediate(converted); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("URP material migration complete: " + changed);
        Directory.CreateDirectory("output/Rendering/URPOnly");
        File.WriteAllText("output/Rendering/URPOnly/materials.txt", "Converted legacy material assets: " + changed);
    }

    public static void AuditShaders()
    {
        var errors = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader) continue;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    errors.Add(path + ": " + message.message);
        }
        Directory.CreateDirectory("output/Rendering/URPOnly");
        File.WriteAllLines("output/Rendering/URPOnly/shader-errors.txt", errors);
        Debug.Log("Shader errors: " + errors.Count + "\n" + string.Join("\n", errors.Take(30)));
    }
}
