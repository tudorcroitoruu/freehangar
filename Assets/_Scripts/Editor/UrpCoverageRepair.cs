using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class UrpCoverageRepair
{
    static readonly StringBuilder Report = new StringBuilder();
    static string backup;
    static bool Compatible(Material m) => m && m.shader &&
        (m.GetTag("RenderPipeline", false) == "UniversalPipeline" || m.shader.name.StartsWith("Universal Render Pipeline/"));
    static bool Special(Material m)
    {
        string n = m && m.shader ? m.shader.name.ToLowerInvariant() : "";
        return n.StartsWith("ui/") || n.StartsWith("gui/") || n.Contains("skybox") ||
            n.StartsWith("custom/outline") || (n.Contains("textmeshpro") && !n.Contains("hdrp"));
    }
    static HashSet<string> TerrainMaterials()
    {
        var result = new HashSet<string>();
        // Inspect references without loading vendor demo scenes or running their editor callbacks.
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && (p.EndsWith(".unity") || p.EndsWith(".prefab"))))
        foreach (string line in File.ReadLines(path))
        {
            if (!line.Contains("m_MaterialTemplate:")) continue;
            var match = Regex.Match(line, @"guid: ([a-f0-9]{32})");
            if (match.Success) result.Add(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
        }
        return result;
    }
    static IEnumerable<string> Paths() => AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
        .Concat(AssetDatabase.FindAssets("t:Model", new[] { "Assets" })).Select(AssetDatabase.GUIDToAssetPath)
        .Where(p => p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || AssetImporter.GetAtPath(p) is ModelImporter)
        .Distinct().OrderBy(p => p);
    [MenuItem("Tools/Rendering/Audit Remaining URP Materials")]
    public static void Audit()
    {
        Report.Clear(); int remaining = 0;
        foreach (string path in Paths())
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && importer.GetExternalObjectMap().TryGetValue(new AssetImporter.SourceAssetIdentifier(m), out var mapped) && mapped is Material replacement && Compatible(replacement)) continue;
            if (Compatible(m) || Special(m)) continue;
            remaining++;
            Report.AppendLine($"REMAINING\t{path}\t{m.name}\t{(m.shader ? m.shader.name : "MISSING")}");
        }
        foreach (string path in TerrainMaterials())
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m || !m.shader || m.shader.name != "Universal Render Pipeline/Terrain/Lit") remaining++;
            Report.AppendLine($"TERRAIN\t{path}\t{(m && m.shader ? m.shader.name : "MISSING")}");
        }
        foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene.IsValid() && r.gameObject.scene.isLoaded))
        foreach (var m in renderer.sharedMaterials)
        {
            if (Compatible(m) || Special(m)) continue;
            Report.AppendLine($"SCENE RENDERER\t{renderer.gameObject.scene.path}\t{renderer.name}\t{(m && m.shader ? m.shader.name : "MISSING")}");
        }
        Report.AppendLine($"Remaining incompatible materials={remaining}");
        Report.AppendLine("Default pipeline=" + AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
        Report.AppendLine("Quality pipeline=" + AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
        Directory.CreateDirectory("Docs/AI"); File.WriteAllText("Docs/AI/UrpCoverageAudit.txt", Report.ToString());
        Debug.Log($"URP coverage audit: {remaining} remaining. See Docs/AI/UrpCoverageAudit.txt");
    }
    static void Backup(string path)
    {
        string copy = backup + "/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(copy));
        if (!File.Exists(copy)) File.Copy(path, copy);
        if (File.Exists(path + ".meta") && !File.Exists(copy + ".meta")) File.Copy(path + ".meta", copy + ".meta");
    }
    [MenuItem("Tools/Rendering/Repair Remaining URP Materials and Terrain")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var pipeline = QualitySettings.renderPipeline;
        if (!pipeline || !pipeline.GetType().Name.Contains("Universal")) throw new InvalidOperationException("Select a URP quality pipeline first.");
        backup = "MaterialConversionBackups/URP-Coverage-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup); Backup("ProjectSettings/GraphicsSettings.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        Report.Clear(); int changed = 0, failed = 0;
        var terrains = TerrainMaterials();
        foreach (string path in Paths().ToArray())
        {
            try
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                bool remapped = false;
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray())
                {
                    if (importer != null && importer.GetExternalObjectMap().TryGetValue(new AssetImporter.SourceAssetIdentifier(m), out var mapped) && mapped is Material replacement && Compatible(replacement)) continue;
                    bool terrain = terrains.Contains(path) || (m.shader && m.shader.name.Contains("Terrain"));
                    if (Compatible(m) && (!terrain || m.shader.name == "Universal Render Pipeline/Terrain/Lit")) continue;
                    if (!terrain && Special(m)) continue;
                    if (!m.shader || m.shader.name == "Hidden/InternalErrorShader" || m.isVariant) { failed++; Report.AppendLine("REQUIRES REVIEW\t" + path); continue; }
                    Material dst;
                    if (m.shader.name.StartsWith("TextMeshPro/") && m.shader.name.Contains("HDRP"))
                    {
                        string shader = m.shader.name.EndsWith("UNLIT") ? "TextMeshPro/SRP/TMP_SDF-URP Unlit" : "TextMeshPro/SRP/TMP_SDF-URP Lit";
                        var target = Shader.Find(shader);
                        if (!target) throw new InvalidOperationException("Missing TMP shader: " + shader);
                        dst = new Material(m) { shader = target };
                        dst.shaderKeywords = Array.Empty<string>();
                    }
                    else if (terrain)
                    {
                        dst = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { enableInstancing = true };
                        foreach (string property in new[] { "_EnableHeightBlend", "_HeightTransition", "_EnableInstancedPerPixelNormal" })
                            if (m.HasProperty(property)) dst.SetFloat(property, m.GetFloat(property));
                        if (dst.GetFloat("_EnableHeightBlend") > 0) dst.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                        if (dst.GetFloat("_EnableInstancedPerPixelNormal") > 0) dst.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
                    }
                    else dst = MaterialPipelineConverter.ConvertMaterial(m, MaterialPipelineConverter.Target.URP,
                        "Assets/MaterialConversionGenerated/URPCoverage", AssetDatabase.AssetPathToGUID(path) + "_" + m.GetInstanceID());
                    try
                    {
                        string old = m.shader.name;
                        if (importer != null)
                        {
                            Backup(path + ".meta");
                            string folder = "Assets/MaterialConversionGenerated/URPCoverage/Embedded";
                            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long localId);
                            string output = folder + "/" + guid + "_" + localId + ".mat";
                            var external = AssetDatabase.LoadAssetAtPath<Material>(output);
                            if (!external) { external = new Material(dst) { name = m.name }; AssetDatabase.CreateAsset(external, output); }
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), external); remapped = true;
                        }
                        else
                        {
                            Backup(path); Undo.RecordObject(m, "Convert remaining URP material");
                            m.shader = dst.shader; m.CopyPropertiesFromMaterial(dst);
                            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
                        }
                        changed++; Report.AppendLine($"CONVERTED\t{path}\t{m.name}\t{old}\t{dst.shader.name}");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(dst); }
                }
                if (remapped) importer.SaveAndReimport();
            }
            catch (Exception e) { failed++; Report.AppendLine("FAILED\t" + path + "\t" + e.Message); Debug.LogException(e); }
        }
        AssetDatabase.SaveAssets(); Report.AppendLine($"Converted={changed}; failed={failed}");
        File.WriteAllText(backup + "/report.tsv", Report.ToString());
        Debug.Log($"URP coverage repair: {changed} converted; {failed} failed. Backup: {backup}");
    }
}
