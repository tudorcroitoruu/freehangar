using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class ContourAndFinish {
static Transform root;static Terrain terrain;static List<Vector3> road;
static Vector3[] knots={new Vector3(0,0,15),new Vector3(0,0,35),new Vector3(0,1.5f,65),new Vector3(45,4.5f,91),new Vector3(116,7,125),new Vector3(68,9,148),new Vector3(-28,7,106),new Vector3(-76,9.5f,143),new Vector3(-37,12,178),new Vector3(8,12,175)};
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
if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode");
root=GameObject.Find("New_Map").transform;terrain=root.GetComponentInChildren<Terrain>();Road();
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
foreach(var tr in root.Find("05_Landscape").Cast<Transform>().Where(t=>t.name.StartsWith("TFP_")).ToArray()){
var p=tr.position;var n=Nearest(p.x,p.z);
if(n.x<6||(new Vector2(p.x-8,p.z-175).magnitude<35&&tr.name.Contains("Tree"))||new Vector2(p.x-25,p.z-39).magnitude<10){tr.gameObject.SetActive(false);continue;}
p.y=Ground(p);tr.position=p;
}
foreach(var group in new[]{root.Find("03_Farmyard"),root.Find("04_Mini_Village"),root.Find("05_Landscape")}){
foreach(var t in group.Cast<Transform>().Where(t=>t.name=="Fence post")){
var p=t.position;p.y=Ground(p)+.7f;t.position=p;
if(group.name=="03_Farmyard"&&p.x>10&&p.z>35)t.gameObject.SetActive(false);
}
foreach(var t in group.Cast<Transform>().Where(t=>t.name=="Fence rail")){
var a=t.TransformPoint(new Vector3(0,-.5f,0));var b=t.TransformPoint(new Vector3(0,.5f,0));
// Each pair was authored consecutively with lower and upper rails.
bool upper=t.GetSiblingIndex()>0 && group.GetChild(t.GetSiblingIndex()-1).name=="Fence rail";
a.y=Ground(a)+(upper?1.12f:.55f);b.y=Ground(b)+(upper?1.12f:.55f);
t.position=(a+b)*.5f;t.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);t.localScale=new Vector3(.12f,(b-a).magnitude,.12f);
if(group.name=="03_Farmyard"&&t.position.x>10&&t.position.z>35)t.gameObject.SetActive(false);
}
}
foreach(var t in root.Find("03_Farmyard").Cast<Transform>().Where(t=>t.name.StartsWith("Modular silo")||t.name.StartsWith("TFP_Barn"))){
var p=t.position;p.y=Ground(p);t.position=p;
}
foreach(var t in root.Find("04_Mini_Village").Cast<Transform>().Where(t=>t.name.StartsWith("TFP_")||t.name.StartsWith("Modular silo"))){
var p=t.position;p.y=Ground(p);t.position=p;
}
var route=root.Find("06_Road_Route");foreach(var t in route.Cast<Transform>().ToArray())Object.DestroyImmediate(t.gameObject);
for(int i=0;i<road.Count;i+=10){var g=new GameObject("Route_"+i.ToString("000"));g.transform.SetParent(route);g.transform.position=road[i];}
var roadFile=string.Join("\n",road.Select(p=>string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:F3},{1:F3},{2:F3}",p.x,p.y,p.z)));
File.WriteAllText("output/NewMap/Revision2/road-centerline.csv","x,y,z\n"+roadFile);
var vp=new VolumeProfile{name="New Map surface grading"};AssetDatabase.CreateAsset(vp,"Assets/NewMap/Lighting/SurfaceGrade.asset");
var ca=vp.Add<ColorAdjustments>(true);ca.saturation.value=-6;ca.postExposure.value=-.2f;ca.contrast.value=8;
var tm=vp.Add<Tonemapping>(true);tm.mode.value=TonemappingMode.ACES;
var vg=new GameObject("08_Surface_Grading");vg.transform.SetParent(root);var volume=vg.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.sharedProfile=vp;
foreach(var c in vp.components){c.name=c.GetType().Name;AssetDatabase.AddObjectToAsset(c,vp);}
foreach(var l in root.GetComponentsInChildren<Light>()){
if(l.name=="Soft warm bounce"){l.color=new Color(1,.86f,.7f);l.intensity=65;}
if(l.name=="Front softbox bounce"){l.color=new Color(1,.92f,.8f);l.intensity=90;}
if(l.name=="Warm pendant"){l.color=new Color(1,.85f,.65f);l.intensity=20;}
}
var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Warm oak structural beams.mat");timber.SetColor("_BaseColor",new Color(.62f,.61f,.58f));
var boards=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Weathered honey timber.mat");boards.SetColor("_BaseColor",new Color(.78f,.82f,.8f));
var gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Golden maize.mat");gold.SetColor("_BaseColor",new Color(.92f,.64f,.16f));gold.SetFloat("_Smoothness",.15f);
var cloth=new Texture2D(512,512,TextureFormat.RGBA32,true);cloth.name="Fine natural canvas";cloth.filterMode=FilterMode.Trilinear;cloth.anisoLevel=16;
var cp=new Color[512*512];for(int y=0;y<512;y++)for(int x=0;x<512;x++){float n=Mathf.PerlinNoise(x*.12f,y*.12f);float weave=.025f*Mathf.Sin(x*Mathf.PI/4)*Mathf.Cos(y*Mathf.PI/4);float f=.85f+n*.09f+weave;cp[y*512+x]=new Color(f,f*.97f,f*.91f);}cloth.SetPixels(cp);cloth.Apply();AssetDatabase.CreateAsset(cloth,"Assets/NewMap/Textures/FineCanvas.asset");
var canvas=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Canvas beige.mat");canvas.SetTexture("_BaseColorMap",cloth);canvas.SetColor("_BaseColor",new Color(.78f,.69f,.52f));canvas.SetFloat("_UVBase",5);canvas.SetFloat("_TexWorldScale",3);canvas.SetTextureScale("_BaseColorMap",Vector2.one);HDMaterial.ValidateMaterial(canvas);
foreach(var t in root.Find("02_Hangar_Props").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("TFP_Sand_Bag")||t.name.StartsWith("TFP_Sack_Pile")||t.name.StartsWith("TFP_Cereal_Sack")||t.name.StartsWith("TFP_Industrial_Seed_Bag"))){
foreach(var r in t.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>canvas).ToArray();
}
// Reduce two full-width roof gaps to small, framed roof windows.
var hangar=root.Find("01_Modular_Hangar");
foreach(float z in new[]{-4f,6f})foreach(int side in new[]{-1,1}){
foreach(var range in new[]{new Vector2(0,3.2f),new Vector2(6.2f,10.5f)}){
float mid=(range.x+range.y)*.5f;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Skylight bay timber infill";go.transform.SetParent(hangar);go.transform.localPosition=new Vector3(side*mid,9.45f-mid*.265f,z);go.transform.localRotation=Quaternion.Euler(0,0,-side*14.843f);go.transform.localScale=new Vector3((range.y-range.x)*1.0345f,.14f,2.04f);go.GetComponent<Renderer>().sharedMaterial=boards;
}
}
foreach(var m in new[]{timber,boards,gold,canvas})EditorUtility.SetDirty(m);
foreach(var cam in root.GetComponentsInChildren<Camera>(true)){var hd=cam.GetComponent<HDAdditionalCameraData>();if(hd)hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;}
float len=0,grade=0;for(int i=1;i<road.Count;i++){var d=road[i]-road[i-1];len+=d.magnitude;grade=Mathf.Max(grade,Mathf.Abs(d.y)/new Vector2(d.x,d.z).magnitude);}
File.WriteAllText("output/NewMap/Revision2/layout.txt",string.Format(System.Globalization.CultureInfo.InvariantCulture,"Road {0:F1}m; estimated {1:F1}s at 8m/s; designed max grade {2:F1}%; village ground ~12m.",len,len/8,grade*100));
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return File.ReadAllText("output/NewMap/Revision2/layout.txt");
}
static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
}
