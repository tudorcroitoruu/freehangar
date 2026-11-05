using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authors stable, scattered discoveries once; repeated imports preserve their positions.</summary>
public static class PerkTreasureSetup
{
    private const string GroupName="Scattered treasure and perk bottles";
    private const string Folder="Assets/NewMap/Discoveries";
    [InitializeOnLoadMethod] private static void AfterImport() => EditorApplication.delayCall+=()=>
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name=="Gameplay")Apply();
    };
    [MenuItem("Tools/Grain/Add scattered chests and perk bottles")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")return;
        var finds=new List<BuriedFind>();
        foreach(var find in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(find.gameObject.scene==scene)finds.Add(find);
        var bottle=finds.Find(f=>f.FindKind==BuriedFind.Kind.InfiniteStamina);
        var copper=finds.Find(f=>f.FindKind==BuriedFind.Kind.CashChest && f.RequiredChestKeyId.Contains("copper"));
        var iron=finds.Find(f=>f.FindKind==BuriedFind.Kind.CashChest && f.RequiredChestKeyId.Contains("iron"));
        var copperKey=finds.Find(f=>f.FindKind==BuriedFind.Kind.Key && f.FindId.Contains("copper"));
        var ironKey=finds.Find(f=>f.FindKind==BuriedFind.Kind.Key && f.FindId.Contains("iron"));
        if(bottle==null || copper==null || iron==null || copperKey==null || ironKey==null){Debug.LogWarning("Treasure setup needs the existing bottle and copper/iron discoveries.");return;}
        Transform group=GameObject.Find("New_Map/Collectables/Perks")?.transform;foreach(var root in scene.GetRootGameObjects())if(root.name==GroupName)group=root.transform;
        if(group==null){var host=new GameObject(GroupName);SceneManager.MoveGameObjectToScene(host,scene);Undo.RegisterCreatedObjectUndo(host,"Add scattered discoveries");group=host.transform;}
        var random=new System.Random(731129);var report=new System.Text.StringBuilder();bool changed=false;
        var rewards=new[]{ChestReward.InfiniteStamina,ChestReward.Mobility,ChestReward.NoAttacks,ChestReward.DoubleSellCash};
        var names=new[]{"Stamina","Mobility","Protection","Double sell cash"};
        var colors=new[]{new Color(.2f,1f,.45f),new Color(.15f,.65f,1f),new Color(.75f,.35f,1f),new Color(1f,.72f,.12f)};
        for(int i=0;i<4;i++)
        {
            int hangar=2+i;string label=i%2==0?"Copper":"Iron";
            var chestTemplate=i%2==0?copper:iron;var keyTemplate=i%2==0?copperKey:ironKey;
            var pile=VehicleKeyBurial.FindPile(scene,hangar);
            if(pile==null){report.AppendLine("FAIL missing Hangar "+hangar+" pile");continue;}
            string keyId="farm.key."+label.ToLowerInvariant()+".bonus."+(i+1);
            string chestId="farm.chest.bonus."+(i+1);
            var key=Ensure(keyTemplate,keyId,label+" bonus key - HANGAR "+hangar,pile,group,random,finds,ref changed);
            if(key==null){report.AppendLine("FAIL could not place "+keyId);continue;}
            key.ConfigureChestKey(keyId,label);
            var chest=Ensure(chestTemplate,chestId,label+" bonus chest - HANGAR "+hangar,pile,group,random,finds,ref changed);
            if(chest!=null)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+label+" matching chest.mat");
                chest.ConfigureMatchingChest(chestId,keyId,label,pile,material);EditorUtility.SetDirty(chest);
                report.AppendLine($"PASS {chestId} in Hangar {hangar}, matching key {keyId} at {key.transform.position}, chest at {chest.transform.position}");
            }
            string bottleId="farm.perk.bottle."+rewards[i].ToString().ToLowerInvariant();
            bool existed=finds.Exists(f=>f.FindId==bottleId);
            var tonic=Ensure(bottle,bottleId,names[i]+" bottle - 5 minutes - HANGAR "+hangar,pile,group,random,finds,ref changed);
            if(tonic==null)continue;
            if(!existed)
            {
                tonic.ConfigurePerkBottle(bottleId,rewards[i],pile);
                // Wide stamina flask, tall mobility vial, broad protection flask and compact gold bottle.
                var shapes=new[]{new Vector3(1.12f,.9f,1.12f),new Vector3(.82f,1.18f,.82f),new Vector3(1.25f,.85f,1.25f),new Vector3(.95f,.78f,.95f)};
                tonic.transform.localScale=Vector3.Scale(tonic.transform.localScale,shapes[i]);
                var material=BottleMaterial(names[i],colors[i]);
                foreach(var renderer in tonic.GetComponentsInChildren<Renderer>(true))if(!(renderer is ParticleSystemRenderer))renderer.sharedMaterial=material;
                var data=new SerializedObject(tonic);data.FindProperty("pickupEffect").objectReferenceValue=BottleEffect(names[i],colors[i]);data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tonic);
            }
            report.AppendLine($"PASS {names[i]} bottle: {bottleId}, Hangar {hangar}, shared E prompt, drinking animation and colored stamina-style burst.");
        }
        if(changed)EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory("output/BuriedChests");File.WriteAllText("output/BuriedChests/scattered-treasure.txt",report.ToString());
        SceneView.RepaintAll();
    }
    private static BuriedFind Ensure(BuriedFind template,string id,string name,GrainPile pile,Transform group,System.Random random,List<BuriedFind> finds,ref bool changed)
    {
        var existing=finds.Find(f=>f.FindId==id);if(existing!=null)return existing;
        Bounds bounds=new Bounds(template.transform.position,Vector3.zero);bool first=true;
        foreach(var renderer in template.GetComponentsInChildren<Renderer>(true)){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
        pile.Initialize();Vector3 point=default;bool placed=false;
        for(int attempt=0;attempt<250;attempt++)
        {
            point=pile.transform.TransformPoint(new Vector3(((float)random.NextDouble()-.5f)*pile.Size*.85f,0,((float)random.NextDouble()-.5f)*pile.Size*.85f));
            if(!pile.IsInsideBoundary(point,.8f) || pile.SurfaceHeight(point)-point.y<bounds.size.y+.35f)continue;
            bool crowded=false;foreach(var find in finds)if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(find.transform.position.x,find.transform.position.z))<2.2f){crowded=true;break;}
            if(crowded)continue;
            point.y+=.12f-(bounds.min.y-template.transform.position.y);placed=true;break;
        }
        if(!placed){Debug.LogError("No safely buried, separated position for "+name);return null;}
        var clone=UnityEngine.Object.Instantiate(template.gameObject,group);clone.name=name;
        Undo.RegisterCreatedObjectUndo(clone,"Scatter buried discovery");clone.transform.SetPositionAndRotation(point,Quaternion.Euler(0,(float)random.NextDouble()*360f,0));
        clone.SetActive(true);var result=clone.GetComponent<BuriedFind>();
        var data=new SerializedObject(result);data.FindProperty("findId").stringValue=id;data.FindProperty("coveringPile").objectReferenceValue=pile;data.ApplyModifiedPropertiesWithoutUndo();
        finds.Add(result);changed=true;return result;
    }
    private static Material BottleMaterial(string name,Color color)
    {
        string path=Folder+"/"+name+" perk bottle.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(Shader.Find("HDRP/Lit")){name=name+" perk bottle"};material.SetColor("_BaseColor",color);
        material.SetFloat("_Metallic",.25f);material.SetFloat("_Smoothness",.65f);AssetDatabase.CreateAsset(material,path);return material;
    }
    private static DiscoveryBurst BottleEffect(string name,Color color)
    {
        string path=Folder+"/"+name+" bottle discovery.prefab";var existing=AssetDatabase.LoadAssetAtPath<DiscoveryBurst>(path);if(existing!=null)return existing;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Stamina discovery.prefab");
        var clone=UnityEngine.Object.Instantiate(source);clone.name=name+" bottle discovery";
        var sourceMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Stamina discovery glow.mat");
        string materialPath=Folder+"/"+name+" bottle glow.mat";var glow=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(glow==null){glow=new Material(sourceMaterial){name=name+" bottle glow"};glow.SetColor("_UnlitColor",color);glow.SetColor("_EmissiveColor",color*3);AssetDatabase.CreateAsset(glow,materialPath);}
        foreach(var particles in clone.GetComponentsInChildren<ParticleSystem>(true)){var main=particles.main;main.startColor=color;particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=glow;}
        var prefab=PrefabUtility.SaveAsPrefabAsset(clone,path);UnityEngine.Object.DestroyImmediate(clone);return prefab.GetComponent<DiscoveryBurst>();
    }
}
