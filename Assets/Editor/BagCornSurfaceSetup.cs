using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BagCornSurfaceSetup
{
    [MenuItem("Tools/Grain/Add 3D corn to bags")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        foreach (var bag in Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var sack = bag.GetComponent<SackController>();
            if (sack == null) continue;
            var surface = System.Array.Find(bag.GetComponentsInChildren<SkinnedMeshRenderer>(true), r => r.name == "Sack_Grain");
            if (surface == null) continue;
            var detail = bag.GetComponent<BagCornSurface>();
            if (detail == null) detail = Undo.AddComponent<BagCornSurface>(bag.gameObject);
            var data = new SerializedObject(detail);
            data.FindProperty("sack").objectReferenceValue = sack;
            data.FindProperty("grainSurface").objectReferenceValue = surface;
            data.FindProperty("kernelMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/GrainPrototype/CornKernel.asset");
            data.FindProperty("kernelMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/CornKernel.mat");
            data.FindProperty("surfaceUp").vector3Value = surface.transform.InverseTransformDirection(Vector3.up);
            data.ApplyModifiedProperties();
            Undo.RecordObject(surface, "Replace flat corn texture with kernel substrate");
            surface.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/Corn.mat");
            sack.RefreshBindings(); detail.Rebuild();
            EditorSceneManager.MarkSceneDirty(bag.gameObject.scene);
        }
    }
}
