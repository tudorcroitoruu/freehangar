using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CollectablesHierarchySetup
{
    [InitializeOnLoadMethod] private static void RequestedRun() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists("output/Collectables/run.request")) return;
        File.Delete("output/Collectables/run.request");
        try { Apply(); } catch (Exception e) { File.WriteAllText("output/Collectables/result.txt", e.ToString()); Debug.LogException(e); }
    };

    [MenuItem("Tools/Grain/Organize collectables")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        var map = scene.GetRootGameObjects().Single(x => x.name == "New_Map").transform;
        var parent = Group(map, "Collectables");
        var moves = new Dictionary<Transform, string>();
        var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var node in all)
        {
            var find = node.GetComponent<BuriedFind>();
            if (find != null) moves[node] = find.FindKind switch
            {
                BuriedFind.Kind.Key or BuriedFind.Kind.HangarKey or BuriedFind.Kind.VehicleKey => "Keys",
                BuriedFind.Kind.CashChest => "Chests",
                BuriedFind.Kind.InfiniteStamina or BuriedFind.Kind.PerkBottle or BuriedFind.Kind.UpgradeVoucher => "Perks",
                BuriedFind.Kind.Axe => "Axes", BuriedFind.Kind.Hammer => "Hammers",
                BuriedFind.Kind.Broom => "Brooms", BuriedFind.Kind.Pistol => "Pistols",
                _ => "Cash"
            };
            var barrow = node.GetComponent<WheelbarrowCargo>();
            if (barrow != null)
            {
                var hangar = node.GetComponentInParent<HangarGrainVisibility>();
                if (hangar != null)
                {
                    var data = new SerializedObject(barrow);
                    data.FindProperty("discoveryHangar").objectReferenceValue = hangar;
                    data.ApplyModifiedProperties();
                }
                moves[node] = "Wheelbarrows";
            }
        }
        // Freeze existing path-derived save identities before changing any paths.
        foreach (var node in moves.Keys)
        foreach (var child in node.GetComponentsInChildren<Transform>(true))
            if (child.GetComponent<WheelbarrowCargo>() || child.GetComponent<BreakableGrainBarrier>() || child.GetComponent<GrainContainer>() || child.GetComponent<CarryableItem>() || child.GetComponent<ConsumableFood>())
            {
                WorldSaveRuntime.Identity(child.gameObject, "scene:" + WorldSaveRuntime.Path(child, null));
                EditorUtility.SetDirty(child.GetComponent<WorldSaveIdentity>());
            }
        var report = new System.Text.StringBuilder();
        foreach (var move in moves)
        {
            var node = move.Key;
            var position = node.position; var rotation = node.rotation; var scale = node.lossyScale;
            var target = Group(parent, move.Value);
            if (node.parent != target) Undo.SetTransformParent(node, target, "Organize collectables");
            if (Vector3.Distance(position, node.position) > .001f || Quaternion.Angle(rotation, node.rotation) > .01f || Vector3.Distance(scale, node.lossyScale) > .001f)
                throw new InvalidOperationException("Transform changed: " + node.name);
            report.AppendLine(move.Value + "/" + node.name);
        }
        foreach (var node in all.Reverse())
            if (node != null && node.childCount == 0 && node.GetComponents<Component>().Length == 1 &&
                (node.name == "15_Buried_Finds" || node.name == "Vehicle keys - Hangars 2-4" || node.name == "Matching buried copper and iron chests" || node.name == "Scattered treasure and perk bottles"))
                Undo.DestroyObjectImmediate(node.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("output/Collectables/result.txt", "Organized " + moves.Count + " objects; world transforms preserved.\n" + report);
        Selection.activeGameObject = parent.gameObject;
    }
    private static Transform Group(Transform parent, string name)
    {
        var found = parent.Find(name);
        if (found != null) return found;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create collectible group");
        go.transform.SetParent(parent, false);
        return go.transform;
    }
}
