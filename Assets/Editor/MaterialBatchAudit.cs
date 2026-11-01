using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

/// <summary>Audit material instancing and compare equivalent Gameplay views.</summary>
public sealed class MaterialBatchAudit : EditorWindow
{
    private const string BaselineKey = "FreeHangar.MaterialBatchAudit.Baseline";
    private const string AfterKey = "FreeHangar.MaterialBatchAudit.After";
    private readonly List<Material> materials = new List<Material>();
    private string filter = "";
    private Vector2 scroll;
    private int readOnlyCount, embeddedCount, missingShaderCount;
    private ProfilerRecorder batches, draws, setPass;
    private int lastFrame, sampleCount;
    private double batchSum, drawSum, setPassSum;
    private bool capturing, captureAfter;
    private const int CaptureFrames = 120;

    [Serializable] private sealed class Capture
    {
        public double batches, draws, setPass;
        public int frames;
        public string scene, quality, timestamp;
    }

    [MenuItem("Tools/Rendering/Material Instancing and Batch Audit")]
    private static void Open() => GetWindow<MaterialBatchAudit>("Batch Audit");

    private void OnEnable() { EditorApplication.update += Tick; Scan(); }
    private void OnDisable() { EditorApplication.update -= Tick; StopCapture(); }

    private void Scan()
    {
        materials.Clear(); readOnlyCount = embeddedCount = missingShaderCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) { embeddedCount++; continue; }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (!AssetDatabase.IsOpenForEdit(mat)) { readOnlyCount++; continue; }
            if (mat.shader == null) missingShaderCount++;
            materials.Add(mat);
        }
        materials.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Material instancing audit", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("SRP Batcher takes priority on compatible renderers. Enabling a material flag does not prove that its shader or renderer supports instancing; inspect actual draws in Frame Debugger. Embedded and read-only materials are excluded.", MessageType.Info);
        EditorGUILayout.LabelField("Active pipeline", GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in / not initialized");
        EditorGUILayout.LabelField("SRP Batcher (active runtime setting)", GraphicsSettings.useScriptableRenderPipelineBatching ? "On" : "Off / not initialized");
        int enabled = 0;
        foreach (var mat in materials) if (mat != null && mat.enableInstancing) enabled++;
        EditorGUILayout.LabelField("Editable .mat assets", $"{materials.Count}; instancing on: {enabled}; off: {materials.Count - enabled}");
        EditorGUILayout.LabelField("Excluded", $"{embeddedCount} embedded paths, {readOnlyCount} read-only; {missingShaderCount} missing shaders");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rescan")) Scan();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Enable instancing on all editable .mat assets")) EnableAll();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gameplay comparison", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("In Play Mode, use the same camera, resolution, quality, scene and gameplay state for each 120-frame capture. Keep the Game view visible. Values are editor rendering counters, not a player-build benchmark.", MessageType.None);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || capturing))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture before")) StartCapture(false);
            if (GUILayout.Button("Capture after")) StartCapture(true);
            EditorGUILayout.EndHorizontal();
        }
        if (capturing) EditorGUILayout.LabelField($"Capturing {(captureAfter ? "after" : "before")}: {sampleCount}/{CaptureFrames} frames");
        ShowCapture("Before", BaselineKey); ShowCapture("After", AfterKey);
        Capture before = Load(BaselineKey), after = Load(AfterKey);
        if (before != null && after != null)
        {
            EditorGUILayout.LabelField("Change (after − before)", $"Batches {after.batches - before.batches:+0.0;-0.0;0.0}, draws {after.draws - before.draws:+0.0;-0.0;0.0}, SetPass {after.setPass - before.setPass:+0.0;-0.0;0.0}");
            if (before.scene != after.scene || before.quality != after.quality)
                EditorGUILayout.HelpBox("Scene or quality differs between captures; results are not directly comparable.", MessageType.Warning);
        }
        EditorGUILayout.Space();
        filter = EditorGUILayout.TextField("Search name, shader or path", filter);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var mat in materials)
        {
            if (mat == null) continue;
            string path = AssetDatabase.GetAssetPath(mat), shader = mat.shader != null ? mat.shader.name : "Missing shader";
            if (!string.IsNullOrEmpty(filter) && (mat.name + " " + shader + " " + path).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(mat.enableInstancing ? "On" : "Off", GUILayout.Width(42))) EditorGUIUtility.PingObject(mat);
            if (GUILayout.Button(mat.name, EditorStyles.label, GUILayout.MinWidth(100))) Selection.activeObject = mat;
            EditorGUILayout.LabelField(shader, GUILayout.MinWidth(100));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void EnableAll()
    {
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Enable material GPU instancing");
        int changed = 0;
        foreach (var mat in materials)
        {
            if (mat == null || mat.enableInstancing || !AssetDatabase.IsOpenForEdit(mat)) continue;
            Undo.RecordObject(mat, "Enable material GPU instancing");
            mat.enableInstancing = true; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); changed++;
        }
        Undo.CollapseUndoOperations(group);
        Debug.Log($"Batch Audit: enabled instancing on {changed} editable materials. Shader and renderer compatibility must be checked separately.");
    }

    private void StartCapture(bool after)
    {
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Total Batches Count");
        draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        if (!batches.Valid || !draws.Valid || !setPass.Valid)
        {
            StopCapture();
            Debug.LogWarning("Batch Audit: one or more render counters are unavailable in this Editor.");
            return;
        }
        captureAfter = after; capturing = true; lastFrame = Time.frameCount; sampleCount = 0;
        batchSum = drawSum = setPassSum = 0;
    }

    private void Tick()
    {
        if (!capturing) return;
        if (!EditorApplication.isPlaying) { StopCapture(); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        batchSum += batches.LastValue; drawSum += draws.LastValue; setPassSum += setPass.LastValue;
        if (++sampleCount >= CaptureFrames)
        {
            var capture = new Capture { batches = batchSum / sampleCount, draws = drawSum / sampleCount,
                setPass = setPassSum / sampleCount, frames = sampleCount,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], timestamp = DateTime.Now.ToString("g") };
            EditorPrefs.SetString(captureAfter ? AfterKey : BaselineKey, JsonUtility.ToJson(capture));
            Debug.Log($"Batch Audit {(captureAfter ? "after" : "before")}: {capture.batches:F1} batches, {capture.draws:F1} draw calls, {capture.setPass:F1} SetPass calls ({sampleCount} frames), {capture.scene}, {capture.quality}.");
            StopCapture();
        }
        Repaint();
    }

    private void StopCapture() { capturing = false; batches.Dispose(); draws.Dispose(); setPass.Dispose(); batches = default; draws = default; setPass = default; }
    private static Capture Load(string key) { string json = EditorPrefs.GetString(key, ""); return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Capture>(json); }
    private static void ShowCapture(string title, string key)
    {
        Capture c = Load(key);
        EditorGUILayout.LabelField(title, c == null ? "Not captured" : $"{c.batches:F1} batches, {c.draws:F1} draws, {c.setPass:F1} SetPass ({c.frames} frames)");
        if (c != null) EditorGUILayout.LabelField("", $"{c.scene} | {c.quality} | {c.timestamp}");
    }
}
