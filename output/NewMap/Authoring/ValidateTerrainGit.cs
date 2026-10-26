using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Runs through `unity command script run`; lives outside Assets to avoid a reload.
public static class ValidateTerrainGit
{
    public static readonly string[] Paths = {
        "Assets/NewMap/Terrain/NewMapTerrain.asset",
        "Assets/NewMap/Terrain/Before road surface undulations.asset",
        "Assets/NewMap/Terrain/Before variable road width.asset",
        "Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Oasis Dig 2.asset",
        "Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Oasis Dig 3.asset",
        "Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Terrain_Oasis.asset",
        "Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Gardens.asset",
        "Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Hay_Fields.asset",
        "Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Ranch.asset"
    };

    public static string Local()
    {
        var report = new StringBuilder();
        foreach (var path in Paths)
        {
            CheckHeader(File.ReadAllBytes(path), path);
            report.AppendLine(Describe(AssetDatabase.LoadAssetAtPath<TerrainData>(path), path));
        }
        var terrain = Terrain.activeTerrains.FirstOrDefault(t =>
            AssetDatabase.GetAssetPath(t.terrainData) == Paths[0]);
        if (!terrain || !terrain.GetComponent<TerrainCollider>() ||
            terrain.GetComponent<TerrainCollider>().terrainData != terrain.terrainData)
            throw new Exception("Active terrain/collider reference missing or mismatched.");
        report.AppendLine("Active scene terrain and collider reference the same NewMapTerrain.");
        return report.ToString();
    }

    public static string Index() { return CheckGit(":"); }
    public static string Head() { return CheckGit("HEAD:"); }

    static string CheckGit(string revision)
    {
        if (EditorApplication.isPlaying) throw new Exception("Run in Edit Mode.");
        // Check all headers before giving any suspect bytes to Unity's importer.
        var blobs = Paths.Select(p => GitBlob(revision + p)).ToArray();
        for (int i = 0; i < Paths.Length; ++i) CheckHeader(blobs[i], Paths[i]);
        var tracked = new HashSet<string>(Encoding.UTF8.GetString(GitBytes(revision == ":"
            ? "ls-files -z" : "ls-tree -rz --name-only HEAD")).Split('\0'));
        var checkedDependencies = new HashSet<string>();
        var folder = "Assets/__TerrainGitValidation_" + Guid.NewGuid().ToString("N");
        var report = new StringBuilder();
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        try
        {
            for (int i = 0; i < Paths.Length; ++i)
            {
                if (!blobs[i].SequenceEqual(File.ReadAllBytes(Paths[i])))
                    throw new Exception("Git bytes differ from working source: " + Paths[i]);
                var temporary = folder + "/" + Path.GetFileName(Paths[i]);
                File.WriteAllBytes(temporary, blobs[i]);
                AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(temporary);
                report.AppendLine(Describe(data, Paths[i]));
                // Resolve the fresh import's external terrain-layer/texture/prototype references.
                foreach (var dependency in AssetDatabase.GetDependencies(temporary, true))
                {
                    if (!dependency.StartsWith("Assets/") || dependency == temporary ||
                        !checkedDependencies.Add(dependency)) continue;
                    if (!AssetDatabase.LoadMainAssetAtPath(dependency))
                        throw new Exception("Unloaded dependency: " + dependency);
                    if (!tracked.Contains(dependency) || !tracked.Contains(dependency + ".meta"))
                        throw new Exception("Untracked dependency or meta: " + dependency);
                }
            }
        }
        finally { AssetDatabase.DeleteAsset(folder); }
        report.AppendLine("PASS: nine Git blobs match local bytes and import under fresh GUIDs; dependencies and metas exist in Git.");
        Directory.CreateDirectory("output/NewMap/TerrainRecovery");
        File.WriteAllText("output/NewMap/TerrainRecovery/validation.txt", report.ToString());
        return report.ToString();
    }

    static string Describe(TerrainData data, string path)
    {
        if (!data) throw new Exception("TerrainData failed to load: " + path);
        int missingLayers = data.terrainLayers.Count(layer => !layer || !layer.diffuseTexture);
        if (data.heightmapResolution < 2 || (path.StartsWith("Assets/NewMap/") &&
            (data.alphamapLayers == 0 || missingLayers != 0)))
            throw new Exception("Incomplete terrain data/layers: " + path);
        var heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        float min = float.MaxValue, max = float.MinValue;
        foreach (float height in heights)
        {
            if (float.IsNaN(height) || float.IsInfinity(height)) throw new Exception("Invalid height: " + path);
            min = Mathf.Min(min, height); max = Mathf.Max(max, height);
        }
        data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        return path + ": " + data.size + ", heights=" + data.heightmapResolution +
            ", range=" + min + ".." + max + ", layers=" + data.alphamapLayers +
            ", missing layer/diffuse refs=" + missingLayers;
    }

    static void CheckHeader(byte[] bytes, string path)
    {
        if (bytes.Length < 48) throw new Exception("Truncated header: " + path);
        uint version = (uint)ReadBigEndian(bytes, 8, 4);
        ulong declared = version >= 22 ? ReadBigEndian(bytes, 24, 8) : ReadBigEndian(bytes, 4, 4);
        if (version < 9 || version > 30 || declared != (ulong)bytes.LongLength)
            throw new Exception(path + ": header declares " + declared + " bytes, actual " + bytes.Length);
    }

    static ulong ReadBigEndian(byte[] bytes, int start, int count)
    {
        ulong result = 0;
        for (int i = start; i < start + count; ++i) result = (result << 8) | bytes[i];
        return result;
    }

    static byte[] GitBlob(string spec)
    {
        return GitBytes("cat-file blob \"" + spec + "\"");
    }

    static byte[] GitBytes(string arguments)
    {
        var info = new ProcessStartInfo("git", arguments) {
            WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        using (var process = Process.Start(info))
        using (var output = new MemoryStream())
        {
            var errors = process.StandardError.ReadToEndAsync();
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception(errors.GetAwaiter().GetResult());
            return output.ToArray();
        }
    }
}
