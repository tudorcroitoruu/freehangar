using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class ValidateSpatialPlay
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");checks=0;
        var listeners=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>(b is GrainProgression||b is GoalsManager)&&b.enabled).ToArray();
        foreach(var listener in listeners)listener.enabled=false;
        var root=new GameObject("Spatial runtime fixture"){hideFlags=HideFlags.HideAndDontSave};root.SetActive(false);
        try
        {
            var scenePiles=Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var scenePile in scenePiles)
            {
                scenePile.Initialize();
                if(scenePile.name.StartsWith("Hangar 4")||scenePile.name.StartsWith("Hangar 5"))
                    Check(Math.Abs(scenePile.GrainKilograms(GrainType.Corn)/scenePile.Kilograms-.05)<.0001,scenePile.name+" starts with 5% corn");
            }
            var first=scenePiles.First(p=>!p.name.StartsWith("Hangar"));
            foreach(var progression in listeners.OfType<GrainProgression>())
            {
                int before=progression.CurrentStage;progression.ApplyStage(progression.StageCount-1);
                Check(Math.Abs(first.GrainKilograms(GrainType.Corn)-first.Kilograms)<.001,"First hangar stays entirely corn at the final milestone");
                progression.ApplyStage(Mathf.Max(0,before));
            }
            root.transform.position=new Vector3(1400,0,1400);
            var pile=root.AddComponent<GrainPile>();
            using(var d=new SerializedObject(pile))
            {
                d.FindProperty("useSpatialLayout").boolValue=true;d.FindProperty("fitToWalls").boolValue=false;
                d.FindProperty("size").floatValue=4;d.FindProperty("initialHeight").floatValue=1;
                d.FindProperty("arrangement").enumValueIndex=(int)GrainSpatialField.Arrangement.LeftToRight;
                d.FindProperty("regionWaviness").floatValue=0;
                var regions=d.FindProperty("regions");regions.arraySize=2;
                for(int i=0;i<2;i++)
                {
                    var r=regions.GetArrayElementAtIndex(i);r.FindPropertyRelative("percent").floatValue=50;
                    var layers=r.FindPropertyRelative("layers");layers.arraySize=1;layers.GetArrayElementAtIndex(0).FindPropertyRelative("percent").floatValue=100;
                    var parts=layers.GetArrayElementAtIndex(0).FindPropertyRelative("mixture").FindPropertyRelative("parts");parts.arraySize=1;
                    parts.GetArrayElementAtIndex(0).FindPropertyRelative("grain").enumValueIndex=i==0?0:5;parts.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue=1;
                }
                d.ApplyModifiedPropertiesWithoutUndo();
            }
            root.SetActive(true);pile.Initialize();
            double initialRice=pile.GrainKilograms(GrainType.Rice),initialCorn=pile.GrainKilograms(GrainType.Corn);
            var cargo=new GrainCargo();var point=root.transform.position+Vector3.right;
            point.y=pile.SurfaceHeight(point);pile.SampleSurfaceGrain(point,cargo);
            Check(cargo[GrainType.Rice]>.99,"Right-side visible surface is rice");cargo.Clear();
            float removed=pile.Scoop(point,.4f,.18f,6.5f,cargo);
            Check(Math.Abs(removed-6.5)<.003&&Math.Abs(cargo[GrainType.Rice]-removed)<.003&&cargo[GrainType.Corn]==0,"Right-side scoop collects only rice");
            Check(Math.Abs(initialRice-pile.GrainKilograms(GrainType.Rice)-removed)<.003&&Math.Abs(initialCorn-pile.GrainKilograms(GrainType.Corn))<.003,"Rice excavation leaves corn stock unchanged");
            var bagObject=new GameObject("Spatial rice bag");bagObject.transform.SetParent(root.transform);var bag=bagObject.AddComponent<GrainContainer>();
            Check(Math.Abs(cargo.PourInto(bag,10)-removed)<.003&&bag.AcceptedGrain==GrainType.Rice,"Spatial rice can be sorted into a rice receiver");
            cargo.Clear();point=root.transform.position;point.y=pile.SurfaceHeight(point);
            removed=pile.Scoop(point,.4f,.18f,6.5f,cargo);
            Check(cargo[GrainType.Corn]>0&&cargo[GrainType.Rice]>0&&Math.Abs(cargo.Total-removed)<.003,"Boundary scoop contains both neighboring crops without loss");
            var block=new MaterialPropertyBlock();pile.ApplySpatialAppearance(block);
            Check(block.GetTexture("_GrainMapA")!=null&&block.GetTexture("_GrainMapB")!=null,"Runtime shader maps survive collection");
            return "PASS: "+checks+" spatial Play Mode assertions; saved hangar percentages, milestone protection, location-specific scoop, boundary mixing, sorting and shader maps.";
        }
        finally{Object.DestroyImmediate(root);foreach(var listener in listeners)if(listener!=null)listener.enabled=true;}
    }
}
