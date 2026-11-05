using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuriedChestSetup
{
    private const string GroupName="Matching buried copper and iron chests";
    [InitializeOnLoadMethod] private static void AutoSetup() => EditorApplication.delayCall+=()=>
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="Gameplay")return;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())if(root.name==GroupName){EnableRewardPool();ApplyPresentation();return;}
        if (GameObject.Find("New_Map/Collectables/Chests") != null) { EnableRewardPool(); ApplyPresentation(); return; }
        Apply();
    };
    [MenuItem("Tools/Grain/Place matching buried key chests")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")return;
        var keys=new List<BuriedFind>();var chests=new List<BuriedFind>();
        foreach(var find in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(find.gameObject.scene!=scene)continue;
            if(find.FindKind==BuriedFind.Kind.Key)keys.Add(find);
            if(find.FindKind==BuriedFind.Kind.CashChest)chests.Add(find);
        }
        if(keys.Count==0)return;
        keys.Sort((a,b)=>string.CompareOrdinal(a.name+ a.transform.position.ToString("F3"),b.name+b.transform.position.ToString("F3")));
        bool wasDirty=scene.isDirty;
        GameObject group=null;foreach(var root in scene.GetRootGameObjects())if(root.name==GroupName)group=root;
        if(group==null){group=new GameObject(GroupName);SceneManager.MoveGameObjectToScene(group,scene);Undo.RegisterCreatedObjectUndo(group,"Add matching buried chests");}
        var usedIds=new HashSet<string>();var usedChests=new HashSet<BuriedFind>();var perFamily=new Dictionary<string,int>();var perHangar=new Dictionary<int,int>();
        var report=new System.Text.StringBuilder();
        foreach(var key in keys)
        {
            string originalId=key.FindId;
            if(string.IsNullOrWhiteSpace(originalId)){report.AppendLine("SKIP key without a save ID: "+key.name);continue;}
            string label=originalId.ToLowerInvariant().Contains("copper")||originalId.ToLowerInvariant().Contains("cooper")?"Copper":originalId.ToLowerInvariant().Contains("iron")?"Iron":"Silver";
            int family=perFamily.TryGetValue(label,out int number)?number:0;perFamily[label]=family+1;
            string id=originalId;int suffix=2;while(!usedIds.Add(id))id=originalId+".instance."+suffix++;
            Undo.RecordObject(key,"Link chest key");key.ConfigureChestKey(id,label);EditorUtility.SetDirty(key);PrefabUtility.RecordPrefabInstancePropertyModifications(key);
            BuriedFind chest=chests.Find(c=>!usedChests.Contains(c) && c.RequiredChestKeyId==id);
            if(chest==null)chest=chests.Find(c=>!usedChests.Contains(c) && c.RequiredChestKeyId==originalId);
            bool created=chest==null;
            if(chest==null)
            {
                var host=new GameObject(label+" chest");SceneManager.MoveGameObjectToScene(host,scene);
                Undo.RegisterCreatedObjectUndo(host,"Add matching chest");chest=host.AddComponent<BuriedFind>();chests.Add(chest);
            }
            usedChests.Add(chest);
            int baseHangar=label=="Copper"?2:label=="Iron"?4:3;
            int hangar=2+(baseHangar-2+family)%3;
            int index=perHangar.TryGetValue(hangar,out int current)?current:0;perHangar[hangar]=index+1;
            float progress=Mathf.Min(.85f,.42f+index*.2f);
            var pile=VehicleKeyBurial.FindPile(scene,hangar);
            if(!VehicleKeyBurial.TryPlacement(pile,VehicleKeyBurial.FindEntrance(scene,hangar,pile),progress,out var point))throw new InvalidOperationException("Cannot bury matching chest in Hangar "+hangar);
            Material tint=ChestMaterial(label);
            Undo.RecordObject(chest,"Configure matching chest");Undo.RecordObject(chest.transform,"Bury matching chest");
            Undo.SetTransformParent(chest.transform,group.transform,"Organize buried chests");
            chest.transform.SetPositionAndRotation(point,Quaternion.LookRotation((VehicleKeyBurial.FindEntrance(scene,hangar,pile)-point).normalized,Vector3.up));
            // Keep chest upright even when the door lock is higher than the floor.
            chest.transform.rotation=Quaternion.Euler(0,chest.transform.eulerAngles.y,0);
            EnsureGeometry(chest,tint);
            string chestId=string.IsNullOrEmpty(chest.FindId)?"farm.chest.for."+id:chest.FindId;
            chest.ConfigureMatchingChest(chestId,id,label,pile,tint);
            chest.name=$"{label} chest - HANGAR {hangar} - key {id}";
            foreach(var renderer in chest.GetComponentsInChildren<Renderer>(true)) { renderer.enabled=true;renderer.sharedMaterial=tint; }
            chest.gameObject.SetActive(true);EditorUtility.SetDirty(chest);
            PrefabUtility.RecordPrefabInstancePropertyModifications(chest);PrefabUtility.RecordPrefabInstancePropertyModifications(chest.transform);
            report.AppendLine($"PASS {label} key {id} -> {chest.name}; {(created?"created":"repaired existing")}; {chest.GetComponentsInChildren<Renderer>(true).Length} mesh renderers; {progress:P0} from doorway.");
        }
        ApplyPresentation();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!wasDirty)EditorSceneManager.SaveScene(scene);
        report.AppendLine($"Matched {usedChests.Count} chests to {keys.Count} chest keys. Hangar door keys and vehicle keys are separate.");
        Directory.CreateDirectory("output/BuriedChests");File.WriteAllText("output/BuriedChests/validation.txt",report.ToString());Debug.Log(report.ToString(),group);SceneView.RepaintAll();
    }
    [MenuItem("Tools/Grain/Enable chest reward pool")]
    public static void EnableRewardPool()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        int count=0;
        foreach(var chest in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(chest.FindKind!=BuriedFind.Kind.CashChest || chest.gameObject.scene!=SceneManager.GetActiveScene())continue;
            var data=new SerializedObject(chest);
            var enabled=data.FindProperty("variedChestRewards");var duration=data.FindProperty("chestPerkSeconds");
            if(!enabled.boolValue || duration.intValue!=PlayerPerks.DefaultDurationSeconds)
            {
                enabled.boolValue=true;duration.intValue=PlayerPerks.DefaultDurationSeconds;
                data.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
                EditorSceneManager.MarkSceneDirty(chest.gameObject.scene);
            }
            count++;
        }
        Directory.CreateDirectory("output/BuriedChests");
        File.WriteAllText("output/BuriedChests/rewards.txt",$"Enabled randomized cash / upgrade / 300-second perks on {count} chests. Placement preserved.");
    }
    [MenuItem("Tools/Grain/Texture chests and connect opening effects")]
    public static void ApplyPresentation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        int count=0;
        var effect=AssetDatabase.LoadAssetAtPath<DiscoveryBurst>("Assets/NewMap/Discoveries/Chest treasure burst.prefab");
        var report=new System.Text.StringBuilder();
        foreach(var chest in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(chest.FindKind!=BuriedFind.Kind.CashChest || chest.gameObject.scene!=SceneManager.GetActiveScene())continue;
            string label=chest.RequiredChestKeyId.Contains("iron")?"Iron":"Copper";
            var material=ChestMaterial(label);
            foreach(var renderer in chest.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is ParticleSystemRenderer)continue;
                Undo.RecordObject(renderer,"Texture buried chest");renderer.sharedMaterial=material;
                EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var data=new SerializedObject(chest);
            data.FindProperty("pickupEffect").objectReferenceValue=effect;
            data.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
            bool lid=data.FindProperty("lidPivot").objectReferenceValue!=null;
            bool textured=material.GetTexture("_BaseColorMap")!=null;
            report.AppendLine($"{(lid && textured && effect!=null?"PASS":"FAIL")} {chest.name}: texture={textured}, animated lid={lid}, treasure VFX={effect!=null}, shared E interaction.");
            EditorSceneManager.MarkSceneDirty(chest.gameObject.scene);count++;
        }
        Directory.CreateDirectory("output/BuriedChests");File.WriteAllText("output/BuriedChests/presentation.txt",report.ToString());
    }
    private static Material ChestMaterial(string label)
    {
        const string folder="Assets/NewMap/Discoveries";
        string path=folder+"/"+label+" matching chest.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(Shader.Find("HDRP/Lit")){name=label+" matching chest"};
            AssetDatabase.CreateAsset(material,path);
        }
        // These meshes were authored against the items1 atlas; unrelated chest textures have different UVs.
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Models New/materials/items1.mat");
        if(source!=null)
        {
            material.SetTexture("_BaseColorMap",source.GetTexture("_BaseColorMap"));
            material.SetTextureScale("_BaseColorMap",source.GetTextureScale("_BaseColorMap"));
            material.SetTextureOffset("_BaseColorMap",source.GetTextureOffset("_BaseColorMap"));
        }
        material.SetColor("_BaseColor",label=="Copper"?new Color(1f,.68f,.43f):new Color(.68f,.79f,.9f));
        material.SetFloat("_Metallic",label=="Copper"?.55f:.75f);material.SetFloat("_Smoothness",.32f);
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
    }
    private static void EnsureGeometry(BuriedFind chest,Material tint)
    {
        if(chest.GetComponentsInChildren<Renderer>(true).Length>0)return;
        var body=Part("Chest body",chest.transform,new Vector3(0,.275f,0),new Vector3(1,.55f,.65f),tint);
        var hinge=new GameObject("Chest lid pivot").transform;hinge.SetParent(chest.transform,false);hinge.localPosition=new Vector3(0,.55f,.325f);
        Part("Chest lid",hinge,new Vector3(0,.08f,-.325f),new Vector3(1.04f,.16f,.7f),tint);
        var socket=new GameObject("Chest key socket").transform;socket.SetParent(chest.transform,false);socket.localPosition=new Vector3(0,.46f,-.36f);socket.localRotation=Quaternion.Euler(0,180,0);
        var collider=chest.GetComponent<BoxCollider>();collider.center=new Vector3(0,.35f,0);collider.size=new Vector3(1.04f,.7f,.7f);
        var data=new SerializedObject(chest);data.FindProperty("lidPivot").objectReferenceValue=hinge;data.FindProperty("keySocket").objectReferenceValue=socket;
        data.FindProperty("insertionKeyPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NewMap/Discoveries/Chest insertion key.prefab");data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;return part;
    }
}
