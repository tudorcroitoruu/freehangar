using System;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class DetailMap {
static Transform root,props;static System.Random rng=new System.Random(33);
static float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
static void Mesh(string name,Transform parent,List<Vector3> v,List<int> t,List<Vector2> uv,Material m,bool collision=false){
var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,"Assets/NewMap/Meshes/"+name+".asset");
var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;
}
static float Y(float x,float z){float a=1-Mathf.Pow((x-6)/5.2f,2)-Mathf.Pow((z-3)/10,2);float b=1-Mathf.Pow((x-7)/3.1f,2)-Mathf.Pow((z+5)/5,2);return Mathf.Max(a>0?5.8f*Mathf.Pow(a,1.2f):0,b>0?2.3f*Mathf.Pow(b,1.2f):0);}
static void Kernel(List<Vector3> v,List<int> t,List<Vector2> uv,Vector3 p,float scale=1){
int b=v.Count;float rx=R(.027f,.045f)*scale,rz=R(.035f,.055f)*scale,ry=R(.025f,.04f)*scale;v.Add(p+Vector3.up*ry);uv.Add(Vector2.zero);
for(int j=0;j<7;j++){float a=j*Mathf.PI*2/7;v.Add(p+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz));uv.Add(Vector2.zero);}
for(int j=0;j<7;j++)t.AddRange(new[]{b,b+1+(j+1)%7,b+1+j});
}
public static string Main(){
root=GameObject.Find("New_Map").transform;props=root.Find("02_Hangar_Props");
var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
foreach(var tm in root.GetComponentsInChildren<TMPro.TextMeshPro>(true)){
var text=tm.text;float size=tm.fontSize;var go=tm.gameObject;Object.DestroyImmediate(tm);
var t=go.AddComponent<TextMesh>();t.text=text;t.font=font;t.fontSize=64;t.characterSize=size*.025f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.96f,.85f,.64f);go.GetComponent<MeshRenderer>().sharedMaterial=font.material;
}
foreach(var l in root.GetComponentsInChildren<Light>()){
if(l.type==LightType.Directional){l.intensity=15;l.color=new Color(1,.92f,.78f);}
else if(l.name=="Soft warm bounce"||l.name=="Doorway sky fill")l.intensity=125;
else if(l.name=="Front softbox bounce")l.intensity=130;
else l.intensity=25;
}
foreach(var v in root.GetComponentsInChildren<Volume>()){
if(v.sharedProfile.TryGet<ColorAdjustments>(out var ca))ca.postExposure.Override(.15f);
}
var td=root.GetComponentInChildren<Terrain>().terrainData;var terrain=root.GetComponentInChildren<Terrain>();var hh=td.GetHeights(0,0,513,513);
for(int z=0;z<513;z++)for(int x=0;x<513;x++){
float wx=x*.75f-192,wz=z*.75f-70;float weight=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,40,Mathf.Abs(wx))))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(35,70,wz))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(270,295,wz)));
float baseY=Mathf.SmoothStep(0,5,Mathf.InverseLerp(60,240,wz));hh[z,x]=Mathf.Lerp(hh[z,x],(baseY+8)/80,weight);
}td.SetHeights(0,0,hh);
foreach(var tr in root.Find("05_Landscape").Cast<Transform>())if(tr.name.StartsWith("TFP_")){var p=tr.position;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;tr.position=p;}
var wheat=AssetDatabase.FindAssets("TFP_Wheat_Patch_01A t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.EndsWith("TFP_Wheat_Patch_01A.prefab"));
for(int i=0;i<180;i++){var p=new Vector3(R(-100,-38),0,R(59,120));p.y=terrain.SampleHeight(p)-8;var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(wheat),root.Find("05_Landscape"));g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,R(0,180),0);g.transform.localScale=Vector3.one*2;}
var old=props.Cast<Transform>().First(t=>t.name=="TFP_Cereal_Sack_01A"&&t.position.z<0);old.gameObject.SetActive(false);
var sack=new GameObject("Open filling sack");sack.transform.SetParent(props,false);sack.transform.localPosition=new Vector3(-.65f,.08f,-5.6f);
var verts=new List<Vector3>();var tris=new List<int>();var uvs=new List<Vector2>();
int n=32;float[] ys={0,.2f,.75f,1.16f,1.35f,1.34f,1.1f};float[] rs={.5f,.58f,.57f,.49f,.54f,.5f,.45f};
for(int r=0;r<ys.Length;r++)for(int j=0;j<=n;j++){float a=j*Mathf.PI*2/n;float rr=rs[r]*(1+.05f*Mathf.Sin(j*2.3f));float y=ys[r]+(r>=3?.04f*Mathf.Sin(j*1.7f):0);verts.Add(new Vector3(Mathf.Sin(a)*rr,y,Mathf.Cos(a)*rr));uvs.Add(new Vector2(j/(float)n,ys[r]));if(r<ys.Length-1&&j<n){int k=r*(n+1)+j;tris.AddRange(new[]{k,k+1,k+n+1,k+1,k+n+2,k+n+1});}}
Mesh("Open burlap sack",sack.transform,verts,tris,uvs,AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Canvas beige.mat"),true);
var grain=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Individual maize kernels.mat");grain.SetColor("_BaseColor",new Color(1,.56f,.025f));
verts=new List<Vector3>();tris=new List<int>();uvs=new List<Vector2>();
for(int i=0;i<450;i++){float a=R(0,6.28f),r=Mathf.Sqrt(R(0,1))*.46f;Kernel(verts,tris,uvs,new Vector3(Mathf.Sin(a)*r,1.13f+.1f*(1-r/.46f),Mathf.Cos(a)*r),.8f);}
Mesh("Maize inside filling sack",sack.transform,verts,tris,uvs,grain);
foreach(var tr in props.Find("Golden maize stockpile").Cast<Transform>().Where(t=>t.name.StartsWith("Maize kernels batch")).ToArray())tr.gameObject.SetActive(false);
for(int b=0;b<6;b++){verts=new List<Vector3>();tris=new List<int>();uvs=new List<Vector2>();
for(int i=0;i<6000;i++){float x=R(-2,9.4f),z=R(-9.5f,12.5f),y=Y(x,z);if(y<.02f&&rng.NextDouble()>.2)continue;Kernel(verts,tris,uvs,new Vector3(x,y+.06f,z));}
Mesh("Dense maize surface "+b,props.Find("Golden maize stockpile"),verts,tris,uvs,grain);
}
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Lighting, signage, sightline, sack and grain refined.";
}
}

