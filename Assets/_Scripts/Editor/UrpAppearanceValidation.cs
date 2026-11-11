using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class UrpAppearanceValidation
{
    public static void FixLeafCutoff()
    {
        const string path = "Assets/NewMap/FiveHangars/TreeCutoutMaterials/TFP_Atlas_Vegetation_1A - NewMap quality - Tree cutout.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material.GetFloat("_AlphaCutoff") > 0) return;
        Directory.CreateDirectory("MaterialConversionBackups/URP-LeafCutoff");
        File.Copy(path, "MaterialConversionBackups/URP-LeafCutoff/Vegetation.mat", true);
        Undo.RecordObject(material, "Restore leaf alpha cutoff");
        material.SetFloat("_AlphaCutoff", .5f);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
    }
    public static void Validate()
    {
        var report = new StringBuilder();
        int errors = 0;
        foreach (string name in new[] { "Toon/CustomToon", "Toon/CustomToonGrass", "Toon/CustomToonVegetation", "Toon/CustomWaterSimple", "Freehangar/URP/Tree Cutout Toon", "Freehangar/URP/Grain Kernels" })
        {
            Shader shader = Shader.Find(name);
            if (!shader) { errors++; report.AppendLine("MISSING " + name); continue; }
            var material = new Material(shader);
            try { for (int p = 0; p < material.passCount; p++) ShaderUtil.CompilePass(material, p, true); }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                report.AppendLine(name + " " + message.severity + ": " + message.message + " " + message.file + ":" + message.line);
                if (message.severity.ToString() == "Error") errors++;
            }
            report.AppendLine(name + ": supported=" + shader.isSupported);
        }
        foreach (var pile in Resources.FindObjectsOfTypeAll<GrainPile>().Where(p => p.gameObject.scene.IsValid()))
        {
            var m = pile.GetComponent<Renderer>().sharedMaterial;
            report.AppendLine("PILE " + pile.name + " " + (m ? m.shader.name : "MISSING"));
        }
        report.AppendLine("Shader errors=" + errors);
        Directory.CreateDirectory("output/Rendering/URPAppearance");
        File.WriteAllText("output/Rendering/URPAppearance/validation.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    static void Capture(Camera camera, string path)
    {
        var old = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1200, 850, 24);
        Texture2D image = null;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image = new Texture2D(1200, 850, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0,0,1200,850),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); if (image) UnityEngine.Object.DestroyImmediate(image); }
    }
    public static void CaptureScene()
    {
        Directory.CreateDirectory("output/Rendering/URPAppearance");
        var camera = SceneView.lastActiveSceneView.camera;
        Capture(camera, "output/Rendering/URPAppearance/Scene.png");
        var oldPos = camera.transform.position; var oldRot = camera.transform.rotation;
        try
        {
            var tree = Resources.FindObjectsOfTypeAll<LODGroup>().FirstOrDefault(t => t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && t.name.StartsWith("TFP_Beech_Tree_02A"));
            if (tree)
            {
                var p = tree.transform.position;
                camera.transform.SetPositionAndRotation(p + new Vector3(12,5,16), Quaternion.LookRotation(p + Vector3.up*5 - (p + new Vector3(12,5,16))));
                Capture(camera,"output/Rendering/URPAppearance/Tree.png");
                try
                {
                    for (int lod = 0; lod < tree.lodCount; lod++)
                    {
                        tree.ForceLOD(lod);
                        Capture(camera, "output/Rendering/URPAppearance/TreeLOD" + lod + ".png");
                    }
                }
                finally { tree.ForceLOD(-1); }
            }
            var pile = Resources.FindObjectsOfTypeAll<GrainPile>().FirstOrDefault(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy);
            if (pile)
            {
                var bounds = pile.GetComponent<Renderer>().bounds;
                var pos = bounds.center + new Vector3(0, 3, -5);
                camera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(bounds.center-pos));
                Capture(camera,"output/Rendering/URPAppearance/Grain.png");
            }
            camera.transform.SetPositionAndRotation(new Vector3(50,95,-65), Quaternion.LookRotation(new Vector3(15,0,110)-new Vector3(50,95,-65)));
            Capture(camera,"output/Rendering/URPAppearance/Overview.png");
        }
        finally { camera.transform.SetPositionAndRotation(oldPos, oldRot); }
        Debug.Log("Appearance captures saved to output/Rendering/URPAppearance");
    }
}
