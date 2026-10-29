using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reuses the project's existing corn system; no additional blend shapes.
public static class ApplyOriginalCornToSelectedSack
{
    private const string Menu = "Tools/Grain/Apply original corn to selected sack";

    [MenuItem(Menu)]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before applying the original corn.");
            return;
        }

        Mesh kernel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/GrainPrototype/CornKernel.asset");
        Material kernelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/CornKernel.mat");
        Material substrate = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/Corn.mat");
        if (kernel == null || kernelMaterial == null || substrate == null)
            throw new InvalidOperationException("The original CornKernel mesh/material or Corn material is missing from Assets/GrainPrototype.");

        var roots = new HashSet<GameObject>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!selected.scene.IsValid()) continue;
            SackController existing = selected.GetComponentInParent<SackController>(true);
            if (existing != null) { roots.Add(existing.gameObject); continue; }
            for (Transform candidate = selected.transform; candidate != null; candidate = candidate.parent)
            {
                var renderers = candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (Array.Exists(renderers, r => r.name.StartsWith("Sack_Body", StringComparison.Ordinal)) &&
                    Array.Exists(renderers, r => r.name.StartsWith("Sack_Grain", StringComparison.Ordinal)))
                { roots.Add(candidate.gameObject); break; }
            }
        }

        int applied = 0;
        foreach (GameObject root in roots)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var grain = Array.Find(renderers, r => r.name.StartsWith("Sack_Grain", StringComparison.Ordinal));
            if (grain == null) continue;
            SackController sack = root.GetComponent<SackController>();
            if (sack == null) sack = Undo.AddComponent<SackController>(root);
            BagCornSurface corn = root.GetComponent<BagCornSurface>();
            if (corn == null) corn = Undo.AddComponent<BagCornSurface>(root);

            Undo.RecordObject(corn, "Use original corn on sack");
            var data = new SerializedObject(corn);
            data.FindProperty("sack").objectReferenceValue = sack;
            data.FindProperty("grainSurface").objectReferenceValue = grain;
            data.FindProperty("kernelMesh").objectReferenceValue = kernel;
            data.FindProperty("kernelMaterial").objectReferenceValue = kernelMaterial;
            data.FindProperty("surfaceUp").vector3Value = grain.transform.InverseTransformDirection(Vector3.up);
            data.ApplyModifiedProperties();
            corn.enabled = true;

            Undo.RecordObject(grain, "Use original corn substrate");
            grain.sharedMaterial = substrate;
            sack.RefreshBindings();
            corn.Rebuild();
            PrefabUtility.RecordPrefabInstancePropertyModifications(corn);
            PrefabUtility.RecordPrefabInstancePropertyModifications(grain);
            EditorSceneManager.MarkSceneDirty(root.scene);
            applied++;
        }

        SceneView.RepaintAll();
        if (applied == 0) Debug.LogWarning("Select an instance of the new sack in the scene, then run this command again.");
        else Debug.Log("Original 3D corn applied to " + applied + " sack(s), using the existing CornKernel and BagCornSurface assets.");
    }
}
