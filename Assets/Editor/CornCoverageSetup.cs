using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CornCoverageSetup
{
    [MenuItem("Tools/Grain/Upgrade pile coverage and ground scatter")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        foreach (var pile in Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var surface = pile.GetComponent<CornSurfaceKernels>();
            if (surface == null) continue;
            var data = new SerializedObject(surface);
            data.FindProperty("maximumSurfaceKernels").intValue = 500000;
            data.FindProperty("spacing").floatValue = .028f;
            data.ApplyModifiedProperties();
            var scatter = pile.GetComponent<CornGroundScatter>();
            if (scatter == null) scatter = Undo.AddComponent<CornGroundScatter>(pile.gameObject);
            var scatterData = new SerializedObject(scatter);
            scatterData.FindProperty("pile").objectReferenceValue = pile;
            scatterData.FindProperty("kernelMesh").objectReferenceValue = data.FindProperty("kernelMesh").objectReferenceValue;
            scatterData.FindProperty("kernelMaterial").objectReferenceValue = data.FindProperty("kernelMaterial").objectReferenceValue;
            scatterData.ApplyModifiedProperties();
            pile.RebuildPreview(); surface.Rebuild(); scatter.Rebuild();
            EditorSceneManager.MarkSceneDirty(pile.gameObject.scene);
        }
    }
}
