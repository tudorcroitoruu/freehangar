using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GrainSpatialSetup
{
    public static GrainRegion[] Preset(int hangar)
    {
        switch (hangar)
        {
            case 1: return new[] { new GrainRegion(100, GrainType.Corn) };
            case 2: return new[] { new GrainRegion(60, GrainType.Corn), new GrainRegion(20, GrainType.Wheat), new GrainRegion(20, GrainType.Corn, GrainType.Wheat) };
            case 3: return new[] { new GrainRegion(20, GrainType.Wheat), new GrainRegion(20, GrainType.Barley), new GrainRegion(20, GrainType.Oats), new GrainRegion(20, GrainType.Rice), new GrainRegion(20, GrainType.Soybeans) };
            case 4: return new[] {
                new GrainRegion(10, GrainType.Wheat), new GrainRegion(5, GrainType.Rice), new GrainRegion(10, GrainType.Wheat),
                new GrainRegion(15, GrainType.Oats), new GrainRegion(20, GrainType.Soybeans), new GrainRegion(20, GrainType.Barley),
                new GrainRegion(15, GrainType.Rice), new GrainRegion(5, GrainType.Corn) };
            default: return new[] {
                new GrainRegion(15, GrainType.Rice), new GrainRegion(10, GrainType.Peas), new GrainRegion(10, GrainType.Soybeans, GrainType.Buckwheat),
                new GrainRegion(5, GrainType.Corn), new GrainRegion(10, GrainType.Rye), new GrainRegion(10, GrainType.SunflowerSeeds),
                new GrainRegion(10, GrainType.Oats), new GrainRegion(10, GrainType.Barley, GrainType.Wheat),
                new GrainRegion(10, GrainType.Rice), new GrainRegion(10, GrainType.Soybeans) };
        }
    }
    [MenuItem("Tools/Grain/Apply spatial hangar layouts")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play Mode before authoring spatial layouts.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var pile in Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pile.gameObject.scene != scene) continue;
            int hangar = 1;
            for (int i = 2; i <= 5; i++) if (pile.name.StartsWith("Hangar " + i)) hangar = i;
            Undo.RecordObject(pile, "Author spatial hangar stock");
            pile.ConfigureRegions(Preset(hangar), hangar == 3 ? GrainSpatialField.Arrangement.ChunksAndCurves : hangar >= 4 ? GrainSpatialField.Arrangement.Patches : GrainSpatialField.Arrangement.FrontToBack, hangar >= 4 ? .23f : .09f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pile);
            EditorUtility.SetDirty(pile);
            pile.GetComponent<CornSurfaceKernels>()?.Rebuild();
        }
        EditorSceneManager.MarkSceneDirty(scene); SceneView.RepaintAll();
    }
}

