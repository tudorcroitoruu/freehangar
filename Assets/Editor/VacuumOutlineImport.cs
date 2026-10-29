using UnityEditor;
using UnityEngine;

/// <summary>Remove the imported inverted black outline shell from the level-five vacuum.</summary>
public sealed class VacuumOutlineImport : AssetPostprocessor
{
    private const string ModelPath = "Assets/Models/Models New/Shovel/Lvl_5/toon-vacuum-cleaner/source/Vacum Cleaner.fbx";

    private void OnPostprocessModel(GameObject model)
    {
        if (assetPath != ModelPath) return;
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var materials = renderer.sharedMaterials;
            // This export places the inverted hull after the original textured surface.
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.subMeshCount != 2 ||
                materials.Length != 2 || materials[1] == null || materials[1].name != "Material.001") continue;
            filter.sharedMesh.subMeshCount = 1;
            renderer.sharedMaterials = new[] { materials[0] };
        }
    }
}
