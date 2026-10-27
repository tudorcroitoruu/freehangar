using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ValidateMixedPlay
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string message) { checks++; if(!condition)throw new Exception(message); }
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static bool Near(double a,double b)=>Math.Abs(a-b)<.003;
    public static string Main()
    {
        Check(Application.isPlaying,"Play Mode required");
        checks=0;
        var listeners=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>(b is GrainProgression || b is GoalsManager) && b.enabled).ToArray();
        foreach(var listener in listeners)listener.enabled=false;
        bool savesLoaded=SaveSystem.savesLoaded; SaveSystem.savesLoaded=false;
        float timescale=Time.timeScale;Time.timeScale=1;
        var root=new GameObject("Mixed grain playtest") {hideFlags=HideFlags.HideAndDontSave};root.SetActive(false);
        try
        {
            Vector3 origin=new Vector3(1200,0,1200);
            var pileObject=new GameObject("Mixed pile");pileObject.transform.SetParent(root.transform);pileObject.transform.position=origin;
            var pile=pileObject.AddComponent<GrainPile>();
            using(var d=new SerializedObject(pile)){d.FindProperty("fitToWalls").boolValue=false;d.ApplyModifiedPropertiesWithoutUndo();}
            pile.SetRecipe(new[]{new GrainLayer(52,GrainType.Corn,GrainType.Rice,GrainType.Soybeans)});
            var rig=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player.prefab"),root.transform);
            rig.transform.position=origin+new Vector3(0,.04f,-2.55f);
            var player=rig.GetComponent<PlayerController>();player.enabled=false;player.GetComponent<CharacterController>().enabled=false;
            foreach(var cam in rig.GetComponentsInChildren<Camera>(true))cam.enabled=false;
            var tool=rig.GetComponentInChildren<ShovelTool>();var grains=rig.GetComponentInChildren<ShovelGrainPhysics>();grains.enabled=false;
            root.SetActive(true);pile.Initialize();
            var camera=player.characterCamera;
            Vector3 aim=origin+new Vector3(0,0,-1.1f);aim.y=pile.SurfaceHeight(aim);camera.transform.LookAt(aim);Physics.SyncTransforms();
            double before=pile.Kilograms;
            var rest=Field<Vector3>(tool,"restPosition");var rotation=Field<Quaternion>(tool,"restRotation");
            Check(tool.TryUse(camera,rig.transform)&&tool.IsScooping,"Mixed scoop selected by aim dot");
            bool partial=false;
            for(int i=0;i<90;i++)
            {
                tool.Tick(1f/60);tool.ComposePresentation(1f/60,Vector2.zero,0,Vector3.zero,out var offset,out var delta);
                tool.transform.localPosition=rest+offset;tool.transform.localRotation=rotation*delta;
                if(tool.Kilograms>0&&tool.Kilograms<6)partial=true;
            }
            Check(partial&&Near(tool.Kilograms,6.5),"Animated scoop transfers progressively to full load");
            Check(Near(before-pile.Kilograms,tool.Kilograms),"Physical pile/shovel mass conserved");
            foreach(var type in new[]{GrainType.Corn,GrainType.Rice,GrainType.Soybeans})Check(Near(tool.Cargo[type],6.5/3),"Held mixture contains "+type);
            var corn=Receiver(root,GrainType.Corn,camera);corn.Add(GrainType.Corn,49);
            Pour(tool,camera,rig.transform);
            Check(Near(corn.Kilograms,50)&&Near(tool.Kilograms,5.5),"Partial corn pour preserves overflow and other crops");
            Pour(tool,camera,rig.transform);
            Check(Near(tool.Kilograms,5.5),"Already-full bag cannot discard cargo");
            corn.gameObject.SetActive(false);
            var rice=Receiver(root,GrainType.Rice,camera);Pour(tool,camera,rig.transform);
            Check(Near(rice.Kilograms,6.5/3)&&Near(tool.Cargo[GrainType.Rice],0),"Rice sorted by animated pour");
            rice.gameObject.SetActive(false);
            var soy=Receiver(root,GrainType.Soybeans,camera);Pour(tool,camera,rig.transform);
            Check(Near(soy.Kilograms,6.5/3)&&Near(tool.Cargo[GrainType.Soybeans],0),"Soybeans sorted by animated pour");
            Check(Near(tool.Cargo[GrainType.Corn],6.5/3-1),"Corn leftover stays on shovel across other pours");
            soy.gameObject.SetActive(false);
            var cornOverflow=Receiver(root,GrainType.Corn,camera);Pour(tool,camera,rig.transform);
            Check(tool.Kilograms<.0001f,"Correct final receiver empties shovel");
            Check(Near(corn.Kilograms-49+rice.Kilograms+soy.Kilograms+cornOverflow.Kilograms,6.5),"All four pours conserve the original mixed scoop");
            grains.SetFill(0);var simulate=typeof(ShovelGrainPhysics).GetMethod("Simulate",Private);
            for(int i=0;i<180;i++)simulate.Invoke(grains,new object[]{1f/60});grains.SendMessage("LateUpdate");
            Check(grains.KernelCount==0&&grains.PackedKernelCount==0,"Emptied shovel hides all load kernels");
            cornOverflow.gameObject.SetActive(false);
            camera.transform.rotation=Quaternion.LookRotation(Vector3.up);tool.TryUse(camera,rig.transform);tool.Tick(2);
            Check(tool.Kilograms==0,"Dry swing creates no cargo");
            // Progression listens to actual accepted deposits, independently of the career save.
            var progression=root.AddComponent<GrainProgression>();
            using(var d=new SerializedObject(progression)){var p=d.FindProperty("piles");p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=pile;d.ApplyModifiedPropertiesWithoutUndo();}
            progression.ApplyStage(0);double remaining=pile.Kilograms;
            var receiver=Receiver(root,GrainType.Corn,camera);
            using(var d=new SerializedObject(receiver)){d.FindProperty("capacityKg").floatValue=150;d.ApplyModifiedPropertiesWithoutUndo();}
            receiver.Add(GrainType.Corn,99);progression.SendMessage("Update");
            Check(progression.CurrentStage==0&&Near(progression.BaggedKilograms,99),"99 accepted kg keeps corn stage");
            receiver.Add(GrainType.Rice,10);progression.SendMessage("Update");
            Check(Near(progression.BaggedKilograms,99),"Rejected crop does not advance progression");
            receiver.Add(GrainType.Corn,1);progression.SendMessage("Update");
            Check(progression.CurrentStage==1&&Near(pile.SurfaceGrain.Weight(0),.5)&&Near(pile.SurfaceGrain.Weight(1),.5),"100 accepted kg unlocks corn + wheat");
            Check(Near(pile.Kilograms,remaining),"Progression preserves excavated volume");
            Check(Object.FindObjectsByType<HangarGrainVisibility>(FindObjectsSortMode.None).All(h=>h.GrainRoot==null||h.GrainRoot.activeSelf==h.HangarOpen),"Editor preview never exposes closed hangars during play");
            var supplyObject=new GameObject("Test supply");supplyObject.transform.SetParent(root.transform);supplyObject.transform.position=origin+Vector3.right*10;
            var supply=supplyObject.AddComponent<GrainBagSupply>();
            using(var d=new SerializedObject(supply))
            {
                d.FindProperty("currentBag").objectReferenceValue=corn;
                d.FindProperty("emptyBagPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GrainPrototype/Grains/Sorting Sack.prefab").GetComponent<GrainContainer>();
                d.ApplyModifiedPropertiesWithoutUndo();
            }
            supply.SendMessage("Update");Check(supply.CurrentBag==corn,"Occupied sack station does not duplicate bags");
            corn.SetCarried(true);supply.SendMessage("Update");
            Check(supply.CurrentBag!=corn&&supply.CurrentBag.Kilograms==0&&supply.CurrentBag.IsOpen&&!supply.CurrentBag.HasAssignedGrain&&corn.Kilograms==50,"Carried full sack is preserved and replaced with empty unassigned sack");
            var replacement=supply.CurrentBag;supply.SendMessage("Update");Check(supply.CurrentBag==replacement,"Replacement appears once per pickup");
            return "PASS: "+checks+" Play Mode assertions; progressive 3-crop scoop, four animated sorted pours, overflow, mass conservation, dry swings, load visibility, actual bagging milestone and closed-hangar visibility.";
        }
        finally
        {
            Object.DestroyImmediate(root);SaveSystem.savesLoaded=savesLoaded;Time.timeScale=timescale;
            foreach(var listener in listeners)if(listener!=null)listener.enabled=true;
        }
    }
    static GrainContainer Receiver(GameObject root,GrainType grain,Camera camera)
    {
        var go=new GameObject(grain+" test receiver");go.transform.SetParent(root.transform);go.transform.position=camera.transform.position+camera.transform.forward*.8f;
        go.AddComponent<BoxCollider>().size=Vector3.one*.4f;var bag=go.AddComponent<GrainContainer>();
        using(var d=new SerializedObject(bag)){d.FindProperty("restrictGrain").boolValue=true;d.FindProperty("acceptedGrain").enumValueIndex=(int)grain;d.ApplyModifiedPropertiesWithoutUndo();}
        Physics.SyncTransforms();return bag;
    }
    static void Pour(ShovelTool tool,Camera camera,Transform player)
    {
        Check(tool.TryUse(camera,player),"Pour gesture starts");
        for(int i=0;i<90;i++)tool.Tick(1f/60);
        Check(!tool.IsBusy,"Pour gesture finishes");
    }
}
