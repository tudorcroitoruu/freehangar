using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class CargoScenePlacementAudit
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Vehicles/Match legacy scene sacks to cargo preview")]
    public static void FixLegacySacks()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode before fixing authored sacks.");
        const string path="Assets/Scenes/Gameplay.unity";
        var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
            Vector3 scale=prefab.transform.lossyScale;int changed=0;
            foreach(var bag in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GrainContainer>(true)))
            {
                var data=new SerializedObject(bag);
                if(!string.IsNullOrWhiteSpace(data.FindProperty("cargoTypeId").stringValue)||bag.TransportLevel!=1||bag.GetComponent<SackController>()==null)continue;
                Undo.RecordObject(bag.transform,"Match legacy sack to cargo preview");
                Vector3 parent=bag.transform.parent!=null?bag.transform.parent.lossyScale:Vector3.one;
                bag.transform.localScale=new Vector3(scale.x/parent.x,scale.y/parent.y,scale.z/parent.z);
                data.FindProperty("cargoTypeId").stringValue="FreeBag";data.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(bag.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(bag);
                changed++;
            }
            if(changed>0){EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save scene cargo correction.");}
            Debug.Log("Matched "+changed+" legacy scene sacks to FreeBag preview scale and identity. Cargo quantities unchanged.");
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        Run();
    }
    [MenuItem("Tools/Vehicles/Inspect scene cargo placement")]
    public static void Run()
    {
        bool preview=!Application.isPlaying;var scene=preview?EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay.unity"):SceneManager.GetActiveScene();
        var text=new StringBuilder("Actual scene cargo placement audit\n");
        GameObject scratch=null;var previous=SaveSystem.Data;
        try
        {
            var roots=scene.GetRootGameObjects();
            var bags=roots.SelectMany(r=>r.GetComponentsInChildren<GrainContainer>(true)).ToArray();
            var bikes=roots.SelectMany(r=>r.GetComponentsInChildren<VehicleUpgrade>(true)).Where(v=>v.VehicleId=="bicycle").ToArray();
            foreach(var v in bikes){var rack=v.GetComponent<VehicleCargoRack>();text.AppendLine($"BIKE {v.name} worldScale={v.transform.lossyScale} rootEuler={v.transform.eulerAngles} rack={(rack!=null?rack.surface?.position.ToString():"none")} rackEuler={rack?.surface?.eulerAngles}");}
            foreach(var bag in bags)
                text.AppendLine($"BAG {bag.name} id={bag.CargoTypeId} localScale={bag.transform.localScale} worldScale={bag.transform.lossyScale} euler={bag.transform.eulerAngles} parent={bag.transform.parent?.name} kg={bag.Kilograms}/{bag.CapacityKg} prefab={AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(bag.gameObject))}");
            if(!preview||bikes.Length==0)return;
            scratch=new GameObject("Scene cargo comparison");SceneManager.MoveGameObjectToScene(scratch,scene);SaveSystem.Data=new PlayerData();
            var actor=new GameObject("Carrier");actor.transform.SetParent(scratch.transform);var carrier=actor.AddComponent<BagInteraction>();var anchor=new GameObject("Hands");anchor.transform.SetParent(actor.transform);Set(carrier,"carryAnchor",anchor.transform);
            foreach(var original in bags.Where(b=>b.TransportLevel==1&&b.GetComponent<SackController>()!=null))
            {
                var bicycle=Object.Instantiate(bikes[0].gameObject,scratch.transform);bicycle.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);bicycle.transform.localScale=bikes[0].transform.lossyScale;
                var slot=bicycle.GetComponent<VehicleCargoRack>().FreeSlot(true);
                var bag=Object.Instantiate(original.gameObject,scratch.transform);bag.SetActive(true);bag.transform.localScale=original.transform.lossyScale;bag.transform.rotation=original.transform.rotation;VehicleCargoPreview.PrepareContainer(bag,1,1);
                var expected=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab"),scratch.transform);VehicleCargoPreview.PrepareContainer(expected,1,1);
                expected.transform.rotation=Quaternion.AngleAxis(slot.eulerAngles.y,Vector3.up)*expected.transform.rotation;
                VehicleCargoPlacement.Place(expected.transform,slot,bicycle.GetComponent<VehicleUpgrade>().Profile,"FreeBag",0,Quaternion.Inverse(slot.rotation)*expected.transform.rotation,true,expected.transform.localScale);
                bool taken=(bool)typeof(BagInteraction).GetMethod("Take",Private).Invoke(carrier,new object[]{bag.GetComponent<GrainContainer>()});bool stowed=taken&&carrier.TryStow(slot);
                text.AppendLine($"COMPARE {original.name}: taken={taken} stowed={stowed} position delta={Vector3.Distance(expected.transform.position,bag.transform.position):F4} angle delta={Quaternion.Angle(expected.transform.rotation,bag.transform.rotation):F2} scale delta={Vector3.Distance(expected.transform.lossyScale,bag.transform.lossyScale):F4} expected={expected.transform.position} actual={bag.transform.position}");
                Object.DestroyImmediate(bicycle);Object.DestroyImmediate(bag);Object.DestroyImmediate(expected);
            }
        }
        finally
        {
            if(preview){SaveSystem.Data=previous;if(scratch!=null)Object.DestroyImmediate(scratch);EditorSceneManager.ClosePreviewScene(scene);}
            Directory.CreateDirectory("output/Cargo");File.WriteAllText("output/Cargo/actual-scene.txt",text.ToString());Debug.Log(text.ToString());
        }
    }
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
}
