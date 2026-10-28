using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class SetupIntroduction
{
    const string Folder="Assets/NewMap/Introduction";
    static void Set(UnityEngine.Object o,string field,UnityEngine.Object v){var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
    public static string Run()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();Directory.CreateDirectory("output/Intro");
        if(!File.Exists("output/Intro/Before.unity"))EditorSceneManager.SaveScene(scene,"output/Intro/Before.unity",true);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var manager=UnityEngine.Object.FindFirstObjectByType<GameManager>();
        var intro=manager.GetComponent<GameIntroduction>()??Undo.AddComponent<GameIntroduction>(manager.gameObject);Set(manager,"introduction",intro);
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();Set(intro,"player",player);
        Set(intro,"bicycle",UnityEngine.Object.FindObjectsByType<BicycleController>(FindObjectsSortMode.None).First(b=>!b.motorDriven));
        var root=GameObject.Find("Introduction staging")??new GameObject("Introduction staging");
        var start=Marker(root.transform,"01 Panorama start",new Vector3(-130,17,165),new Vector3(-90,15,150));
        var end=Marker(root.transform,"02 Panorama end",new Vector3(-130,17,161),new Vector3(-90,15,146));
        var arrival=Marker(root.transform,"03 First hangar arrival",new Vector3(0,.06f,18),new Vector3(0,.06f,0));
        var grain=Marker(root.transform,"04 Grain on the left",new Vector3(8,1.7f,4),Vector3.zero);
        var shovel=Marker(root.transform,"05 First shovel on the right",new Vector3(-2.1f,.8f,15.6f),Vector3.zero);
        Set(intro,"landscapeStart",start);Set(intro,"landscapeEnd",end);Set(intro,"arrival",arrival);Set(intro,"grainLook",grain);Set(intro,"shovelLook",shovel);
        var oldProp=root.transform.Find("First shovel introduction prop");if(oldProp!=null)UnityEngine.Object.DestroyImmediate(oldProp.gameObject);
        var filter=player.HeldInstrument.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Shovel_Grain_Textured");
        var prop=new GameObject("First shovel introduction prop",typeof(MeshFilter),typeof(MeshRenderer));prop.transform.SetParent(root.transform,false);prop.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;prop.GetComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;
        prop.transform.position=new Vector3(-2.1f,1.2f,15.6f);prop.transform.rotation=Quaternion.Euler(75,30,0);prop.SetActive(false);Set(intro,"worldShovel",prop);
        var points=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(line=>line.Split(',')).Select(v=>new Vector3(float.Parse(v[0],CultureInfo.InvariantCulture),0,float.Parse(v[2],CultureInfo.InvariantCulture))).ToArray();
        int nearest=Enumerable.Range(0,points.Length).OrderBy(i=>(points[i]-new Vector3(47.46f,0,95.02f)).sqrMagnitude).First();
        var route=new List<Vector3>();var terrain=Terrain.activeTerrain;
        for(int i=nearest;i>=0;i--){if(points[i].z<24)break;Vector3 p=points[i];p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.04f;route.Add(p);}
        var serialized=new SerializedObject(intro);var array=serialized.FindProperty("bicycleRoute");array.arraySize=route.Count;for(int i=0;i<route.Count;i++)array.GetArrayElementAtIndex(i).vector3Value=route[i];serialized.ApplyModifiedPropertiesWithoutUndo();
        var birds=GameObject.Find("Audio Game").GetComponent<AudioSource>();Set(intro,"birds",birds);
        Set(intro,"purchasePaperPrefab",Paper());
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        return "Introduction wired; "+route.Count+" ground-fitted bicycle route samples; skipTutorial defaults false.";
    }
    static Transform Marker(Transform parent,string name,Vector3 position,Vector3 target)
    {
        var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.position=position;t.rotation=Quaternion.LookRotation(target-position,Vector3.up);return t;
    }
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("HDRP/Unlit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_UnlitColor",color);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static void Label(Transform parent,string name,Vector3 pos,Vector2 size,float font,string text,Color color)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;var t=g.AddComponent<TextMeshPro>();t.text=text;t.color=color;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;
    }
    static GameObject Paper()
    {
        var cream=Mat("Warm purchase paper",new Color(.86f,.79f,.61f));var ink=Mat("Faded ink",new Color(.16f,.18f,.19f));var red=Mat("Sale stamp ink",new Color(.55f,.055f,.04f));
        var root=new GameObject("Hangar purchase deed");Box(root.transform,"Paper",Vector3.zero,new Vector3(.42f,.57f,.002f),cream);
        Label(root.transform,"Ownership heading",new Vector3(0,.235f,-.003f),new Vector2(.38f,.05f),.22f,"TRANSFER OF OWNERSHIP",new Color(.16f,.18f,.19f));
        Label(root.transform,"Property heading",new Vector3(0,.2f,-.003f),new Vector2(.36f,.04f),.18f,"FARM ESTATE / HANGARS 1-5",new Color(.16f,.18f,.19f));
        var stamp=new GameObject("SOLD OUT stamp").transform;stamp.SetParent(root.transform,false);stamp.localPosition=new Vector3(.015f,-.1f,-.004f);stamp.localRotation=Quaternion.Euler(0,0,-9);
        Label(stamp,"Sale stamp",new Vector3(0,0,-.002f),new Vector2(.35f,.09f),.65f,"SOLD OUT",new Color(.55f,.055f,.04f));
        Box(stamp,"Stamp top",new Vector3(0,.052f,0),new Vector3(.36f,.004f,.001f),red);Box(stamp,"Stamp bottom",new Vector3(0,-.052f,0),new Vector3(.36f,.004f,.001f),red);
        Box(stamp,"Stamp left",new Vector3(-.18f,0,0),new Vector3(.004f,.105f,.001f),red);Box(stamp,"Stamp right",new Vector3(.18f,0,0),new Vector3(.004f,.105f,.001f),red);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int row=0;row<10;row++)
        {
            float y=row<7?.15f-row*.027f:-.19f-(row-7)*.022f;
            for(int i=0;i<100;i++)
            {
                float x=-.16f+i*.0031f;float yy=y+.0035f*Mathf.Sin(i*.72f+row)+.0018f*Mathf.Sin(i*1.7f+row*3);
                int k=vertices.Count;vertices.Add(new Vector3(x,yy-.0006f,-.003f));vertices.Add(new Vector3(x,yy+.0006f,-.003f));
                if(i>0){triangles.Add(k-2);triangles.Add(k+1);triangles.Add(k);triangles.Add(k-2);triangles.Add(k-1);triangles.Add(k+1);}
            }
        }
        var mesh=new Mesh{name="Illegible cursive deed strokes"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string meshPath=Folder+"/Deed handwriting.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
        var strokes=new GameObject("Handwritten purchase details",typeof(MeshFilter),typeof(MeshRenderer));strokes.transform.SetParent(root.transform,false);strokes.GetComponent<MeshFilter>().sharedMesh=mesh;strokes.GetComponent<MeshRenderer>().sharedMaterial=ink;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Purchase deed.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
}

