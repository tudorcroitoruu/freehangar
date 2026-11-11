using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[FilePath("ProjectSettings/SwitchOptimizationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class SwitchOptimizationSettings : ScriptableSingleton<SwitchOptimizationSettings>
{
    public bool automaticTextures = false;
    public bool automaticMaterials = false;
    public int worldMaxSize = 1024;
    public int spriteMaxSize = 2048;
    public TextureImporterFormat worldFormat = TextureImporterFormat.ASTC_6x6;
    public TextureImporterFormat normalFormat = TextureImporterFormat.ASTC_4x4;
    public TextureImporterFormat uiFormat = TextureImporterFormat.ASTC_4x4;
    public bool oneFormatForAll;
    public void SaveSettings() => Save(true);
}

public sealed class SwitchTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!SwitchOptimizationSettings.instance.automaticTextures) return;
        var importer = (TextureImporter)assetImporter;
        if (SwitchOptimizationTools.TryPlan(importer, out var plan, out _) &&
            !SwitchOptimizationTools.Matches(importer.GetPlatformTextureSettings("Nintendo Switch"), plan))
        {
            SwitchOptimizationTools.BackupMeta(assetPath, "Library/SwitchOptimizationBackups/Automatic");
            importer.SetPlatformTextureSettings(plan);
        }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                SwitchOptimizationTools.QueueMaterial(path);
    }
}

// Fixed commands follow the existing project Editor automation convention.
// No work is performed during a build, Play Mode, or script compilation.
[InitializeOnLoad]
public sealed class SwitchOptimizationTools : EditorWindow
{
    private const string Platform = "Nintendo Switch";
    private const string Root = "Tools/Nintendo Switch 1/";
    private const string Request = "Temp/switch-optimization-request.txt";
    private const string Report = "Temp/switch-optimization-report.txt";
    private const string AuditReport = "Temp/switch-texture-audit.txt";
    private static Queue<string> pending;
    private static int total, changed, skipped;
    private static string backup;
    private static readonly StringBuilder results = new StringBuilder();
    private static readonly HashSet<string> materialQueue = new HashSet<string>();
    private static readonly string[] FormatNames = { "ASTC 4x4", "ASTC 5x5", "ASTC 6x6", "ASTC 8x8", "ASTC 10x10", "ASTC 12x12" };
    private static readonly int[] Formats = { (int)TextureImporterFormat.ASTC_4x4, (int)TextureImporterFormat.ASTC_5x5,
        (int)TextureImporterFormat.ASTC_6x6, (int)TextureImporterFormat.ASTC_8x8,
        (int)TextureImporterFormat.ASTC_10x10, (int)TextureImporterFormat.ASTC_12x12 };
    internal static void QueueMaterial(string path) => materialQueue.Add(path);

    static SwitchOptimizationTools() => EditorApplication.update += Tick;

    [MenuItem(Root + "Optimizer")]
    public static void Open() => GetWindow<SwitchOptimizationTools>("Switch 1 Optimizer");

    private void OnGUI()
    {
        var settings = SwitchOptimizationSettings.instance;
        EditorGUILayout.HelpBox("Choose ASTC compression for all eligible project textures on Nintendo Switch. Larger blocks use less memory but lose more detail. CPU-readable, data, HDR, lightmap and non-2D textures are reported for review. Add switch-keep to importer User Data to opt out.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Busy || pending != null))
        {
        EditorGUI.BeginChangeCheck();
        settings.automaticTextures = EditorGUILayout.Toggle("Automatic texture imports", settings.automaticTextures);
        settings.automaticMaterials = EditorGUILayout.Toggle("Automatic GPU instancing", settings.automaticMaterials);
        settings.worldMaxSize = EditorGUILayout.IntPopup("World maximum", settings.worldMaxSize, new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        settings.spriteMaxSize = EditorGUILayout.IntPopup("UI maximum", settings.spriteMaxSize, new[] { "1024", "2048", "4096" }, new[] { 1024, 2048, 4096 });
        settings.oneFormatForAll = EditorGUILayout.Toggle("One format for all", settings.oneFormatForAll);
        settings.worldFormat = FormatPopup(settings.oneFormatForAll ? "All eligible textures" : "World textures", settings.worldFormat);
        if (!settings.oneFormatForAll)
        {
            settings.normalFormat = FormatPopup("Normal maps", settings.normalFormat);
            settings.uiFormat = FormatPopup("UI / sprites", settings.uiFormat);
        }
        if (EditorGUI.EndChangeCheck()) settings.SaveSettings();
        }
        using (new EditorGUI.DisabledScope(Busy || pending != null))
        {
            if (GUILayout.Button("Audit all textures and startup dependencies")) Audit();
            if (GUILayout.Button("Apply compression to all eligible textures")) ApplyAll();
            if (GUILayout.Button("Enable GPU instancing on compatible materials")) EnableInstancing();
            if (GUILayout.Button("Apply compression + GPU instancing")) { EnableInstancing(); ApplyAll(); }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain layers + material normal strength", EditorStyles.boldLabel);
            if (GUILayout.Button("Audit normal strengths outside 0?1")) ProcessNormalStrengths(false);
            if (GUILayout.Button("Clamp normal strengths to 0?1")) ProcessNormalStrengths(true);
            if (GUILayout.Button("Set all normal strengths to exactly 1")) ProcessNormalStrengths(true, true);
            EditorGUILayout.HelpBox("Edits shared assets on every platform. Clamp preserves strengths already within 0?1; Set to 1 also changes deliberately weak/disabled normals. Undo and asset backups are available. This is a visual policy, not a universal mobile requirement.", MessageType.None);
        }
        EditorGUILayout.HelpBox("GPU instancing is not material cloning. SRP Batcher takes priority in URP; skinned renderers do not use ordinary GPU instancing. Review static batching separately to avoid duplicated mesh memory.", MessageType.None);
        if (pending != null)
        {
            EditorGUILayout.LabelField($"{total - pending.Count}/{total} processed; {changed} changed");
            if (GUILayout.Button("Stop after current texture")) Finish("Cancelled; processed overrides retained. Run Apply again to resume.");
            Repaint();
        }
        if (GUILayout.Button("Show compression report") && File.Exists(Report)) EditorUtility.RevealInFinder(Report);
        if (GUILayout.Button("Show texture audit") && File.Exists(AuditReport)) EditorUtility.RevealInFinder(AuditReport);
    }

    private static TextureImporterFormat FormatPopup(string label, TextureImporterFormat value) =>
        (TextureImporterFormat)EditorGUILayout.IntPopup(label, (int)value, FormatNames, Formats);

    private static bool Busy => EditorApplication.isCompiling || EditorApplication.isUpdating ||
        EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;

    private static string[] TexturePaths() => AssetDatabase.FindAssets("t:Texture", new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();

    internal static bool TryPlan(TextureImporter importer, out TextureImporterPlatformSettings plan, out string reason)
    {
        plan = null;
        reason = "";
        if (importer == null) { reason = "Not a TextureImporter (generated/embedded asset)"; return false; }
        string path = importer.assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("/Editor/"))
        { reason = "Editor/package asset"; return false; }
        string lower = path.ToLowerInvariant();
        if ((importer.userData ?? "").IndexOf("switch-keep", StringComparison.OrdinalIgnoreCase) >= 0)
        { reason = "Explicit switch-keep opt-out"; return false; }
        if (importer.isReadable) { reason = "CPU-readable; review pixel-reading code before compression"; return false; }
        if (importer.textureShape != TextureImporterShape.Texture2D)
        { reason = "Cube/array/volume: review separately"; return false; }
        if (importer.textureType != TextureImporterType.Default && importer.textureType != TextureImporterType.NormalMap &&
            importer.textureType != TextureImporterType.Sprite)
        { reason = "Specialized importer (lightmap, single-channel, cursor, etc.)"; return false; }
        if (lower.EndsWith(".exr") || lower.EndsWith(".hdr") || lower.Contains("lightmap") ||
            lower.Contains("/lut") || lower.Contains("lookup") || lower.Contains("/colorgrading") ||
            lower.Contains("/noise") || lower.Contains("/bluenoise") || lower.Contains("/bayer") ||
            lower.Contains("/vat") || lower.Contains("sdf"))
        { reason = "HDR/data/lighting texture: preserve precision"; return false; }

        var settings = SwitchOptimizationSettings.instance;
        bool ui = importer.textureType == TextureImporterType.Sprite || lower.Contains("/_ui/") || lower.Contains("/ui/");
        TextureImporterFormat format = settings.oneFormatForAll ? settings.worldFormat :
            ui ? settings.uiFormat : importer.textureType == TextureImporterType.NormalMap ? settings.normalFormat : settings.worldFormat;
        if (!Formats.Contains((int)format))
        { reason = "Select a supported ASTC block size in the optimizer"; return false; }
        if (!TextureImporter.IsPlatformTextureFormatValid(importer.textureType, BuildTarget.Switch, format))
        { reason = "ASTC unavailable for this importer/installed Switch module"; return false; }
        var existing = importer.GetPlatformTextureSettings(Platform);
        int authoredMax = existing.overridden ? existing.maxTextureSize : importer.maxTextureSize;
        int max = Mathf.Min(authoredMax, ui ? settings.spriteMaxSize : settings.worldMaxSize);
        plan = existing;
        plan.name = Platform;
        plan.overridden = true;
        plan.maxTextureSize = max;
        plan.format = format;
        plan.textureCompression = TextureImporterCompression.CompressedHQ;
        plan.compressionQuality = 100;
        plan.crunchedCompression = false;
        return true;
    }

    internal static bool Matches(TextureImporterPlatformSettings a, TextureImporterPlatformSettings b) =>
        a.overridden == b.overridden && a.maxTextureSize == b.maxTextureSize && a.format == b.format &&
        a.textureCompression == b.textureCompression && a.compressionQuality == b.compressionQuality &&
        a.crunchedCompression == b.crunchedCompression;

    [MenuItem(Root + "Audit")]
    public static void Audit()
    {
        if (Busy || pending != null) throw new InvalidOperationException("Wait for Unity to finish its current operation.");
        var report = new StringBuilder("Switch 1 texture audit (import settings, not measured device RAM)\n");
        var startup = new HashSet<string>(AssetDatabase.GetDependencies(new[] { "Assets/Scenes/Bootstrap.unity", "Assets/Scenes/Menu.unity" }, true));
        int count = 0, eligible = 0, startupTextures = 0;
        foreach (string path in TexturePaths())
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            count++;
            bool inStartup = startup.Contains(path);
            if (inStartup) startupTextures++;
            var current = importer.GetPlatformTextureSettings(Platform);
            bool canChange = TryPlan(importer, out var plan, out string reason);
            if (canChange) eligible++;
            report.AppendLine($"{(inStartup ? "STARTUP" : "ASSET")} | {path} | current override={current.overridden}, {current.format}, max={current.maxTextureSize}, readable={importer.isReadable} | " +
                (canChange ? $"planned {plan.format}, max={plan.maxTextureSize}" : "REVIEW: " + reason));
        }
        report.Insert(0, $"Textures: {count}; eligible: {eligible}; startup texture dependencies: {startupTextures}\n");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(AuditReport, report.ToString());
        Debug.Log($"[Switch Optimizer] Audit complete: {count} textures, {startupTextures} startup dependencies. {AuditReport}");
    }

    [MenuItem(Root + "Apply Texture Compression")]
    public static void ApplyAll()
    {
        if (Busy || pending != null) throw new InvalidOperationException("Wait for Unity to finish its current operation.");
        pending = new Queue<string>(TexturePaths());
        total = pending.Count;
        changed = skipped = 0;
        backup = "Library/SwitchOptimizationBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        results.Clear();
        results.AppendLine("Switch-only texture overrides. Original .meta files backed up under " + backup);
        Debug.Log($"[Switch Optimizer] Processing {total} texture assets. Progress: {Report}");
    }

    private static void Tick()
    {
        if (Busy) return;
        try
        {
            if (pending == null)
            {
                if (materialQueue.Count > 0)
                {
                    string materialPath = materialQueue.First();
                    materialQueue.Remove(materialPath);
                    if (SwitchOptimizationSettings.instance.automaticMaterials) EnableMaterial(materialPath);
                }
                if (!File.Exists(Request)) return;
                string command = File.ReadAllText(Request).Trim();
                File.Delete(Request);
                if (command == "audit") Audit();
                else if (command == "apply") ApplyAll();
                else if (command == "materials") EnableInstancing();
                else if (command == "validate") Validate();
                else if (command == "setup") { Validate(); Audit(); EnableInstancing(); ApplyAll(); }
                else throw new ArgumentException("Unknown Switch optimizer command: " + command);
                return;
            }
            if (pending.Count == 0) { Finish("Complete"); return; }
            string path = pending.Dequeue();
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!TryPlan(importer, out var plan, out string reason))
            {
                skipped++;
                results.AppendLine("REVIEW | " + path + " | " + reason);
            }
            else if (!Matches(importer.GetPlatformTextureSettings(Platform), plan))
            {
                BackupMeta(path, backup);
                importer.SetPlatformTextureSettings(plan);
                importer.SaveAndReimport();
                if (!Matches(importer.GetPlatformTextureSettings(Platform), plan))
                    throw new InvalidOperationException("Texture override failed: " + path);
                changed++;
                results.AppendLine($"CHANGED | {path} | {plan.format} max={plan.maxTextureSize}");
            }
            if ((total - pending.Count) % 50 == 0) WriteProgress("Running");
        }
        catch (Exception error)
        {
            if (pending != null) Finish("Stopped on error: " + error);
            File.WriteAllText("Temp/switch-optimization-error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    internal static void BackupMeta(string path, string directory)
    {
        if (!File.Exists(path + ".meta")) return; // A newly created asset has no previous settings.
        string destination = directory + "/" + path + ".meta";
        if (File.Exists(destination)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(path + ".meta", destination, false);
    }

    private static void WriteProgress(string status)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(Report, $"{status}; processed {total - (pending?.Count ?? 0)}/{total}, changed {changed}, review {skipped}\n" + results);
    }

    private static void Finish(string status)
    {
        WriteProgress(status);
        pending = null;
        Debug.Log($"[Switch Optimizer] {status}. Changed {changed}, review {skipped}. {Report}");
    }

    [MenuItem(Root + "Enable Compatible GPU Instancing")]
    public static void EnableInstancing()
    {
        if (Busy || pending != null) throw new InvalidOperationException("Wait for Unity to finish its current operation.");
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (EnableMaterial(path)) count++;
        }
        File.WriteAllText("Temp/switch-material-report.txt", $"Enabled GPU instancing on {count} compatible material assets. SRP Batcher remains enabled. No renderer.material cloning.");
        Debug.Log($"[Switch Optimizer] Enabled GPU instancing on {count} compatible materials.");
    }

    private static bool EnableMaterial(string path)
    {
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || path.Contains("/Editor/")) return false;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || material.shader == null || material.enableInstancing ||
            !material.shader.keywordSpace.FindKeyword("INSTANCING_ON").isValid) return false;
        var importer = AssetImporter.GetAtPath(path);
        if (importer != null && (importer.userData ?? "").Contains("switch-keep")) return false;
        string materialBackup = "Library/SwitchOptimizationBackups/Materials/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(materialBackup));
        if (!File.Exists(materialBackup)) File.Copy(path, materialBackup);
        BackupMeta(path, "Library/SwitchOptimizationBackups/Materials");
        Undo.RecordObject(material, "Enable GPU instancing");
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return true;
    }

    // Explicit property names avoid accidentally changing normal textures, spaces or keywords.
    private static readonly string[] NormalProperties =
        { "_BumpScale", "_DetailNormalMapScale", "_NormalScale", "_NormalStrength" };

    [MenuItem(Root + "Clamp Terrain and Material Normals")]
    public static void ClampNormals() => ProcessNormalStrengths(true);

    private static void ProcessNormalStrengths(bool apply, bool setToOne = false)
    {
        if (Busy || pending != null) throw new InvalidOperationException("Wait for Unity to finish its current operation.");
        string folder = "Library/SwitchOptimizationBackups/Normals-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var report = new StringBuilder(apply ? "Applied normal strength changes\n" : "Normal strength audit (no changes)\n");
        report.AppendLine(setToOne ? "Policy: exactly 1" : "Policy: clamp to [0, 1]; non-finite values become 1");
        int changes = 0;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Normalize terrain and material normal strength");
        try
        {
            var paths = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets" }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct();
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (path.Contains("/Editor/") || (importer?.userData ?? "").IndexOf("switch-keep", StringComparison.OrdinalIgnoreCase) >= 0)
                { report.AppendLine("SKIP | " + path); continue; }
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!(asset is TerrainLayer) && !(asset is Material && path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
                { report.AppendLine("REVIEW embedded/unsupported asset | " + path); continue; }
                bool recorded = false;
                void Change(string property, float current, Action<float> assign)
                {
                    float next = setToOne || float.IsNaN(current) || float.IsInfinity(current) ? 1 : Mathf.Clamp01(current);
                    if (current == next) return;
                    report.AppendLine($"{path} | {property}: {current} -> {next}");
                    changes++;
                    if (!apply) return;
                    if (!recorded)
                    {
                        string destination = Path.Combine(folder, path);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(path, destination, false);
                        Undo.RecordObject(asset, "Normalize normal strength");
                        recorded = true;
                    }
                    assign(next);
                    EditorUtility.SetDirty(asset);
                }
                if (asset is TerrainLayer layer)
                    Change("normalScale", layer.normalScale, value => layer.normalScale = value);
                else if (asset is Material material && material.shader)
                    foreach (string property in NormalProperties)
                    {
                        int index = material.shader.FindPropertyIndex(property);
                        if (index < 0) continue;
                        var type = material.shader.GetPropertyType(index);
                        if (type != UnityEngine.Rendering.ShaderPropertyType.Float && type != UnityEngine.Rendering.ShaderPropertyType.Range) continue;
                        Change(property, material.GetFloat(property), value => material.SetFloat(property, value));
                    }
                if (recorded) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
            Directory.CreateDirectory("Temp");
            report.AppendLine($"Properties identified: {changes}. Backup folder (if applied): {folder}");
            File.WriteAllText("Temp/switch-normal-strength-report.txt", report.ToString());
        }
        Debug.Log($"[Switch Optimizer] Normal strengths: {changes} properties {(apply ? "changed" : "would change")}. Report: Temp/switch-normal-strength-report.txt");
    }

    public static void Validate()
    {
        if (!TextureImporter.IsPlatformTextureFormatValid(TextureImporterType.Default, BuildTarget.Switch, TextureImporterFormat.ASTC_6x6))
            throw new InvalidOperationException("ASTC 6x6 is unavailable for this Switch installation.");
        Debug.Log("Switch optimizer format validation passed.");
    }
}
