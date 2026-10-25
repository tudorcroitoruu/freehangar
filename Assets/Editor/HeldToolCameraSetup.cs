using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class HeldToolCameraSetup
{
    [MenuItem("Tools/Grain/Set up held-tool camera")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Run in Edit Mode");
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null || player.characterCamera == null) throw new System.InvalidOperationException("Player camera missing");
        var playerData = new SerializedObject(player);
        var hands = (Transform)playerData.FindProperty("heldInstrument").objectReferenceValue;
        if (hands == null) throw new System.InvalidOperationException("Held instrument missing");
        int layer = LayerMask.NameToLayer("HeldTools");
        if (layer < 0)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layer = i; layers.GetArrayElementAtIndex(i).stringValue = "HeldTools"; break; }
            if (layer < 0) throw new System.InvalidOperationException("No free layer");
            tags.ApplyModifiedProperties();
        }
        int mask = 1 << layer;
        foreach (var child in hands.GetComponentsInChildren<Transform>(true))
        { Undo.RecordObject(child.gameObject, "Held tool layer"); child.gameObject.layer = layer; }
        var world = player.characterCamera;
        Undo.RecordObject(world, "Exclude held tool from world camera"); world.cullingMask &= ~mask;
        var existing = world.transform.Find("Held Tool Camera");
        var go = existing != null ? existing.gameObject : new GameObject("Held Tool Camera");
        if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create held tool camera");
        go.transform.SetParent(world.transform, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        var camera = go.GetComponent<Camera>(); if (camera == null) camera = Undo.AddComponent<Camera>(go);
        camera.CopyFrom(world); camera.enabled = false; camera.cullingMask = mask; camera.nearClipPlane = .01f;
        camera.clearFlags = CameraClearFlags.Depth; camera.depth = world.depth + 1;
        var hd = go.GetComponent<HDAdditionalCameraData>(); if (hd == null) hd = Undo.AddComponent<HDAdditionalCameraData>(go);
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.None; hd.clearDepth = true;
        var driver = world.GetComponent<HeldToolCamera>(); if (driver == null) driver = Undo.AddComponent<HeldToolCamera>(world.gameObject);
        var data = new SerializedObject(driver);
        data.FindProperty("worldCamera").objectReferenceValue = world;
        data.FindProperty("toolCamera").objectReferenceValue = camera;
        data.FindProperty("toolLayer").intValue = mask; data.ApplyModifiedProperties();
        var volume = go.GetComponent<CustomPassVolume>(); if (volume == null) volume = Undo.AddComponent<CustomPassVolume>(go);
        Undo.RecordObject(volume, "Configure held tool pass");
        volume.isGlobal = true; volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        var pass = volume.customPasses.Find(p => p is HeldToolPass) as HeldToolPass;
        if (pass == null) { pass = new HeldToolPass(); volume.customPasses.Add(pass); }
        pass.name = "Held tools over world"; pass.worldCamera = world; pass.toolCamera = camera; pass.toolLayer = mask;
        EditorUtility.SetDirty(volume);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
    }
}
