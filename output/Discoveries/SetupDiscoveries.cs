using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

public static class SetupDiscoveries
{
    const string Folder="Assets/NewMap/Discoveries";
    static void Set(UnityEngine.Object obj,string field,UnityEngine.Object value){var s=new SerializedObject(obj);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Int(UnityEngine.Object obj,string field,int value){var s=new SerializedObject(obj);s.FindProperty(field).intValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Str(UnityEngine.Object obj,string field,string value){var s=new SerializedObject(obj);s.FindProperty(field).stringValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static Material gold,blue,white,dark;
    public static string Run()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Directory.CreateDirectory("output/Discoveries");
        if(!File.Exists("output/Discoveries/Before.unity"))EditorSceneManager.SaveScene(scene,"output/Discoveries/Before.unity",true);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        gold=Mat("Discovery brass",new Color(1,.64f,.13f),false);
        blue=Mat("Hangar key blue",new Color(.025f,.35f,.7f),false);
        white=Mat("Silver key",new Color(.8f,.86f,.9f),false);
        dark=Mat("Keyhole black",new Color(.015f,.02f,.03f),false);
        var goldBurst=Burst("Golden discovery",new Color(1,.65f,.08f),34);
        var blueBurst=Burst("Hangar discovery",new Color(.15f,.75f,1),30);
        var greenBurst=Burst("Stamina discovery",new Color(.2f,1,.65f),28);
        var chestBurst=Burst("Chest treasure burst",new Color(1,.7f,.16f),70);
        var keyModel=KeyModel(gold);
        var keyPrefab=PrefabUtility.SaveAsPrefabAsset(keyModel,Folder+"/Chest insertion key.prefab");UnityEngine.Object.DestroyImmediate(keyModel);
        var finds=UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var f in finds)
        {
            Undo.RecordObject(f,"Discovery presentation");
            Set(f,"pickupEffect",f.FindKind==BuriedFind.Kind.InfiniteStamina?greenBurst:f.FindKind==BuriedFind.Kind.CashChest?chestBurst:goldBurst);
            if(f.FindKind!=BuriedFind.Kind.CashChest)continue;
            var bounds=f.GetComponent<BoxCollider>().bounds;
            Transform socket=f.transform.Find("Chest lock socket");
            if(socket==null)
            {
                socket=new GameObject("Chest lock socket").transform;socket.SetParent(f.transform,true);
                socket.position=new Vector3(bounds.center.x,bounds.center.y+.15f,bounds.max.z+.015f);socket.rotation=Quaternion.identity;
                socket.localScale=new Vector3(1/f.transform.lossyScale.x,1/f.transform.lossyScale.y,1/f.transform.lossyScale.z);
                Cube(socket,"Brass lock plate",Vector3.zero,new Vector3(.17f,.22f,.025f),gold);
                Cube(socket,"Keyhole",new Vector3(0,0,.016f),new Vector3(.025f,.07f,.01f),dark);
            }
            Set(f,"keySocket",socket);Set(f,"insertionKeyPrefab",keyPrefab);
        }
        var volumes=UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var staminaVolume=volumes.Single(v=>v.name=="Global Volume Infinite Stamina");
        Undo.RecordObject(staminaVolume,"Blend stamina volume");staminaVolume.weight=0;staminaVolume.priority=volumes.Where(v=>v!=staminaVolume).Max(v=>v.priority)+10;
        staminaVolume.gameObject.SetActive(true);
        var graphics=GameObject.Find("Graphics");var blend=graphics.GetComponent<InfiniteStaminaVolume>()??Undo.AddComponent<InfiniteStaminaVolume>(graphics);Set(blend,"staminaVolume",staminaVolume);
        var hangars=UnityEngine.Object.FindObjectsByType<HangarGrainVisibility>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var findParent=GameObject.Find("New_Map/15_Buried_Finds").transform;
        for(int n=2;n<=5;n++)
        {
            var target=hangars.Single(h=>h.name.StartsWith("Hangar "+n+" "));
            var source=n==2?hangars.Single(h=>h.name=="01_Modular_Hangar"):hangars.Single(h=>h.name.StartsWith("Hangar "+(n-1)+" "));
            string keyName="Blue key - HANGAR "+n;
            var existing=findParent.Find(keyName);
            if(existing==null)
            {
                var key=new GameObject(keyName);Undo.RegisterCreatedObjectUndo(key,"Buried hangar key");key.transform.SetParent(findParent,false);
                var pile=source.GrainRoot.GetComponent<GrainPile>();
                Vector3 p=pile.transform.position+source.transform.right*.9f;
                float surface=pile.SurfaceHeight(p);p.y=Mathf.Lerp(pile.transform.position.y,surface,.42f);
                key.transform.position=p;key.transform.rotation=Quaternion.LookRotation(-source.transform.forward,Vector3.up);
                var metal=KeyModel(white);metal.name="Industrial silver key";metal.transform.SetParent(key.transform,false);metal.transform.localRotation=Quaternion.Euler(90,0,0);metal.transform.localScale=Vector3.one*2;
                Cube(key.transform,"Blue hangar tag",new Vector3(0,.7f,0),new Vector3(.43f,.34f,.045f),blue);
                var roof=Cube(key.transform,"Barn roof symbol",new Vector3(0,.91f,0),new Vector3(.31f,.31f,.043f),blue);roof.transform.localRotation=Quaternion.Euler(0,0,45);
                Cube(key.transform,"Barn doorway icon",new Vector3(0,.95f,-.03f),new Vector3(.085f,.11f,.015f),white);
                Label(key.transform,"Destination label",new Vector3(0,.67f,-.028f),new Vector2(.42f,.26f),.65f,"HANGAR\n<size=170%>"+n+"</size>");
                var col=key.AddComponent<BoxCollider>();col.center=new Vector3(0,.5f,0);col.size=new Vector3(.48f,1.05f,.14f);
                var f=key.AddComponent<BuriedFind>();Int(f,"kind",(int)BuriedFind.Kind.HangarKey);Int(f,"targetHangar",n);Str(f,"findId",HangarKeyDoor.KeyFor(n));Set(f,"coveringPile",pile);Set(f,"pickupEffect",blueBurst);
                var state=new SerializedObject(f);state.FindProperty("settleOnReveal").boolValue=true;state.ApplyModifiedPropertiesWithoutUndo();
            }
            var left=target.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Left door - slide outward to open");
            var right=target.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Right door - slide outward to open");
            var existingDoor=target.GetComponentInChildren<HangarKeyDoor>(true);
            var plaque=existingDoor!=null?existingDoor.transform:target.transform.Find("Hangar key lock");
            if(plaque==null)
            {
                plaque=new GameObject("Hangar key lock").transform;plaque.SetParent(target.transform,true);
                plaque.position=(left.position+right.position)*.5f+target.transform.forward*.7f+Vector3.up*1.65f;
                plaque.rotation=target.transform.rotation;
                plaque.localScale=new Vector3(1/target.transform.lossyScale.x,1/target.transform.lossyScale.y,1/target.transform.lossyScale.z);
                Cube(plaque,"Numbered blue lock",Vector3.zero,new Vector3(1,.52f,.12f),blue);
                var label=Label(plaque,"Lock label",new Vector3(0,0,.07f),new Vector2(.95f,.45f),.65f,"HANGAR "+n+"\nBLUE KEY REQUIRED");label.transform.localRotation=Quaternion.Euler(0,180,0);
                var collider=plaque.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(1.05f,.6f,.2f);
                var door=plaque.gameObject.AddComponent<HangarKeyDoor>();Int(door,"hangarNumber",n);Set(door,"grainVisibility",target);Set(door,"leftDoor",left);Set(door,"rightDoor",right);Set(door,"label",label);Set(door,"unlockEffect",blueBurst);
                var overhead=target.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="LOCKED");Set(door,"overheadLabel",overhead);
                plaque.SetParent(left,true);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        return "Wired 16 discoveries, 4 numbered hangar keys and doors, authored stamina-volume blend.";
    }
    static Material Mat(string name,Color color,bool glow)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(glow?"HDRP/Unlit":"HDRP/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor(glow?"_UnlitColor":"_BaseColor",color);if(glow)m.SetColor("_EmissiveColor",color*3);else{m.SetFloat("_Metallic",.55f);m.SetFloat("_Smoothness",.45f);}EditorUtility.SetDirty(m);return m;
    }
    static GameObject Cube(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static TextMeshPro Label(Transform parent,string name,Vector3 pos,Vector2 size,float font,string text)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;var t=g.AddComponent<TextMeshPro>();t.text=text;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;t.color=Color.white;return t;
    }
    static GameObject KeyModel(Material material)
    {
        var g=new GameObject("Insertion key");
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI*2/16;var segment=Cube(g.transform,"Key bow",new Vector3(Mathf.Cos(a)*.065f,0,-.28f+Mathf.Sin(a)*.065f),new Vector3(.03f,.022f,.026f),material);
            segment.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
        }
        Cube(g.transform,"Key shaft",new Vector3(0,0,-.105f),new Vector3(.025f,.025f,.22f),material);
        Cube(g.transform,"Tooth",new Vector3(.028f,0,-.035f),new Vector3(.06f,.025f,.025f),material);
        Cube(g.transform,"Tooth",new Vector3(.028f,0,-.075f),new Vector3(.06f,.025f,.025f),material);return g;
    }
    static DiscoveryBurst Burst(string name,Color color,int count)
    {
        var g=new GameObject(name);var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.duration=1.5f;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.7f);main.startSpeed=new ParticleSystem.MinMaxCurve(.8f,2.5f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.07f);main.gravityModifier=.25f;main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=true;
        var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.16f;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
        var rend=g.GetComponent<ParticleSystemRenderer>();rend.renderMode=ParticleSystemRenderMode.Mesh;
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);rend.mesh=cube.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(cube);rend.sharedMaterial=Mat(name+" glow",color,true);rend.shadowCastingMode=ShadowCastingMode.Off;
        var caption=Label(g.transform,"Reward caption",Vector3.up*.3f,new Vector2(3,.7f),3,"DISCOVERED");caption.color=color;
        var burst=g.AddComponent<DiscoveryBurst>();Set(burst,"caption",caption);
        var prefab=PrefabUtility.SaveAsPrefabAsset(g,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(g);return prefab.GetComponent<DiscoveryBurst>();
    }
}
