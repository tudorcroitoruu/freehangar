using System.IO;
using UnityEditor;
using UnityEngine;

public static class MirrorShaderValidation
{
    [MenuItem("Tools/Rendering/Validate simple mirror shader")]
    public static void Validate()
    {
        const string path = "Assets/Shaders/HDRPRealtimeScreenSpaceReflection.shader";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) throw new System.InvalidOperationException("Mirror shader is missing.");
        var material = new Material(shader);
        try
        {
            ShaderUtil.CompilePass(material, 0, true);
            var messages = ShaderUtil.GetShaderMessages(shader);
            var report = new System.Text.StringBuilder();
            foreach (var message in messages) report.AppendLine($"{message.severity}: {message.message} ({message.file}:{message.line})");
            if (messages.Length == 0) report.AppendLine("No shader compiler messages for the current editor graphics API.");
            Directory.CreateDirectory("output/Reflection");
            File.WriteAllText("output/Reflection/shader-validation.txt", report.ToString());
            Debug.Log(report.ToString(), shader);
        }
        finally { Object.DestroyImmediate(material); }
    }
}
