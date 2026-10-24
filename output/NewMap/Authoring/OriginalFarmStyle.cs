using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class OriginalFarmStyle {
const string P="Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/";const string A="Assets/NewMap/OriginalStyle";
static Transform root;static Terrain terrain;static Vector3[] road;static float[] lengths;static Material atlas;static System.Random rng;
static float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
static void Init(){if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");root=GameObject.Find("New_Map").transform;terrain=root.GetComponentInChildren<Terrain>();if(root.position!=Vector3.zero)throw new Exception("New_Map moved; inspect coordinates first.");road=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(l=>{var s=l.Split(',');return new Vector3(float.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[2],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();lengths=new float[road.Length];for(int i=1;i<road.Length;i++)lengths[i]=lengths[i-1]+Vector3.Distance(road[i],road[i-1]);atlas=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/TFP_Atlas_1A - NewMap quality.mat");rng=new System.Random(241024);Directory.CreateDirectory(A);Directory.CreateDirectory("output/NewMap/OriginalStyle");AssetDatabase.Refresh();}
static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
static Vector3 Point(float s){s=Mathf.Clamp(s,0,lengths.Last());int i=Array.BinarySearch(lengths,s);if(i>=0)return road[i];i=~i;return Vector3.Lerp(road[i-1],road[i],(s-lengths[i-1])/(lengths[i]-lengths[i-1]));}
static Vector3 Direction(float s){var d=Point(s+.5f)-Point(s-.5f);d.y=0;return d.normalized;}
static float Distance(Vector3 p){float best=float.MaxValue;for(int i=1;i<road.Length;i++){var a=new Vector2(road[i-1].x,road[i-1].z);var d=new Vector2(road[i].x,road[i].z)-a;var q=new Vector2(p.x,p.z)-a;best=Mathf.Min(best,(q-d*Mathf.Clamp01(Vector2.Dot(q,d)/d.sqrMagnitude)).sqrMagnitude);}return Mathf.Sqrt(best);}
static Transform Group(string n,Transform p){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
static GameObject Prefab(string p,Transform parent,Vector3 point,float yaw,Vector3 scale){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P+p+".prefab"),parent);g.transform.position=point;g.transform.rotation=Quaternion.Euler(0,yaw,0);g.transform.localScale=scale;foreach(var r in g.GetComponentsInChildren<Renderer>(true)){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++)if(ms[i]&&ms[i].name=="TFP_Atlas_1A")ms[i]=atlas;r.sharedMaterials=ms;}return g;}
static void Save(){AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());}
public static string GroundAndRoad(){
Init();if(root.Find("09_Original_Road_Details"))throw new Exception("Already authored");
var old=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t!=terrain);var indices=new[]{0,1,2,4,5,6};var layers=new TerrainLayer[indices.Length];AssetDatabase.StartAssetEditing();try{for(int i=0;i<indices.Length;i++){layers[i]=Object.Instantiate(old.terrainData.terrainLayers[indices[i]]);layers[i].name="Original farm layer "+i;AssetDatabase.CreateAsset(layers[i],A+"/"+layers[i].name+".terrainlayer");}}finally{AssetDatabase.StopAssetEditing();}
var td=terrain.terrainData;td.terrainLayers=layers;int n=1024;td.alphamapResolution=n;var splat=new float[n,n,6];
for(int z=0;z<n;z++)for(int x=0;x<n;x++){
var p=new Vector3(x/(float)(n-1)*384-192,0,z/(float)(n-1)*384-70);float d=Distance(p);float noise=Mathf.PerlinNoise(p.x*.19f+300,p.z*.19f+500);float fine=Mathf.PerlinNoise(p.x*.7f+12,p.z*.7f+55);
float track=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.7f,7.2f,d+(noise-.5f)*1.8f));
float yard=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,35,new Vector2(p.x,p.z-14).magnitude+(noise-.5f)*3));
float station=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,15,new Vector2(p.x-25,p.z-39).magnitude));
float village=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,32,new Vector2(p.x-8,p.z-175).magnitude));
float dirt=Mathf.Max(track,Mathf.Max(yard,Mathf.Max(station,village)));float field=(p.x>-100&&p.x<-34&&p.z>55&&p.z<125)?.85f:0;
float rockiness=Mathf.Lerp(.12f,.43f,fine)*dirt;float soil=dirt*.13f*noise;float meadow=(1-dirt)*(1-field);float variation=Mathf.PerlinNoise(p.x*.06f+100,p.z*.06f+500);
splat[z,x,0]=meadow*(.55f+.3f*variation);splat[z,x,4]=meadow*(.45f-.3f*variation);splat[z,x,1]=dirt-rockiness-soil;splat[z,x,2]=rockiness;splat[z,x,3]=(1-dirt)*field;splat[z,x,5]=soil;
}td.SetAlphamaps(0,0,splat);
var details=Group("09_Original_Road_Details",root);var tracks=Group("Curved dirt ruts - original TFP meshes",details);
int count=0,rocks=0;AssetDatabase.StartAssetEditing();try{
for(float s=10;s<lengths.Last()-8;s+=11){
var d0=Direction(s-5);var d1=Direction(s+5);bool curve=Vector3.Angle(d0,d1)>18;string name=curve?"TFP_Dirt_Road_Curve_01A":(count%2==0?"TFP_Dirt_Road_Straight_01A":"TFP_Dirt_Road_Straight_02A");
var source=AssetDatabase.LoadAssetAtPath<GameObject>(P+"Ground/"+name+".prefab").GetComponentInChildren<MeshFilter>().sharedMesh;var mesh=Object.Instantiate(source);mesh.name="Conformed rut "+count;var vertices=mesh.vertices;var bounds=source.bounds;var origin=Point(s);origin.y=Ground(origin);
for(int j=0;j<vertices.Length;j++){var v=vertices[j];float at=s+(v.z-bounds.center.z)*(curve?1.35f:.94f);var p=Point(at);var tangent=Direction(at);var side=new Vector3(tangent.z,0,-tangent.x);p+=side*(v.x-bounds.center.x)*(curve?.7f:1.1f);p.y=Ground(p)+.025f+v.y*.8f;vertices[j]=p-origin;}
mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,A+"/Rut "+count+".asset");var g=Group(name+" - conformed "+count,tracks);g.position=origin;g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=atlas;count++;
}
var rockParent=Group("Rocky road shoulders",details);
for(float s=15;s<lengths.Last()-15;s+=5.8f){var p=Point(s);var d=Direction(s);var side=new Vector3(d.z,0,-d.x)*(rng.Next(2)==0?-1:1);p+=side*R(4.6f,7.3f);p.y=Ground(p)-.045f;float size=R(.14f,.36f);var g=Prefab("Vegetation/Rocks/"+(rocks%3==0?"TFP_Rocks_02A":"TFP_Rock_01A"),rockParent,p,R(0,360),new Vector3(size,size*.5f,size));foreach(var c in g.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);rocks++;}
}finally{AssetDatabase.StopAssetEditing();}
Save();return "Original terrain layers blended at 1024 alpha resolution; "+count+" conforming straight/curved rut pieces and "+rocks+" roadside rock clusters.";
}
static float BarnY(float y)=>y<=4?y*1.7f:6.8f+(y-4)*.95f;
static void FitBarn(GameObject g,string prefix){
var changed=new Dictionary<Mesh,Mesh>();foreach(var f in g.GetComponentsInChildren<MeshFilter>(true)){
var source=f.sharedMesh;var mesh=Object.Instantiate(source);var verts=mesh.vertices;var toRoot=g.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;var fromRoot=toRoot.inverse;for(int i=0;i<verts.Length;i++){var v=toRoot.MultiplyPoint3x4(verts[i]);v.y=BarnY(v.y);verts[i]=fromRoot.MultiplyPoint3x4(v);}mesh.vertices=verts;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.name=prefix+" "+f.name;AssetDatabase.CreateAsset(mesh,A+"/"+mesh.name+".asset");f.sharedMesh=mesh;changed[source]=mesh;
}
foreach(var c in g.GetComponentsInChildren<MeshCollider>())if(changed.TryGetValue(c.sharedMesh,out var replacement)){c.sharedMesh=null;c.sharedMesh=replacement;}
foreach(var c in g.GetComponentsInChildren<BoxCollider>()){var center=c.center;float a=BarnY(center.y-c.size.y*.5f),b=BarnY(center.y+c.size.y*.5f);center.y=(a+b)*.5f;c.center=center;var size=c.size;size.y=b-a;c.size=size;}
foreach(var l in g.GetComponentsInChildren<LODGroup>())l.RecalculateBounds();
}
public static string Barn(){
Init();var h=root.Find("01_Modular_Hangar");if(h.Find("Red barn exterior - TFP_Barn_04A"))throw new Exception("Already authored");var study=root.Find("RedExterior_Study");if(study)Object.DestroyImmediate(study.gameObject);var shell=Group("Red barn exterior - TFP_Barn_04A",h);
AssetDatabase.StartAssetEditing();try{
var main=Prefab("Buildings/Barn Presets/TFP_Barn_04A",shell,Vector3.zero,0,new Vector3(1.6f,1,1.6f));FitBarn(main,"Red barn core");
main.transform.Find("TFP_Barn_04A_Door_03").localRotation=Quaternion.Euler(0,105,0);main.transform.Find("TFP_Barn_04A_Door_04").localRotation=Quaternion.Euler(0,-105,0);
foreach(int side in new[]{-1,1}){var ext=Prefab("Buildings/Barn Presets/TFP_Barn_04A_Extension",shell,new Vector3(side*14.5f,0,0),side==1?0:180,new Vector3(1,1,1.6f));FitBarn(ext,"Red barn wing "+side);}
}finally{AssetDatabase.StopAssetEditing();}
foreach(var t in h.Cast<Transform>().Where(t=>t.name.StartsWith("Open sliding door")||t.name=="Entrance side plank"||t.name=="Entrance gable plank"))t.gameObject.SetActive(false);
Save();return "Original red TFP_Barn_04A exterior and two wings fitted around timber interior; front doors open.";
}
public static string Power(){
Init();if(root.Find("10_Power_Line"))throw new Exception("Already authored");var power=Group("10_Power_Line",root);foreach(var t in root.Find("05_Landscape").Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Wooden_Electricity_Pole")))t.gameObject.SetActive(false);
var poles=new List<Transform>();Physics.SyncTransforms();
for(float s=8;s<lengths.Last()-4;s+=25){var p=Point(s);var d=Direction(s);var side=new Vector3(-d.z,0,d.x);Vector3 candidate=p;bool found=false;
foreach(float offset in new[]{8.5f,10.5f,12.5f,-8.5f,-10.5f}){candidate=p+side*offset;candidate.y=Ground(candidate);if(!Physics.OverlapSphere(candidate+Vector3.up*1.7f,1.1f,~0,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider))){found=true;break;}}
if(!found)continue;var g=Prefab("Props/Fence Kits/TFP_Wooden_Electricity_Pole_01A",power,candidate,0,Vector3.one);g.name="TFP_Wooden_Electricity_Pole_01A - route "+poles.Count.ToString("00");g.transform.rotation=Quaternion.LookRotation(new Vector3(-d.z,0,d.x));poles.Add(g.transform);
}
var cablePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(P+"Props/Fence Kits/TFP_Electricity_Pole_Cable_01A.prefab");var cableSource=(GameObject)PrefabUtility.InstantiatePrefab(cablePrefab);cableSource.hideFlags=HideFlags.HideAndDontSave;var skin=cableSource.GetComponent<SkinnedMeshRenderer>();skin.SetBlendShapeWeight(0,100);var baseMesh=new Mesh();skin.BakeMesh(baseMesh);Object.DestroyImmediate(cableSource);
float minClearance=999;AssetDatabase.StartAssetEditing();try{for(int i=1;i<poles.Count;i++){
var a=poles[i-1];var b=poles[i];var mesh=Object.Instantiate(baseMesh);mesh.name="TFP cable fitted span "+i;var vertices=mesh.vertices;float x0=baseMesh.bounds.max.x,x1=baseMesh.bounds.min.x;var origin=a.position;float span=Vector3.Distance(a.position,b.position);float sagScale=Mathf.Clamp(span/24,.65f,1.6f);
for(int j=0;j<vertices.Length;j++){var v=vertices[j];float t=Mathf.InverseLerp(x0,x1,v.x);var pa=a.TransformPoint(new Vector3(-.57272f,8.84f,v.z));var pb=b.TransformPoint(new Vector3(-.57272f,8.84f,v.z));var world=Vector3.Lerp(pa,pb,t)+Vector3.up*((v.y-8.84f)*sagScale);vertices[j]=world-origin;minClearance=Mathf.Min(minClearance,world.y-Ground(world));}
mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,A+"/"+mesh.name+".asset");var g=Group("TFP_Electricity_Pole_Cable_01A - span "+i,power);g.position=origin;g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=atlas;
}
// A three-wire service drop visibly connects the first route pole to the hangar wall.
var wire=new Material(Shader.Find("HDRP/Lit")){name="Service cable dark rubber"};wire.SetColor("_BaseColor",new Color(.035f,.028f,.021f));wire.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(wire,A+"/Service cable.mat");
for(int cable=0;cable<3;cable++){var a=new Vector3(-10.6f,8.4f,14.7f+cable*.18f);var b=poles[0].TransformPoint(new Vector3(-.57272f,8.84f,-1.33f+cable*1.33f));var go=Group("Hangar service drop "+(cable+1),power);var lr=go.gameObject.AddComponent<LineRenderer>();lr.sharedMaterial=wire;lr.useWorldSpace=true;lr.positionCount=25;lr.startWidth=lr.endWidth=.045f;lr.numCornerVertices=3;for(int j=0;j<25;j++){float t=j/24f;lr.SetPosition(j,Vector3.Lerp(a,b,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*.65f));}}
}finally{AssetDatabase.StopAssetEditing();Object.DestroyImmediate(baseMesh);}
Save();string report=poles.Count+" original wooden poles; "+(poles.Count-1)+" fitted native cable spans; three service drops to hangar. Minimum sampled route wire ground clearance "+minClearance.ToString("F2")+"m.";File.WriteAllText("output/NewMap/OriginalStyle/power-line.txt",report);return report;
}
}
