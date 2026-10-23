using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[InitializeOnLoad]
internal static class HdrpMaterialValidation
{
    static HdrpMaterialValidation() { EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (File.Exists("Temp/HdrpValidation.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete("Temp/HdrpValidation.request"); Validate(); }
    }
    [MenuItem("Tools/Rendering/Validate HDRP Materials")]
    static void Validate()
    {
        var report = new StringBuilder();
        var counts = new Dictionary<string, int>();
        var shaders = new HashSet<Shader>();
        int missing = 0, unsupported = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                string name = m.shader ? m.shader.name : "MISSING";
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
                if (m.shader == null || name == "Hidden/InternalErrorShader") { missing++; report.AppendLine("MISSING: " + path); }
                else if (!m.shader.isSupported) { unsupported++; report.AppendLine("UNSUPPORTED: " + path + " " + name); }
                if (m.shader != null && (name.StartsWith("HDRP/") || name.StartsWith("Freehangar/HDRP/") || m.GetTag("RenderPipeline", false) == "HDRenderPipeline"))
                {
                    if (shaders.Add(m.shader)) for (int pass = 0; pass < m.passCount; pass++) m.SetPass(pass);
                }
            }
        }
        int shaderErrors = 0;
        foreach (var shader in shaders)
        {
            foreach (var msg in ShaderUtil.GetShaderMessages(shader))
                if (msg.severity.ToString() == "Error") { shaderErrors++; report.AppendLine("SHADER ERROR: " + shader.name + " " + msg.message); }
        }
        report.AppendLine($"Missing shaders={missing}, unsupported shaders={unsupported}, HDRP shader compile errors={shaderErrors}");
        report.AppendLine("Graphics pipeline: " + AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = settings.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var entry = levels.GetArrayElementAtIndex(i);
            report.AppendLine("Quality " + entry.FindPropertyRelative("name").stringValue + ": " + AssetDatabase.GetAssetPath(entry.FindPropertyRelative("customRenderPipeline").objectReferenceValue));
        }
        foreach (var pair in counts.OrderByDescending(p => p.Value)) report.AppendLine(pair.Value + "\t" + pair.Key);
        Directory.CreateDirectory("Docs/AI");
        File.WriteAllText("Docs/AI/HdrpMaterialValidation.txt", report.ToString());
        File.WriteAllText("Temp/HdrpValidationComplete.txt", report.ToString());
        Debug.Log("HDRP material validation complete: " + missing + " missing, " + unsupported + " unsupported, " + shaderErrors + " shader errors.");
    }
}

// Validation entry point is also available from Tools/Rendering.
