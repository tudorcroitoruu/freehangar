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
    public static void Apply()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor must be idle.");
        for (int i=0;i<SceneManager.sceneCount;i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before cleanup.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new List<string>();
        string backup = "DisabledAssets/VegetationBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                string destination = backup + "/" + entry.path;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(entry.path,destination);
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var candidates = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                    .Where(t=>!t.gameObject.activeInHierarchy && (t.name.StartsWith("Meadow cell ",StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("TFP_Grass_",StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                int count=0;
                foreach(var t in candidates)
                {
                    if (!t) continue;
                    // Never remove gameplay scripts or their owning objects.
                    if(t.GetComponentsInChildren<Component>(true).Any(c=>!c || !(c is Transform || c is Renderer || c is MeshFilter || c is LODGroup || c is Collider))) continue;
                    count += t.GetComponentsInChildren<Transform>(true).Length;
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
                if(count>0) EditorSceneManager.SaveScene(scene);
                report.Add(entry.path + ": removed " + count + " inactive vegetation objects");
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); Directory.CreateDirectory("output/Rendering/Switch"); File.WriteAllLines("output/Rendering/Switch/vegetation.txt",report); }
        Debug.Log(string.Join("\n",report));
    }
}
