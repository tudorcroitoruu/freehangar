using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// Run in the connected Editor with: unity command script run --file AgentScripts/NewMapBuilder.cs --entry NewMapBuilder.Build
// All generated assets are isolated; imported prefabs/materials are never edited.
public static class NewMapBuilder
{
    const string A = "Assets/NewMap";
    static Transform root, hangar, dressing, landscape;
    static Dictionary<string,string> paths;
    static Material timber, boards, dark, gold, metal, floor, cream;
    static List<Vector3> road;
    static System.Random rng;
    static float R(float a,float b) => a+(float)rng.NextDouble()*(b-a);
    static GameObject Group(string name,Transform parent) {var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
    static Material Mat(string name, Color c, float smooth=.2f) {
        string p=A+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("HDRP/Lit"));AssetDatabase.CreateAsset(m,p);}
        m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static GameObject Prefab(string name,Transform parent,Vector3 pos,float yaw=0,Vector3? scale=null) {
        if(!paths.TryGetValue(name,out string p))throw new Exception("Missing prefab "+name);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p),parent);
        g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);g.transform.localScale=scale??Vector3.one;return g;
    }
    static void Paint(GameObject g,Material m) {foreach(var r in g.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>m).ToArray();}
    static GameObject Box(string n,Transform p,Vector3 pos,Vector3 scale,Material m,bool collider=true) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static void Beam(string name,Transform p,Vector3 a,Vector3 b,float width,Material m) {
        var g=Box(name,p,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),m);
        g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static void Text(string text,Transform p,Vector3 pos,float size,Color color,float yaw=0) {
        var g=Group(text.Replace("\n"," "),p);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var tm=g.AddComponent<TMPro.TextMeshPro>();tm.text=text;tm.fontSize=size;tm.color=color;tm.alignment=TMPro.TextAlignmentOptions.Center;tm.rectTransform.sizeDelta=new Vector2(7,2);tm.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
    }
    static void Sign(string text,Transform p,Vector3 pos,Vector2 size,float yaw=0) {
        var g=Group("Sign - "+text.Replace("\n"," "),p);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
        Box("Timber frame",g.transform,Vector3.zero,new Vector3(size.x+.12f,size.y+.12f,.12f),timber);
        Box("Painted panel",g.transform,new Vector3(0,0,-.072f),new Vector3(size.x,size.y,.035f),dark);
        Text(text,g.transform,new Vector3(0,0,-.1f),size.y*.65f,new Color(.94f,.84f,.63f));
    }
    public static string Build() {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Gameplay must be active.");
        var preview=GameObject.Find("New_Map_AssetPreview");if(preview)Object.DestroyImmediate(preview);
        if(GameObject.Find("New_Map"))throw new Exception("New_Map exists; use targeted refinement instead of replacing user edits.");
        Directory.CreateDirectory("output/NewMap");
        EditorSceneManager.SaveScene(scene);
        File.Copy(scene.path,"output/NewMap/Gameplay.before-new-map.unity",false);
        foreach(var d in new[]{"Materials","Meshes","Terrain","Lighting"})Directory.CreateDirectory(A+"/"+d);
        AssetDatabase.Refresh();
        paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Imported/Toon Series/Toon Farm Pack/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(g=>g.Key,g=>g.First());
        rng=new System.Random(4127);
        root=Group("New_Map",null).transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Create reference farm map");
        hangar=Group("01_Modular_Hangar",root).transform;dressing=Group("02_Hangar_Props",root).transform;landscape=Group("05_Landscape",root).transform;
        timber=Mat("Warm oak structural beams",new Color(.31f,.135f,.047f));
        boards=Mat("Weathered honey timber",new Color(.44f,.28f,.13f));
        dark=Mat("Charcoal sign paint",new Color(.065f,.052f,.039f));
        gold=Mat("Golden maize",new Color(.98f,.55f,.055f),.26f);
        metal=Mat("Aged blue grey steel",new Color(.18f,.25f,.29f),.35f);
        floor=Mat("Warm dusty concrete",new Color(.48f,.36f,.23f),.08f);
        cream=Mat("Canvas beige",new Color(.66f,.53f,.34f),.08f);
        MakeTextures();
        BuildRoad();BuildTerrain();BuildHangar();BuildDressing();BuildYard();BuildVillage();BuildLandscape();BuildLighting();
        var roots=scene.GetRootGameObjects();
        var old=roots.First(g=>g.name=="MAP");Undo.RecordObject(old,"Disable original map");old.SetActive(false);
        var originalPlayer=roots.First(g=>g.name=="Player");
        var player=Object.Instantiate(originalPlayer,root);player.name="Player";player.SetActive(true);
        Undo.RecordObject(originalPlayer,"Preserve original player");originalPlayer.SetActive(false);
        player.transform.position=new Vector3(-1.2f,.13f,-8);player.transform.rotation=Quaternion.Euler(0,3,0);
        var cam=player.GetComponentInChildren<Camera>(true);cam.fieldOfView=58;cam.nearClipPlane=.07f;cam.farClipPlane=650;
        cam.transform.localPosition=new Vector3(0,1.7f,0);cam.transform.localRotation=Quaternion.Euler(-2,0,0);cam.tag="MainCamera";
        var viewpoints=Group("07_Reference_Views",root).transform;
        var inside=Group("Interior - reference 01 - 58 vertical FOV",viewpoints);inside.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);
        var exterior=Group("Exterior - reference 02 - 60 vertical FOV",viewpoints);exterior.transform.position=new Vector3(17,1.75f,27);exterior.transform.rotation=Quaternion.LookRotation(new Vector3(-3,3,12)-exterior.transform.position);
        var route=Group("06_Road_Route",root).transform;
        for(int i=0;i<road.Count;i+=10){var w=Group("Route_"+i.ToString("000"),route);w.transform.position=road[i]+Vector3.up*.1f;}
        float length=0;for(int i=1;i<road.Count;i++)length+=Vector3.Distance(road[i-1],road[i]);
        File.WriteAllText("output/NewMap/layout.txt","Terrain: 384 x 384 m\nRoad centerline: "+length.ToString("F1")+" m\nTravel at 8 m/s: "+(length/8).ToString("F1")+" s\nCamera: 58 vertical FOV; eye height 1.7 m\nOriginal MAP, Graphics, Player retained inactive.\n");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=root.gameObject;
        return "Built New_Map; road "+length.ToString("F1")+" m; "+root.GetComponentsInChildren<Renderer>(true).Length+" renderers.";
    }
    public static string Finish() {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        root=GameObject.Find("New_Map").transform;
        hangar=root.Find("01_Modular_Hangar");
        BuildRoad();BuildLighting();
        var roots=scene.GetRootGameObjects();
        var old=roots.First(g=>g.name=="MAP");Undo.RecordObject(old,"Disable original map");old.SetActive(false);
        var originalPlayer=roots.First(g=>g.name=="Player");
        var player=Object.Instantiate(originalPlayer,root);player.name="Player";player.SetActive(true);
        Undo.RecordObject(originalPlayer,"Preserve original player");originalPlayer.SetActive(false);
        player.transform.position=new Vector3(-1.2f,.13f,-8);player.transform.rotation=Quaternion.Euler(0,3,0);
        var cam=player.GetComponentInChildren<Camera>(true);cam.fieldOfView=58;cam.nearClipPlane=.07f;cam.farClipPlane=650;
        cam.transform.localPosition=new Vector3(0,1.7f,0);cam.transform.localRotation=Quaternion.Euler(-2,0,0);cam.tag="MainCamera";
        var viewpoints=Group("07_Reference_Views",root).transform;
        var inside=Group("Interior - reference 01 - 58 vertical FOV",viewpoints);inside.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);
        var exterior=Group("Exterior - reference 02 - 60 vertical FOV",viewpoints);exterior.transform.position=new Vector3(17,1.75f,27);exterior.transform.rotation=Quaternion.LookRotation(new Vector3(-3,3,12)-exterior.transform.position);
        var route=Group("06_Road_Route",root).transform;
        for(int i=0;i<road.Count;i+=10){var w=Group("Route_"+i.ToString("000"),route);w.transform.position=road[i]+Vector3.up*.1f;}
        float length=0;for(int i=1;i<road.Count;i++)length+=Vector3.Distance(road[i-1],road[i]);
        File.WriteAllText("output/NewMap/layout.txt","Terrain: 384 x 384 m\nRoad centerline: "+length.ToString("F1")+" m\nTravel at 8 m/s: "+(length/8).ToString("F1")+" s\nCamera: 58 vertical FOV; eye height 1.7 m\nOriginal MAP, Graphics, Player retained inactive.\n");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=root.gameObject;
        return "Built New_Map; road "+length.ToString("F1")+" m; "+root.GetComponentsInChildren<Renderer>(true).Length+" renderers.";
    }
    static void MakeTextures(){
        var tex=new Texture2D(256,256,TextureFormat.RGBA32,true);tex.name="Oak grain";
        var pix=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++){
            float f=.76f+.2f*Mathf.PerlinNoise(x*.16f,y*.015f)+.07f*Mathf.Sin(x*.9f+Mathf.Sin(y*.035f));
            pix[y*256+x]=new Color(f,f,f,1);
        }
        tex.SetPixels(pix);tex.Apply();AssetDatabase.CreateAsset(tex,A+"/Materials/OakGrain.asset");
        timber.SetTexture("_BaseColorMap",tex);boards.SetTexture("_BaseColorMap",tex);
        var maize=new Texture2D(256,256,TextureFormat.RGBA32,true);maize.name="Maize kernels tiling";
        for(int y=0;y<256;y++)for(int x=0;x<256;x++){
            float row=Mathf.Floor(y/16f);float xx=Mathf.Repeat(x+row%2*8,16)/8-1;float yy=Mathf.Repeat(y,16)/8-1;
            float e=xx*xx+yy*yy;float v=Mathf.Clamp01(1-e*.43f)+.12f*(-xx+yy);
            pix[y*256+x]=new Color(.6f+.4f*v,.52f+.48f*v,.42f+.58f*v,1);
        }
        maize.SetPixels(pix);maize.Apply();AssetDatabase.CreateAsset(maize,A+"/Materials/MaizeKernelPattern.asset");
        gold.SetTexture("_BaseColorMap",maize);gold.SetTextureScale("_BaseColorMap",new Vector2(7,7));
    }
    static Vector3[] knots=new[]{new Vector3(0,0,15),new Vector3(1,0,35),new Vector3(26,0,58),new Vector3(86,0,91),new Vector3(103,0,135),new Vector3(52,0,165),new Vector3(-40,0,163),new Vector3(-67,0,207),new Vector3(-23,0,238),new Vector3(8,0,250)};
    static void BuildRoad(){
        road=new List<Vector3>();
        for(int i=0;i<knots.Length-1;i++)for(int j=0;j<24;j++){
            float t=j/24f;var a=knots[Math.Max(0,i-1)];var b=knots[i];var c=knots[i+1];var d=knots[Math.Min(knots.Length-1,i+2)];
            var q=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);q.y=RoadY(q.z);road.Add(q);
        }var last=knots.Last();last.y=RoadY(last.z);road.Add(last);
    }
    static float RoadY(float z)=>Mathf.SmoothStep(0,5,Mathf.InverseLerp(60,240,z));
    static float RoadDistance(float x,float z){
        var p=new Vector2(x,z);float best=float.MaxValue;
        for(int i=1;i<road.Count;i++){var a=new Vector2(road[i-1].x,road[i-1].z);var d=new Vector2(road[i].x,road[i].z)-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);best=Mathf.Min(best,(p-a-d*t).sqrMagnitude);}return Mathf.Sqrt(best);
    }
    static float Height(float x,float z){
        float d=RoadDistance(x,z);
        float h=RoadY(z)+(Mathf.PerlinNoise((x+300)*.012f,(z+200)*.012f)*17+Mathf.PerlinNoise(x*.035f+60,z*.03f+90)*3)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,52,d));
        float yard=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(30,65,new Vector2(x,z-15).magnitude));
        h=Mathf.Lerp(h,0,yard);
        float village=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(37,65,new Vector2(x-8,z-250).magnitude));
        h=Mathf.Lerp(h,5,village);
        float edge=Mathf.Max(Mathf.Abs(x)/192,Mathf.Abs(z-122)/192);
        h+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,edge))*23;return h;
    }
    static void BuildTerrain(){
        var source=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        var td=new TerrainData{name="New_Map Terrain 384m",heightmapResolution=513,alphamapResolution=512,baseMapResolution=1024,size=new Vector3(384,80,384)};
        AssetDatabase.CreateAsset(td,A+"/Terrain/NewMapTerrain.asset");
        var heights=new float[513,513];for(int z=0;z<513;z++)for(int x=0;x<513;x++)heights[z,x]=(Height(x*.75f-192,z*.75f-70)+8)/80;
        td.SetHeights(0,0,heights);
        var layers=new[]{Object.Instantiate(source.terrainData.terrainLayers[0]),Object.Instantiate(source.terrainData.terrainLayers[1]),Object.Instantiate(source.terrainData.terrainLayers[4])};
        for(int i=0;i<3;i++){layers[i].name=new[]{"Meadow","Farm dirt","Field soil"}[i];AssetDatabase.CreateAsset(layers[i],A+"/Terrain/"+layers[i].name+".terrainlayer");}
        layers[0].diffuseRemapMax=new Vector4(.72f,.78f,.58f,1);
        td.terrainLayers=layers;
        var splat=new float[512,512,3];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++){
            float wx=x/511f*384-192,wz=z/511f*384-70,d=RoadDistance(wx,wz);
            float roadPaint=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.7f,5.6f,d));
            float yard=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,31,new Vector2(wx,wz-14).magnitude));
            float village=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(19,29,new Vector2(wx-8,wz-250).magnitude));
            float dirt=Mathf.Max(roadPaint,Mathf.Max(yard,village));
            float field=(wx>-100&&wx<-34&&wz>55&&wz<125)? .9f:0;
            splat[z,x,1]=dirt;splat[z,x,2]=field*(1-dirt);splat[z,x,0]=(1-dirt)*(1-field);
        }td.SetAlphamaps(0,0,splat);
        var g=Terrain.CreateTerrainGameObject(td);g.name="New Terrain - 384 x 384";g.transform.SetParent(root,false);g.transform.position=new Vector3(-192,-8,-70);
        var terrain=g.GetComponent<Terrain>();terrain.materialTemplate=source.materialTemplate;terrain.heightmapPixelError=4;terrain.drawInstanced=true;terrain.basemapDistance=500;
    }
    static void BuildHangar(){
        Box("Concrete threshing floor",hangar,new Vector3(0,-.16f,0),new Vector3(20,.4f,30),floor);
        // Original modular hangar kit: repeated portal frames, wall bays and roof sections.
        for(int z=-14;z<=14;z+=4){
            var arch=Prefab("TFP_Hall_Arch_01A",hangar,new Vector3(0,.05f,z),0,new Vector3(1.75f,1.2f,1));Paint(arch,timber);
            foreach(int side in new[]{-1,1}){
                var wall=Prefab("TFP_Hall_Wall_01A",hangar,new Vector3(side*9.75f,0,z+2),0,new Vector3(1,1.4f,1));Paint(wall,boards);
                for(int k=0;k<8;k++)Box("Vertical wall batten",hangar,new Vector3(side*9.66f,3.5f,z+k*.5f),new Vector3(.09f,7,.055f),timber,false);
            }
        }
        for(int z=-14;z<=14;z+=2){
            // Leave two narrow roof bays as framed skylights.
            if(z==-4||z==6){
                foreach(int side in new[]{-1,1})Beam("Skylight roof frame",hangar,new Vector3(side*10,6.8f,z),new Vector3(0,9.45f,z),.12f,timber);
                continue;
            }
            var roof=Prefab("TFP_Hall_Roof_01A",hangar,new Vector3(0,.05f,z),0,new Vector3(1.75f,1.2f,1));Paint(roof,boards);
        }
        // Rear wall, open 10m entrance, and independently editable cladding pieces.
        Box("Rear timber wall",hangar,new Vector3(0,3.5f,-15.05f),new Vector3(19.5f,7,.2f),boards);
        for(int x=-19;x<=19;x++){
            float px=x*.5f;float h=7+(1-Mathf.Abs(px)/10)*2.4f;
            Box("Rear gable plank",hangar,new Vector3(px,h*.5f,-15.17f),new Vector3(.48f,h,.1f),boards);
            if(Mathf.Abs(px)>5.1f)Box("Entrance side plank",hangar,new Vector3(px,3.5f,15.05f),new Vector3(.48f,7,.16f),boards);
            float top=h-7;Box("Entrance gable plank",hangar,new Vector3(px,7+top*.5f,15.05f),new Vector3(.48f,Mathf.Max(.1f,top),.16f),boards);
        }
        foreach(int side in new[]{-1,1}){
            Box("Door jamb",hangar,new Vector3(side*5.1f,3.5f,14.8f),new Vector3(.35f,7,.4f),timber);
            var door=Group("Open sliding door "+side,hangar).transform;door.localPosition=new Vector3(side*7.6f,0,15.4f);
            for(int i=0;i<10;i++)Box("Door plank",door,new Vector3((i-4.5f)*.46f,3.35f,0),new Vector3(.44f,6.7f,.15f),boards);
            for(int y=1;y<7;y+=4)Box("Door cross rail",door,new Vector3(0,y, -.13f),new Vector3(4.7f,.19f,.15f),timber);
            Beam("Door diagonal brace",door,new Vector3(-2.1f,.6f,-.15f),new Vector3(2.1f,6.1f,-.15f),.18f,timber);
        }
        Box("Entrance lintel",hangar,new Vector3(0,7,14.8f),new Vector3(20,.4f,.4f),timber);
        for(int z=-10;z<=12;z+=7){
            Beam("Pendant cable",hangar,new Vector3(-1,9,z),new Vector3(-1,5.9f,z),.025f,dark);
            var shade=GameObject.CreatePrimitive(PrimitiveType.Sphere);shade.name="Pendant shade";shade.transform.SetParent(hangar,false);shade.transform.localPosition=new Vector3(-1,5.9f,z);shade.transform.localScale=new Vector3(.65f,.18f,.65f);shade.GetComponent<Renderer>().sharedMaterial=metal;Object.DestroyImmediate(shade.GetComponent<Collider>());
            LightAt("Warm pendant",hangar,new Vector3(-1,5.7f,z),new Color(1,.7f,.35f),170,9);
        }
    }
    static void BuildDressing(){
        var rack=Group("Container shelves - reference left",dressing).transform;rack.localPosition=new Vector3(-5.3f,0,-.7f);
        for(int i=0;i<5;i++)Box("Oak upright",rack,new Vector3(-3+i*1.5f,2,0),new Vector3(.15f,4.1f,1.1f),timber);
        foreach(float y in new[]{.18f,1.95f,3.9f})Box("Shelf deck",rack,new Vector3(0,y,0),new Vector3(6.3f,.18f,1.3f),boards);
        Sign("CONTAINERS",rack,new Vector3(0,4.35f,-.15f),new Vector2(4.2f,.65f));
        for(int i=0;i<2;i++){
            float x=-2.3f+i*1.45f;
            Prefab("TFP_Wooden_Pallet_01A",rack,new Vector3(x,2.05f,-.12f),0,new Vector3(.85f,.65f,.8f));
            for(int j=0;j<4;j++)Prefab("TFP_Sand_Bag_01A",rack,new Vector3(x,2.12f+j*.29f,-.08f),j%2*180,new Vector3(1.15f,.9f,1));
        }
        Prefab("TFP_Wooden_Crate_01A",rack,new Vector3(.75f,2.07f,0),0,new Vector3(1.15f,1.15f,1.15f));
        var barrel=Prefab("TFP_Metal_Barrel_01A",rack,new Vector3(2.25f,2.06f,0),0,new Vector3(1.2f,1.2f,1.2f));Paint(barrel,metal);
        var labels=new[]{"Small Sack\n25 kg","Large Sack\n50 kg","Crate\n80 kg","Barrel\n120 kg"};
        for(int i=0;i<4;i++)Sign(labels[i],rack,new Vector3(-2.3f+i*1.5f,1.88f,-.75f),new Vector2(1.15f,.53f));
        Prefab("TFP_Industrial_Seed_Bag_01A",rack,new Vector3(-2.2f,.29f,0),0,new Vector3(1, .72f,1));
        Sign("Big Bag\n500 kg",rack,new Vector3(-2.2f,.36f,-.8f),new Vector2(1.2f,.55f));
        for(int i=0;i<3;i++)Prefab("TFP_Sack_Pile_01A",rack,new Vector3(-.5f+i*1.15f,.29f,0),0,new Vector3(1,1.3f,.7f));
        Sign("Empty Sacks",rack,new Vector3(1,.36f,-.8f),new Vector2(2,.45f));
        var truck=Prefab("TFP_Utility_Pick_Up_02A",dressing,new Vector3(-.7f,.08f,9),0,new Vector3(.8f,.8f,.8f));
        var bed=Prefab("TFP_Utility_Pick_Up_02A_Wooden_Bed",dressing,new Vector3(-.7f,.08f,9),0,new Vector3(.8f,.8f,.8f));
        for(int i=0;i<6;i++)Prefab("TFP_Cereal_Sack_01A",dressing,new Vector3(-1.2f+(i%3)*.5f,1.05f,8.6f+(i/3)*.5f),20*i,new Vector3(.9f,.9f,.9f));
        var barrow=Prefab("TFP_Wheelbarrow_02A",dressing,new Vector3(-.5f,.08f,-1.8f),-60,new Vector3(1.2f,1.2f,1.2f));
        for(int i=0;i<3;i++){Prefab("TFP_Wooden_Pallet_01A",dressing,new Vector3(-8.3f,.05f,5+i*1.5f),10);Prefab("TFP_Sack_Pile_01A",dressing,new Vector3(-8.3f,.2f,5+i*1.5f),10);}
        Prefab("TFP_Wooden_Crate_01A",dressing,new Vector3(-4.7f,.05f,-7.6f),15,new Vector3(1.4f,1.4f,1.4f));
        Prefab("TFP_Wooden_Crate_01B",dressing,new Vector3(-6,.05f,-6.6f),-8,new Vector3(1.3f,1.3f,1.3f));
        Prefab("TFP_Cereal_Sack_01A",dressing,new Vector3(.15f,.06f,-5.7f),0,new Vector3(1.6f,1.6f,1.6f));
        var pile=Group("Golden maize stockpile",dressing).transform;
        MakePile(pile,new Vector3(6,0,3),new Vector3(5.2f,5.8f,10), "Main grain mound");
        MakePile(pile,new Vector3(7,0,-5),new Vector3(3.1f,2.3f,5), "Foreground grain slope");
        MakeKernels(pile);
        Prefab("TFP_Shovel_01A",dressing,new Vector3(-7.8f,.1f,12),-25);
        Sign("GRAIN STORE",hangar,new Vector3(0,7.7f,15.25f),new Vector2(4.2f,.7f),180);
    }
    static float PileHeight(float x,float z){
        float a=1-Mathf.Pow((x-6)/5.2f,2)-Mathf.Pow((z-3)/10,2);
        float b=1-Mathf.Pow((x-7)/3.1f,2)-Mathf.Pow((z+5)/5,2);
        return Mathf.Max(a>0?5.8f*Mathf.Pow(a,1.2f):0,b>0?2.3f*Mathf.Pow(b,1.2f):0);
    }
    static void MakePile(Transform parent,Vector3 center,Vector3 dims,string name){
        int n=60;var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){
            float xx=(x/(float)n*2-1)*dims.x,zz=(z/(float)n*2-1)*dims.z;
            float f=1-xx*xx/(dims.x*dims.x)-zz*zz/(dims.z*dims.z);
            float y=f>0?dims.y*Mathf.Pow(f,1.2f):-.05f;
            verts.Add(center+new Vector3(xx,y+.04f,zz));uv.Add(new Vector2(xx,zz));
            if(x<n&&z<n){int k=z*(n+1)+x;tris.AddRange(new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2});}
        }
        MeshObject(name,parent,verts,uv,tris,gold,true);
    }
    static GameObject MeshObject(string name,Transform parent,List<Vector3> verts,List<Vector2> uv,List<int> tris,Material mat,bool collider=false){
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,A+"/Meshes/"+name+".asset");
        var g=Group(name,parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;
    }
    static void MakeKernels(Transform p){
        var kernel=Mat("Individual maize kernels",new Color(1,.6f,.08f),.3f);
        for(int batch=0;batch<4;batch++){
            var verts=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<3200;i++){
                float x=R(-1.5f,9.2f),z=R(-10,12);float y=PileHeight(x,z);
                if(y<.03f && rng.NextDouble()>.3)continue;
                float rx=R(.025f,.05f),rz=R(.035f,.065f),ry=R(.025f,.04f);int b=verts.Count;
                verts.Add(new Vector3(x,y+.035f+ry,z));uv.Add(Vector2.zero);
                for(int j=0;j<6;j++){float a=j*Mathf.PI/3;verts.Add(new Vector3(x+Mathf.Cos(a)*rx,y+.035f,z+Mathf.Sin(a)*rz));uv.Add(Vector2.zero);}
                for(int j=0;j<6;j++)tri.AddRange(new[]{b,b+1+j,b+1+(j+1)%6});
            }MeshObject("Maize kernels batch "+batch,p,verts,uv,tri,kernel);
        }
    }
    static void BuildYard(){
        var yard=Group("03_Farmyard",root).transform;
        // Weighbridge is to the right of the entrance, outside the drivable road.
        var weigh=Group("Weigh station",yard).transform;weigh.localPosition=new Vector3(21,0,25);
        Box("Weighbridge platform",weigh,new Vector3(0,.12f,0),new Vector3(7,.24f,12),floor);
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box("Canopy pillar",weigh,new Vector3(x*3.7f,2.7f,z*5.8f),new Vector3(.22f,5.4f,.22f),metal);
        Box("Canopy",weigh,new Vector3(0,5.5f,0),new Vector3(8,.3f,13),metal);
        Sign("WEIGH STATION",weigh,new Vector3(0,5.4f,-6.6f),new Vector2(6.5f,.65f));
        var yellow=Mat("Safety yellow",new Color(.95f,.58f,.025f));
        for(int z=-4;z<=4;z+=4)foreach(int side in new[]{-1,1}){
            Box("Safety bollard",weigh,new Vector3(side*3.4f,.7f,z),new Vector3(.32f,1.4f,.32f),yellow);
            Box("Bollard stripe",weigh,new Vector3(side*3.4f,.75f,z-.01f),new Vector3(.34f,.3f,.34f),dark);
        }
        Prefab("TFP_Utility_Pick_Up_02B",weigh,new Vector3(0,.25f,1),90);
        Box("Scale office",weigh,new Vector3(5,1.5f,3),new Vector3(2.5f,3,3),boards);
        Sign("SCALE\nOFFICE",weigh,new Vector3(5,2.2f,1.45f),new Vector2(1.7f,.7f));
        for(int i=0;i<3;i++)Silo(yard,new Vector3(-22-i*8,0,40+i%2*3),"02B",1);
        Prefab("TFP_Barn_03A",yard,new Vector3(-32,0,26),180,new Vector3(.8f,.8f,.8f));
        Prefab("TFP_Utility_Pick_Up_01A",yard,new Vector3(-12,0,20),10);
        for(int i=0;i<3;i++)Prefab("TFP_Sack_Pile_01A",yard,new Vector3(-8+i*1.5f,.15f,17),15);
        var signs=Group("Farm direction signs",yard).transform;signs.localPosition=new Vector3(11,0,32);signs.localRotation=Quaternion.Euler(0,-20,0);
        Box("Sign post",signs,new Vector3(0,2,0),new Vector3(.2f,4,.2f),timber);
        string[] words={"SELL GRAIN  ^","SUPPLIES  >","FUEL  >","VILLAGE  ^"};
        for(int i=0;i<4;i++)Sign(words[i],signs,new Vector3(0,3.6f-i*.63f,-.1f),new Vector2(2.8f,.52f));
        Fence(yard,new Vector3(-17,0,18),new Vector3(-52,0,18));
        Fence(yard,new Vector3(12,0,38),new Vector3(35,0,45));
        for(int i=0;i<5;i++)Prefab("TFP_Metal_Barrel_01A",yard,new Vector3(30+(i%2),0,13+i/2),i*40);
    }
    static void Silo(Transform p,Vector3 pos,string kind,float scale){
        var g=Group("Modular silo "+kind,p).transform;g.localPosition=pos;g.localScale=Vector3.one*scale;
        Prefab("TFP_Silo_"+kind+"_Base",g,Vector3.zero);
        Prefab("TFP_Silo_"+kind+"_Middle",g,new Vector3(0,1.5f,0));
        Prefab("TFP_Silo_"+kind+"_Lid",g,new Vector3(0,13.5f,0));
    }
    static void Fence(Transform p,Vector3 a,Vector3 b){
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);
        for(int i=0;i<=count;i++){
            var q=Vector3.Lerp(a,b,i/(float)count);q.y=Height(q.x,q.z);
            Box("Fence post",p,q+Vector3.up*.7f,new Vector3(.18f,1.4f,.18f),timber);
            if(i>0){var prev=Vector3.Lerp(a,b,(i-1)/(float)count);prev.y=Height(prev.x,prev.z);
                Beam("Fence rail",p,prev+Vector3.up*.55f,q+Vector3.up*.55f,.12f,boards);
                Beam("Fence rail",p,prev+Vector3.up*1.12f,q+Vector3.up*1.12f,.12f,boards);}
        }
    }
    static void BuildVillage(){
        var v=Group("04_Mini_Village",root).transform;
        string[] homes={"TFP_House_01A_Preset_1","TFP_House_02A_Preset_1","TFP_House_03A_Preset_1"};
        Vector3[] points={new Vector3(-17,5,235),new Vector3(28,5,234),new Vector3(-19,5,267),new Vector3(27,5,268),new Vector3(1,5,280)};
        for(int i=0;i<points.Length;i++)Prefab(homes[i%3],v,points[i],i%2==0?90:-90);
        Prefab("TFP_Barn_01A",v,new Vector3(40,5,254),90);
        Silo(v,new Vector3(10,5,278),"01A",1.3f);
        Prefab("TFP_Water_Tower_01A",v,new Vector3(-37,5,262));
        Sign("WILLOW CREEK\nGRAIN COOPERATIVE",v,new Vector3(8,7.4f,225),new Vector2(5,1));
        foreach(int x in new[]{-1,1})Box("Village gateway post",v,new Vector3(8+x*2.4f,6.3f,225),new Vector3(.18f,2.6f,.18f),timber);
        Prefab("TFP_Utility_Pick_Up_01A",v,new Vector3(14,5,254),25);
        for(int i=0;i<8;i++)Prefab("TFP_Wooden_Crate_01A",v,new Vector3(30+i%4*1.2f,5,248+i/4*1.2f),i*12);
        Fence(v,new Vector3(-30,5,220),new Vector3(-30,5,277));
    }
    static void BuildLandscape(){
        for(int i=0;i<160;i++){
            float x=R(-169,169),z=R(-48,296);
            if(RoadDistance(x,z)<10||new Vector2(x,z-10).magnitude<36||new Vector2(x-8,z-250).magnitude<43)continue;
            if(x>-105&&x<-30&&z>50&&z<130)continue;
            string name="TFP_Beech_Tree_0"+(i%3+1)+"A";float scale=R(.8f,1.7f);
            Prefab(name,landscape,new Vector3(x,Height(x,z),z),R(0,360),Vector3.one*scale);
        }
        // Deliberate trees frame the yard and the village approach.
        foreach(var q in new[]{new Vector3(19,0,44),new Vector3(-18,0,53),new Vector3(-23,0,-6),new Vector3(38,0,26),new Vector3(-9,0,216),new Vector3(30,0,215)})
            Prefab("TFP_Beech_Tree_02A",landscape,new Vector3(q.x,Height(q.x,q.z),q.z),R(0,360),Vector3.one*1.4f);
        for(int i=0;i<450;i++){
            float x=R(-135,135),z=R(-40,288),d=RoadDistance(x,z);
            if(d<5.8f||new Vector2(x,z-10).magnitude<29||new Vector2(x-8,z-250).magnitude<32)continue;
            Prefab(i%4==0?"TFP_Bush_01A":"TFP_Grass_Patch_03A",landscape,new Vector3(x,Height(x,z)+.01f,z),R(0,360),Vector3.one*R(.7f,1.3f));
        }
        for(int i=15;i<road.Count;i+=23){
            var q=road[i]+new Vector3(9,0,0);q.y=Height(q.x,q.z);
            Prefab("TFP_Wooden_Electricity_Pole_01A",landscape,q,90);
        }
        Fence(landscape,new Vector3(-31,0,53),new Vector3(-105,0,53));
        Fence(landscape,new Vector3(-105,0,53),new Vector3(-105,0,127));
        for(int i=0;i<90;i++){
            float x=R(-100,-38),z=R(59,120);
            Prefab("TFP_Wheat_Patch_01A",landscape,new Vector3(x,Height(x,z),z),R(0,180),Vector3.one*2);
        }
    }
    static void LightAt(string n,Transform parent,Vector3 pos,Color c,float lumens,float range){
        var g=Group(n,parent);g.transform.localPosition=pos;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.range=range;l.shadows=LightShadows.None;
        var hd=g.AddComponent<HDAdditionalLightData>();hd.SetIntensity(lumens,LightUnit.Lumen);
    }
    static void BuildLighting(){
        var old=GameObject.Find("Graphics");var g=Object.Instantiate(old,root);g.name="08_Lighting";g.transform.position=Vector3.zero;
        Undo.RecordObject(old,"Preserve old lighting");old.SetActive(false);
        foreach(var v in g.GetComponentsInChildren<Volume>()){
            var profile=Object.Instantiate(v.sharedProfile);profile.name="NewMap "+v.name;
            AssetDatabase.CreateAsset(profile,A+"/Lighting/"+profile.name+".asset");
            // Clone each component; copying only the container would mutate the source profile.
            profile.components=new List<VolumeComponent>();
            foreach(var c in v.sharedProfile.components){var copy=Object.Instantiate(c);copy.name=c.name;AssetDatabase.AddObjectToAsset(copy,profile);profile.components.Add(copy);}
            v.sharedProfile=profile;
            if(profile.TryGet<ColorAdjustments>(out var ca)){ca.saturation.Override(-10);ca.contrast.Override(6);}
            if(profile.TryGet<Tonemapping>(out var tm))tm.mode.Override(TonemappingMode.ACES);
            if(profile.TryGet<HDRISky>(out var sky))sky.exposure.Override(.55f);
            if(profile.TryGet<Fog>(out var fog)){fog.meanFreePath.Override(650);fog.baseHeight.Override(-10);fog.maximumHeight.Override(90);fog.color.Override(new Color(.58f,.69f,.73f));}
        }
        foreach(var probe in g.GetComponentsInChildren<ReflectionProbe>())Object.DestroyImmediate(probe.gameObject);
        var sun=g.GetComponentInChildren<Light>();sun.transform.rotation=Quaternion.Euler(38,215,0);sun.color=new Color(1,.86f,.65f);sun.intensity=7;
        LightAt("Soft warm bounce",hangar,new Vector3(-3,4,0),new Color(1,.73f,.44f),750,22);
        LightAt("Doorway sky fill",hangar,new Vector3(0,4,12),new Color(.72f,.82f,1),600,23);
    }
}

