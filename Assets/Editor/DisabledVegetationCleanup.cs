using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DisabledVegetationCleanup
{
    public static void ValidateSavedScenes()
    {
        var report = new List<string>();
        foreach(string path in new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/Gameplay.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                int remaining = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                    .Count(t=>!t.gameObject.activeInHierarchy && Decoration(t.name));
                report.Add(path + ": " + remaining + " inactive matching decoration roots remain after reload");
                if(remaining != 0) throw new InvalidOperationException("Review remaining decoration in " + path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllLines("output/Rendering/Switch/decoration-validation.txt",report);
        Debug.Log("PASS saved decoration cleanup reloaded with no matching inactive objects.");
    }
    private static bool Decoration(string name)
    {
        string n = name.ToLowerInvariant();
        return n.StartsWith("meadow cell ") || n.StartsWith("meadow ") ||
            (n.StartsWith("tfp_") && new[] { "grass", "wheat", "beech", "beach", "tree", "fence", "bush", "flower", "reed", "rock", "plant" }.Any(n.Contains));
    }
    private static bool VisualComponent(Component c) => c is Transform || c is MeshRenderer || c is MeshFilter || c is LODGroup || c is Collider;
    public static void Apply()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor must be idle.");
        for (int i=0;i<SceneManager.sceneCount;i++)
        {
            var open = SceneManager.GetSceneAt(i);
            if (!open.isDirty) continue;
            if (string.IsNullOrEmpty(open.path)) throw new InvalidOperationException("An untitled scene must be saved before cleanup.");
            string recovery = "Assets/_Recovery/DecorationCleanup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "/" + open.name + ".unity";
            Directory.CreateDirectory(Path.GetDirectoryName(recovery));
            if (!EditorSceneManager.SaveScene(open, recovery, true) || !EditorSceneManager.SaveScene(open))
                throw new IOException("Could not preserve open scene edits.");
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new List<string>();
        string backup = "DisabledAssets/VegetationBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        report.Add("Scene backups: " + backup);
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var transforms = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                var referenced = new HashSet<Transform>();
                var containsReference = new HashSet<Transform>();
                // Include disabled behaviours and UnityEvent object arguments. Animation,
                // constraints and other nonvisual components can also own references.
                foreach(var component in transforms.SelectMany(t=>t.GetComponents<Component>()))
                {
                    if (!component || VisualComponent(component)) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while(property.Next(true))
                    {
                        if(property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_GameObject") continue;
                        var value = property.objectReferenceValue;
                        Transform target = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
                        if (!target || target.gameObject.scene != scene) continue;
                        referenced.Add(target);
                        for(var parent=target;parent;parent=parent.parent) containsReference.Add(parent);
                    }
                }
                var candidates = transforms.Where(t=>!t.gameObject.activeInHierarchy && Decoration(t.name)).ToArray();
                int count=0, roots=0, skipped=0;
                string destination = backup + "/" + entry.path;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(entry.path,destination);
                foreach(var t in candidates)
                {
                    if (!t) continue;
                    string reason = null;
                    if(t.GetComponentsInChildren<Component>(true).Any(c=>!c || !VisualComponent(c))) reason="contains gameplay/unknown components";
                    if(containsReference.Contains(t)) reason="referenced by scene logic";
                    for(var parent=t.parent;parent && reason==null;parent=parent.parent)
                        if(referenced.Contains(parent) || parent.GetComponents<Component>().Any(c=>!c || !VisualComponent(c))) reason="controlled parent";
                    if(reason!=null) { skipped++; report.Add("KEEP " + entry.path + " | " + t.name + " | " + reason); continue; }
                    count += t.GetComponentsInChildren<Transform>(true).Length; roots++;
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
                if(count>0 && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + entry.path);
                report.Add(entry.path + ": removed " + count + " inactive decoration objects (" + roots + " roots), preserved " + skipped + " protected candidates");
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); Directory.CreateDirectory("output/Rendering/Switch"); File.WriteAllLines("output/Rendering/Switch/decoration-cleanup.txt",report); }
        Debug.Log(string.Join("\n",report.Where(s=>!s.StartsWith("KEEP "))));
    }
}
