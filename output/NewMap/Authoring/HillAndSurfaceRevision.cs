using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class HillAndSurfaceRevision {
static Transform root;static Terrain terrain;static List<Vector3> road;
static Vector3[] knots={new Vector3(0,0,15),new Vector3(1,0,35),new Vector3(20,.15f,53),new Vector3(82,2,70),new Vector3(125,6,115),new Vector3(68,9,141),new Vector3(-28,7,106),new Vector3(-76,9.5f,143),new Vector3(-37,12,178),new Vector3(8,12,175)};
static void Road(){
road=new List<Vector3>();for(int i=0;i<knots.Length-1;i++)for(int j=0;j<30;j++){
float t=j/30f;var a=knots[Math.Max(0,i-1)];var b=knots[i];var c=knots[i+1];var d=knots[Math.Min(knots.Length-1,i+2)];
road.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}road.Add(knots.Last());}
static Vector2 Nearest(float x,float z){
var p=new Vector2(x,z);float best=float.MaxValue,yy=0;
for(int i=1;i<road.Count;i++){var a=new Vector2(road[i-1].x,road[i-1].z);var d=new Vector2(road[i].x,road[i].z)-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);float dist=(p-a-d*t).sqrMagnitude;if(dist<best){best=dist;yy=Mathf.Lerp(road[i-1].y,road[i].y,t);}}return new Vector2(Mathf.Sqrt(best),yy);
}
static float Height(float x,float z,Vector2 nearest){
float rise=12*Mathf.SmoothStep(0,1,Mathf.InverseLerp(48,164,z));
float hill=(Mathf.PerlinNoise((x+200)*.018f,(z+400)*.018f)-.35f)*6;
float h=rise+hill;
float plateau=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(39,69,new Vector2(x-8,z-175).magnitude));h=Mathf.Lerp(h,12,plateau);
float yard=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(37,63,new Vector2(x,z-14).magnitude));h=Mathf.Lerp(h,0,yard);
float flatten=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,22,nearest.x));h=Mathf.Lerp(h,nearest.y,flatten);
float skyline=Mathf.SmoothStep(0,1,Mathf.InverseLerp(245,314,z))*8;h+=skyline;
float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(158,192,Mathf.Abs(x)))*17;return Mathf.Max(-.05f,h+edge);
}
static void Import(string p,bool normal=false){
var imp=(TextureImporter)AssetImporter.GetAtPath(p);imp.maxTextureSize=2048;imp.npotScale=TextureImporterNPOTScale.None;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=16;imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.Uncompressed;
if(normal){imp.textureType=TextureImporterType.NormalMap;imp.convertToNormalmap=true;imp.heightmapScale=.015f;}imp.SaveAndReimport();
}
static Material Surface(string path,string albedo,string normal,Color tint,float worldScale,float bump){
var mat=AssetDatabase.LoadAssetAtPath<Material>(path);mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));mat.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
mat.SetColor("_BaseColor",tint);mat.SetFloat("_UVBase",5);mat.SetFloat("_TexWorldScale",worldScale);mat.SetFloat("_NormalScale",bump);mat.SetFloat("_Smoothness",.16f);mat.SetTextureScale("_BaseColorMap",Vector2.one);HDMaterial.ValidateMaterial(mat);EditorUtility.SetDirty(mat);return mat;
}
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Wrong scene");
root=GameObject.Find("New_Map").transform;terrain=root.GetComponentInChildren<Terrain>();
EditorSceneManager.SaveScene(scene);File.Copy(scene.path,"Temp/NewMap/Gameplay.before-hill-revision.unity",true);
Directory.CreateDirectory("Assets/NewMap/Textures");AssetDatabase.Refresh();
foreach(var key in new[]{"Oak","PackedEarth"}){var p="Assets/NewMap/Textures/"+key;AssetDatabase.CopyAsset(p+"_Albedo.png",p+"_Normal.png");Import(p+"_Albedo.png");Import(p+"_Normal.png",true);}
var oak=Surface("Assets/NewMap/Materials/Warm oak structural beams.mat","Assets/NewMap/Textures/Oak_Albedo.png","Assets/NewMap/Textures/Oak_Normal.png",new Color(.65f,.52f,.4f),.7f,.28f);
var boards=Surface("Assets/NewMap/Materials/Weathered honey timber.mat","Assets/NewMap/Textures/Oak_Albedo.png","Assets/NewMap/Textures/Oak_Normal.png",new Color(.85f,.77f,.65f),.6f,.23f);
Surface("Assets/NewMap/Materials/Warm dusty concrete.mat","Assets/NewMap/Textures/PackedEarth_Albedo.png","Assets/NewMap/Textures/PackedEarth_Normal.png",new Color(.8f,.76f,.69f),.32f,.18f);
// Keep shared imported materials untouched, use scene-owned wood on wood-only props.
foreach(var tr in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("TFP_Wooden_Crate")||t.name.StartsWith("TFP_Wooden_Pallet"))){
if(tr.GetComponent<LODGroup>()==null&&tr.parent.name.StartsWith("TFP_Wooden"))continue;
foreach(var r in tr.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>boards).ToArray();
}
var body=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Canvas beige.mat");body.SetFloat("_NormalScale",.15f);body.SetFloat("_Smoothness",.08f);body.SetTextureScale("_BaseColorMap",new Vector2(2,2));EditorUtility.SetDirty(body);
foreach(var p in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/NewMap/SignTextures"}).Select(AssetDatabase.GUIDToAssetPath))Import(p);
Road();
var td=terrain.terrainData;var heights=new float[513,513];
for(int z=0;z<513;z++)for(int x=0;x<513;x++){float wx=x*.75f-192,wz=z*.75f-70;heights[z,x]=(8+Height(wx,wz,Nearest(wx,wz)))/80;}
td.SetHeights(0,0,heights);
var dirt=td.terrainLayers[1];dirt.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NewMap/Textures/PackedEarth_Albedo.png");dirt.normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NewMap/Textures/PackedEarth_Normal.png");dirt.normalScale=.22f;dirt.tileSize=new Vector2(4,4);dirt.diffuseRemapMin=Vector4.zero;dirt.diffuseRemapMax=Vector4.one;EditorUtility.SetDirty(dirt);
var meadow=td.terrainLayers[0];meadow.tileSize=new Vector2(4,4);meadow.diffuseRemapMax=new Vector4(.82f,.88f,.75f,1);EditorUtility.SetDirty(meadow);
var alpha=new float[512,512,3];
for(int z=0;z<512;z++)for(int x=0;x<512;x++){
float wx=x/511f*384-192,wz=z/511f*384-70;var near=Nearest(wx,wz);
float variation=(Mathf.PerlinNoise(wx*.55f+300,wz*.55f+500)-.5f)*.7f;
float track=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.8f,5.6f,near.x+variation));
float yard=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,34,new Vector2(wx,wz-14).magnitude));
float station=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,14,new Vector2(wx-25,wz-39).magnitude));
float village=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(19,30,new Vector2(wx-8,wz-175).magnitude));
float dirtWeight=Mathf.Max(track,Mathf.Max(yard,Mathf.Max(station,village)));
float field=(wx>-100&&wx<-34&&wz>55&&wz<125)?.8f:0;alpha[z,x,1]=dirtWeight;alpha[z,x,2]=field*(1-dirtWeight);alpha[z,x,0]=(1-dirtWeight)*(1-field);
}td.SetAlphamaps(0,0,alpha);terrain.heightmapPixelError=2;terrain.basemapDistance=650;
var villageRoot=root.Find("04_Mini_Village");villageRoot.position+=new Vector3(0,7,-75);
var yardRoot=root.Find("03_Farmyard");yardRoot.Find("Weigh station").localPosition=new Vector3(25,0,39);yardRoot.Find("Weigh station").localRotation=Quaternion.Euler(0,0,0);
foreach(var tr in root.Find("05_Landscape").Cast<Transform>().Where(t=>t.name.StartsWith("TFP_")).ToArray()){
var pos=tr.position;var n=Nearest(pos.x,pos.z);float vd=new Vector2(pos.x-8,pos.z-175).magnitude;
if(n.x<6||(vd<35&&tr.name.Contains("Tree"))||new Vector2(pos.x-25,pos.z-39).magnitude<10){tr.gameObject.SetActive(false);continue;}
pos.y=terrain.SampleHeight(pos)+terrain.transform.position.y;tr.position=pos;
}
foreach(var tr in yardRoot.Cast<Transform>().Where(t=>t.name=="Fence post")){var pos=tr.position;pos.y=terrain.SampleHeight(pos)-8+.7f;tr.position=pos;}
var route=root.Find("06_Road_Route");foreach(var tr in route.Cast<Transform>().ToArray())Object.DestroyImmediate(tr.gameObject);
for(int i=0;i<road.Count;i+=10){var g=new GameObject("Route_"+i.ToString("000"));g.transform.SetParent(route);g.transform.position=road[i];}
var vc=root.Find("07_Reference_Views/Village Review");vc.position+=new Vector3(0,7,-75);
// Low-vibration TAA on the existing user camera; preserve its position, rotation and FOV.
var player=GameObject.Find("Player");var camera=player.GetComponentInChildren<Camera>();var hd=camera.GetComponent<HDAdditionalCameraData>();if(hd)hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
float len=0,maxGrade=0;for(int i=1;i<road.Count;i++){var d=road[i]-road[i-1];len+=d.magnitude;maxGrade=Mathf.Max(maxGrade,Mathf.Abs(d.y)/new Vector2(d.x,d.z).magnitude);}
File.WriteAllText("output/NewMap/Revision2/layout.txt","Road length "+len.ToString("F1")+" m; time at 8 m/s "+(len/8).ToString("F1")+" s; maximum centerline grade "+(maxGrade*100).ToString("F1")+"%; village ground 12m above farmyard. "+road.Count+" centerline samples.");
AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
return File.ReadAllText("output/NewMap/Revision2/layout.txt");
}
}
