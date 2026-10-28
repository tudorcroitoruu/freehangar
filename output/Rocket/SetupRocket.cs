using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using Object=UnityEngine.Object;
public static class SetupRocket
{
 const string A="Assets/Models/Rocket Modular";
 static Material metal,trim,screen,amber;
 static Material Mat(string name,Color color,float metallic=0)
 {
  var m=new Material(Shader.Find("HDRP/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.35f);AssetDatabase.CreateAsset(m,A+"/"+name+".mat");return m;
 }
 static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material m)
 {
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g;
 }
 static void Label(Transform p,string text,Vector3 pos,float width)
 {
  var g=new GameObject(text);g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,180,0);var t=g.AddComponent<TextMeshPro>();t.text=text;t.fontSize=.65f;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.65f,.92f,1);t.rectTransform.sizeDelta=new Vector2(width,.12f);
 }
 static Texture2D EmissionTexture()
 {
  var t=new Texture2D(512,256,TextureFormat.RGBA32,false);var pixels=Enumerable.Repeat(Color.black,512*256).ToArray();t.SetPixels(pixels);
  Action<int,int,Color> pixel=(x,y,c)=>{if(x>=0&&x<512&&y>=0&&y<256)t.SetPixel(x,y,c);};
  Action<int,int,int,int,Color> line=(x0,y0,x1,y1,c)=>{int n=Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0));for(int i=0;i<=n;i++){float u=n==0?0:i/(float)n;int x=Mathf.RoundToInt(Mathf.Lerp(x0,x1,u)),y=Mathf.RoundToInt(Mathf.Lerp(y0,y1,u));pixel(x,y,c);pixel(x+1,y,c);}};
  var cyan=new Color(.1f,.8f,1);var green=new Color(.3f,1,.35f);var orange=new Color(1,.45f,.08f);
  for(int x=16;x<512;x+=32)line(x,16,x,240,cyan*.12f);for(int y=16;y<256;y+=32)line(16,y,496,y,cyan*.12f);
  foreach(int radius in new[]{28,57,86})for(int i=0;i<360;i++){float a=i*Mathf.Deg2Rad;pixel(122+Mathf.RoundToInt(Mathf.Cos(a)*radius),128+Mathf.RoundToInt(Mathf.Sin(a)*radius),cyan);}
  line(28,128,216,128,cyan*.6f);line(122,34,122,222,cyan*.6f);line(122,128,179,189,green);
  for(int i=0;i<7;i++){int h=40+(i*23)%115;for(int x=260+i*31;x<278+i*31;x++)for(int y=48;y<48+h;y++)pixel(x,y,i==5?orange:cyan);}
  for(int x=254;x<487;x++)pixel(x,210+Mathf.RoundToInt(Mathf.Sin(x*.095f)*11),green);
  line(8,8,504,8,cyan);line(8,248,504,248,cyan);line(8,8,8,248,cyan);line(504,8,504,248,cyan);
  t.Apply();string path=A+"/Cockpit displays emission.png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static void Console(Transform parent,string name,Vector3 pos,float yaw)
 {
  var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);
  Box(g,"Equipment cabinet",new Vector3(0,-.23f,-.06f),new Vector3(.86f,.68f,.32f),metal);
  Box(g,"Screen bezel",new Vector3(0,.15f,.12f),new Vector3(.84f,.49f,.08f),trim);
  var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Emissive telemetry display";q.transform.SetParent(g,false);q.transform.localPosition=new Vector3(0,.15f,.166f);q.transform.localRotation=Quaternion.Euler(0,180,0);q.transform.localScale=new Vector3(.73f,.365f,1);q.GetComponent<Renderer>().sharedMaterial=screen;Object.DestroyImmediate(q.GetComponent<Collider>());
  Label(g,name,new Vector3(0,.44f,.13f),.9f);
  for(int k=0;k<5;k++){Box(g,"Status indicator "+k,new Vector3(-.3f+k*.15f,-.17f,.115f),new Vector3(.065f,.025f,.02f),k==4?amber:screen);Box(g,"Toggle "+k,new Vector3(-.3f+k*.15f,-.28f,.13f),new Vector3(.025f,.07f,.055f),trim);}
 }
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");AssetDatabase.Refresh();
  if(AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Rocket Modular.prefab"))throw new Exception("Modular prefab already exists");
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Rocket separated.fbx");
  var original=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).First(f=>AssetDatabase.GetAssetPath(f.sharedMesh)=="Assets/Models/rocket.fbm/rocket.fbx");
  EditorSceneManager.SaveScene(original.gameObject.scene,"output/Rocket/Before.unity",true);
  var exterior=original.GetComponent<Renderer>().sharedMaterial;
  var root=new GameObject("Rocket Modular");
  foreach(var f in source.GetComponentsInChildren<MeshFilter>(true))
  {
   Vector3 pivot=f.transform.position;var mesh=new Mesh{name=f.name};mesh.CombineMeshes(new[]{new CombineInstance{mesh=f.sharedMesh,transform=Matrix4x4.Translate(-pivot)*f.transform.localToWorldMatrix}},true,true);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,A+"/"+f.name+".asset");
   var part=new GameObject(f.name);part.transform.SetParent(root.transform,false);part.transform.localPosition=pivot;part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=exterior;part.AddComponent<MeshCollider>().sharedMesh=mesh;
  }
  metal=Mat("Cabin graphite",new Color(.055f,.085f,.11f),.65f);trim=Mat("Cabin aluminum",new Color(.23f,.31f,.36f),.75f);
  screen=Mat("Cockpit emissive screens",new Color(.006f,.018f,.025f));screen.SetTexture("_EmissiveColorMap",EmissionTexture());screen.SetColor("_EmissiveColor",Color.white*5);screen.SetColor("_EmissiveColorLDR",Color.white);screen.EnableKeyword("_EMISSIVE_COLOR_MAP");screen.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(screen);
  amber=Mat("Amber status emission",new Color(.2f,.08f,.005f));amber.SetColor("_EmissiveColor",new Color(5,1.5f,.04f));EditorUtility.SetDirty(amber);
  var cabin=new GameObject("Interior Devices").transform;cabin.SetParent(root.transform,false);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cylinder);floor.name="Cabin floor";floor.transform.SetParent(cabin,false);floor.transform.localPosition=new Vector3(0,1.73f,0);floor.transform.localScale=new Vector3(1.26f,.04f,1.26f);floor.GetComponent<Renderer>().sharedMaterial=metal;
  for(int k=0;k<12;k++)
  {
   float a=k*30*Mathf.Deg2Rad;if(k==0)continue;var p=new Vector3(Mathf.Sin(a)*1.3f,2.95f,Mathf.Cos(a)*1.3f);var wall=Box(cabin,"Interior wall segment "+k,p,new Vector3(.69f,2.36f,.045f),metal);wall.transform.localRotation=Quaternion.Euler(0,k*30,0);
   Box(wall.transform,"Cabin light strip",new Vector3(0,.38f,-.65f),new Vector3(.72f,.012f,.15f),amber);
  }
  Console(cabin,"NAVIGATION",new Vector3(0,2.35f,-.91f),0);
  Console(cabin,"ENGINE CONTROL",new Vector3(-.98f,2.35f,-.1f),65);
  Console(cabin,"POWER SYSTEM",new Vector3(.98f,2.35f,-.1f),-65);
  Box(cabin,"Door threshold",new Vector3(0,1.73f,1.1f),new Vector3(.92f,.08f,.72f),trim);
  AssetDatabase.SaveAssets();var prefab=PrefabUtility.SaveAsPrefabAsset(root,A+"/Rocket Modular.prefab");Object.DestroyImmediate(root);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,original.gameObject.scene);instance.transform.SetParent(original.transform,false);instance.transform.localRotation=Quaternion.Euler(90,0,0);instance.transform.localScale=Vector3.one*.01f;
  original.GetComponent<Renderer>().enabled=false;foreach(var c in original.GetComponents<Collider>())c.enabled=false;
  EditorSceneManager.SaveScene(original.gameObject.scene);Selection.activeGameObject=instance;
  return "Created modular prefab with separate hinged door, stairs, open cabin and three emissive consoles. Preserved original scene transform and original FBX.";
 }
}
