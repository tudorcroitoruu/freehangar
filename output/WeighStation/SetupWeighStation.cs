using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class SetupWeighStation
{
    const string Folder = "Assets/NewMap/WeighStation";
    public static string Run()
    {
        if (Application.isPlaying) throw new Exception("Edit Mode required");
        var root = GameObject.Find("New_Map/Weigh Station");
        if (root == null) throw new Exception("Station not found");
        // Preserve all current user edits before adding the station feature.
        Directory.CreateDirectory("output/WeighStation");
        if (!File.Exists("output/WeighStation/Before.unity")) EditorSceneManager.SaveScene(root.scene, "output/WeighStation/Before.unity", true);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var platform = root.transform.Find("Weighbridge platform").GetComponent<Renderer>();
        var old = root.transform.Find("Selling scale");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var scale = new GameObject("Selling scale");
        Undo.RegisterCreatedObjectUndo(scale, "Improve weigh station");
        scale.transform.SetParent(root.transform, true);
        scale.transform.position = platform.bounds.center + Vector3.up * platform.bounds.extents.y;
        scale.transform.rotation = root.transform.rotation;
        var steel = Material("Embossed steel", new Color(.28f,.34f,.35f), .65f, true);
        var roof = Material("Corrugated painted steel", new Color(.18f,.27f,.29f), .35f, false);
        var yellow = Material("Scale safety yellow", new Color(.98f,.65f,.06f), .1f, false);
        var dark = Material("Scale charcoal", new Color(.035f,.055f,.06f), .1f, false);
        var trim = Material("Edge steel", new Color(.45f,.48f,.46f), .7f, false);
        Undo.RecordObject(platform,"Scale surface"); platform.sharedMaterial = roof;
        foreach (Transform child in root.transform)
        {
            if (child.name == "Canopy" || child.name == "Canopy pillar")
            { var r=child.GetComponent<Renderer>(); Undo.RecordObject(r,"Steel finish"); r.sharedMaterial=roof; }
        }
        Cube(scale.transform,"Inset weighing deck",new Vector3(0,.045f,0),new Vector3(3.8f,.09f,4.8f),steel,true);
        for(int side=-1;side<=1;side+=2)
        {
            Cube(scale.transform,"Yellow drop boundary",new Vector3(side*2,.035f,0),new Vector3(.2f,.07f,5.2f),yellow);
            Cube(scale.transform,"Yellow drop boundary",new Vector3(0,.035f,side*2.5f),new Vector3(4.2f,.07f,.2f),yellow);
            for(int i=0;i<10;i++)
            {
                var stripe=Cube(scale.transform,"Hazard tread",new Vector3(side*2,.075f,-2.25f+i*.5f),new Vector3(.21f,.012f,.18f),dark);
                stripe.transform.localRotation=Quaternion.Euler(0,25,0);
            }
            // Inward arrows point to the actual receiving area from both approaches.
            Cube(scale.transform,"Drop arrow stem",new Vector3(0,.025f,side*4),new Vector3(.18f,.025f,1),yellow);
            for(int arm=-1;arm<=1;arm+=2)
            {
                var arrow=Cube(scale.transform,"Drop arrow tip",new Vector3(arm*.23f,.025f,side*3.55f),new Vector3(.16f,.025f,.65f),yellow);
                arrow.transform.localRotation=Quaternion.Euler(0,side*arm*45,0);
            }
        }
        for(int x=-1;x<=1;x+=2) for(int z=-2;z<=2;z++)
            Cube(scale.transform,"Deck fastener",new Vector3(x*1.7f,.098f,z),new Vector3(.06f,.014f,.06f),trim);
        // Shallow ribs add readable highlights to the roof, without another texture dependency.
        for(int i=0;i<22;i++) Cube(scale.transform,"Roof seam",new Vector3(-3.85f+i*.365f,5.43f,0),new Vector3(.045f,.045f,12.9f),trim);
        Cube(scale.transform,"Instructions board",new Vector3(0,2.3f,5.88f),new Vector3(4.7f,1.05f,.12f),dark);
        for(int side=-1;side<=1;side+=2) Cube(scale.transform,"Display support",new Vector3(side*2,1.1f,5.88f),new Vector3(.08f,2.2f,.08f),trim);
        var text = Label(scale.transform,"Scale status",new Vector3(0,2.3f,5.8f),new Vector2(4.5f,.9f),2.9f,
            "DROP FILLED BAGS / BOXES\nCenter only • step off to sell");
        Label(scale.transform,"Deck instruction",new Vector3(0,.105f,0),new Vector2(3.6f,1.5f),4.5f,"DROP HERE\nBAGS + BOXES").transform.localRotation=Quaternion.Euler(90,0,0);
        var station=scale.AddComponent<GrainWeighStation>();
        var serialized=new SerializedObject(station); serialized.FindProperty("statusText").objectReferenceValue=text; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(root.scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=scale;
        return "Station authored and saved; original scene snapshot in output/WeighStation/Before.unity";
    }
    static Material Material(string name, Color color,float metal,bool tread)
    {
        string path=Folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("HDRP/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.32f);
        if(tread)
        {
            var tex=new Texture2D(256,256,TextureFormat.RGB24,false);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                int xx=x%32, yy=y%32;
                bool raised=Mathf.Abs(xx-yy)<3 && xx>7 && xx<25;
                float v=raised?.85f:.54f+Mathf.PerlinNoise(x*.18f,y*.18f)*.09f;
                tex.SetPixel(x,y,new Color(v,v,v));
            }
            tex.Apply(); File.WriteAllBytes(Folder+"/SteelTread.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Folder+"/SteelTread.png");mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/SteelTread.png"));
            mat.SetTextureScale("_BaseColorMap",new Vector2(3,4));
        }
        else { mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NewMap/Textures/TFP_Noise_1.png"));mat.SetTextureScale("_BaseColorMap",new Vector2(3,3)); }
        EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Cube(Transform parent,string name,Vector3 pos,Vector3 size,Material material,bool solid=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static TextMeshPro Label(Transform parent,string name,Vector3 pos,Vector2 size,float font,string message)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;
        var t=g.AddComponent<TextMeshPro>();t.text=message;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;t.color=new Color(1,.88f,.45f);return t;
    }
}
