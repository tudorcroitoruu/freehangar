using System;
using UnityEditor;
using UnityEngine;

public static class GrainTypesValidation
{
    private static int assertions;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static bool Near(double a, double b, double epsilon = .002) => Math.Abs(a - b) < epsilon;
    [MenuItem("Tools/Grain/Validate mixed grains and progression")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this model suite in Edit Mode so it cannot alter career progress.");
        assertions = 0;
        var root = new GameObject("Grain type validation") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var pile = root.AddComponent<GrainPile>();
            var data = new SerializedObject(pile); data.FindProperty("fitToWalls").boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            pile.SetRecipe(new[] { new GrainLayer(52, GrainType.Corn), new GrainLayer(52, GrainType.Rice, GrainType.Soybeans) });
            var cargo = new GrainCargo();
            pile.CollectRecipe(0, 52, cargo);
            Check(Near(cargo[GrainType.Corn],52) && Near(cargo.Total,52), "First eight full shovels are corn");
            cargo.Clear(); pile.CollectRecipe(51, 3, cargo);
            Check(Near(cargo[GrainType.Corn],1) && Near(cargo[GrainType.Rice],1) && Near(cargo[GrainType.Soybeans],1), "Crossing a layer includes both layers and all mixed components");
            cargo.Clear(); pile.CollectRecipe(52, 6.5f, cargo);
            Check(Near(cargo[GrainType.Corn],0) && Near(cargo[GrainType.Rice],3.25) && Near(cargo[GrainType.Soybeans],3.25), "Ninth scoop reveals rice/soy mixture");
            pile.Initialize(); double previewMass = pile.Kilograms;
            data.Update(); data.FindProperty("previewHarvestedKg").floatValue = 52; data.ApplyModifiedPropertiesWithoutUndo();
            Check(Near(pile.SurfaceGrain.Weight(5),.5) && Near(pile.SurfaceGrain.Weight(7),.5) && Near(pile.Kilograms,previewMass), "Edit Mode scrubbing changes visible crops without changing mass");
            data.Update(); data.FindProperty("previewHarvestedKg").floatValue = 0; data.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < GrainCargo.TypeCount; i++)
            {
                cargo.Clear(); new GrainMixture((GrainType)i).AddTo(cargo, 6.5f);
                Check(Near(cargo[(GrainType)i],6.5), "Single crop mass: " + (GrainType)i);
            }
            var a = new GameObject("Rice receiver"); a.transform.SetParent(root.transform); var rice = a.AddComponent<GrainContainer>();
            var b = new GameObject("Soy receiver"); b.transform.SetParent(root.transform); var soy = b.AddComponent<GrainContainer>();
            var c = new GameObject("Corn receiver"); c.transform.SetParent(root.transform); var corn = c.AddComponent<GrainContainer>();
            root.SetActive(true);
            rice.Add(GrainType.Rice, 49); soy.Add(GrainType.Soybeans,1); corn.Add(GrainType.Corn,1);
            cargo.Clear(); cargo.Add(GrainType.Corn,2); cargo.Add(GrainType.Rice,2); cargo.Add(GrainType.Soybeans,2);
            Check(Near(cargo.PourInto(rice,6),1) && Near(cargo.Total,5) && Near(rice.Kilograms,50), "Full receiver leaves excess and other crops on shovel");
            Check(cargo.PourInto(rice,6) == 0 && rice.Add(GrainType.Corn,1) == 0, "Full/wrong crop rejected");
            Check(Near(cargo.PourInto(soy,6),2) && Near(cargo.Total,3), "Soy sorted separately");
            Check(Near(cargo.PourInto(corn,6),2) && Near(cargo[GrainType.Rice],1), "Corn sorted separately, rice retained");
            var d = new GameObject("Unassigned receiver"); d.transform.SetParent(root.transform); var empty = d.AddComponent<GrainContainer>();
            Check(Near(cargo.PourInto(empty,10),1) && empty.AcceptedGrain == GrainType.Rice && Near(cargo.Total,0), "Empty receiver binds to first crop");
            Check(empty.Add(GrainType.Corn,1) == 0 && empty.Add(GrainType.Rice,float.NaN) == 0 && empty.Add(GrainType.Rice,float.PositiveInfinity) == 0, "Invalid transfer cannot change receiver");
            pile.SetRecipe(new[] { new GrainLayer(52, GrainType.Wheat, GrainType.Barley, GrainType.Oats) });
            pile.Initialize(); double before = pile.Kilograms;
            cargo.Clear();
            var point = new Vector3(0,pile.SurfaceHeight(Vector3.zero),0);
            float scooped = pile.Scoop(point,.4f,.18f,6.5f,cargo);
            Check(Near(scooped,6.5) && Near(cargo.Total,scooped), "Physical scoop returns typed cargo");
            Check(Near(before - pile.Kilograms, cargo.Total, .01), "Physical pile + shovel mass conserved");
            Check(Near(cargo[GrainType.Wheat],scooped/3) && Near(cargo[GrainType.Barley],scooped/3) && Near(cargo[GrainType.Oats],scooped/3), "Three crops collected simultaneously");
            double remaining = pile.Kilograms;
            pile.SetRecipe(new[] { new GrainLayer(52, GrainType.Rice) });
            Check(Near(pile.Kilograms,remaining), "Changing progression recipe does not refill excavation");
            var progression = root.AddComponent<GrainProgression>();
            Check(progression.StageFor(0)==0 && progression.StageFor(99.99)==0 && progression.StageFor(100)==1, "First progression milestone is bagged 100 kg");
            Check(progression.StageFor(249.99)==1 && progression.StageFor(250)==2 && progression.StageFor(500)==3 && progression.StageFor(1000)==4, "Later progression thresholds");
            var saved = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(new PlayerData { grainBaggedKilograms = 283.75 }));
            Check(saved.grainBaggedKilograms == 283.75 && JsonUtility.FromJson<PlayerData>("{}").grainBaggedKilograms == 0, "Progress saves and old saves default to corn");
            Check(!ShaderUtil.ShaderHasError(Shader.Find("Freehangar/HDRP/Grain Kernels")), "Grain shader compiles");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("Mixed grain validation passed: " + assertions + " assertions.");
    }
}
