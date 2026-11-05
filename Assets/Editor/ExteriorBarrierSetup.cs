using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExteriorBarrierSetup
{
    [InitializeOnLoadMethod] private static void OnImport() => EditorApplication.delayCall += () =>
    {
        if (File.Exists("output/exterior-walls.request"))
        {
            File.Delete("output/exterior-walls.request");
            Apply();
        }
    };

    [MenuItem("Tools/Grain/Expose exterior wall copies")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Gameplay") return;
        var report = new StringBuilder();
        int count = 0;
        foreach (var barrier in Object.FindObjectsByType<BreakableGrainBarrier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (barrier.gameObject.scene != scene) continue;
            var data = new SerializedObject(barrier);
            var id = data.FindProperty("barrierId");
            // These are the duplicated panels used for the exterior enclosures.
            if (id.stringValue != "farm.barrier.h5.metal" && id.stringValue != "farm.barrier.h2.wood.p1") continue;
            var pile = data.FindProperty("coveringPile").objectReferenceValue as GrainPile;
            if (pile == null) continue;
            Vector3 local = pile.transform.InverseTransformPoint(barrier.transform.position);
            if (Mathf.Abs(local.x) <= pile.Size * .5f && Mathf.Abs(local.z) <= pile.Size * .5f) continue;
            Undo.RecordObject(barrier, "Expose exterior wall");
            id.stringValue = "farm.barrier.exterior." + GlobalObjectId.GetGlobalObjectIdSlow(barrier).targetObjectId;
            data.FindProperty("alwaysExposed").boolValue = true;
            data.FindProperty("coveringPile").objectReferenceValue = null;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(barrier);
            report.AppendLine(barrier.name + " -> " + id.stringValue);
            count++;
        }
        if (count > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        File.WriteAllText("output/exterior-walls.txt", "Configured " + count + " exterior walls.\n" + report);
    }
}
