using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class DenseHillsideVegetation {
static System.Random rng=new System.Random(241026);static Terrain terrain;static Vector3[] road;
static float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
static float RoadDistance(Vector3 p){float best=float.MaxValue;for(int i=1;i<road.Length;i++){var a=new Vector2(road[i-1].x,road[i-1].z);var d=new Vector2(road[i].x,road[i].z)-a;var q=new Vector2(p.x,p.z)-a;float f=Mathf.Clamp01(Vector2.Dot(q,d)/d.sqrMagnitude);best=Mathf.Min(best,(q-d*f).sqrMagnitude);}return Mathf.Sqrt(best);}
static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
static bool Developed(Vector3 p,float padding){
if(new Vector2(p.x,p.z-14).magnitude<35+padding)return true;
if(new Vector2(p.x-25,p.z-39).magnitude<11+padding)return true;
if(new Vector2(p.x-8,p.z-175).magnitude<43+padding)return true;
if(p.x>-105-padding&&p.x<-31+padding&&p.z>52-padding&&p.z<128+padding)return true;
if(p.x>-48-padding&&p.x<-13+padding&&p.z>18-padding&&p.z<52+padding)return true;
return false;
}
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Wrong scene");
var root=GameObject.Find("New_Map").transform;var land=root.Find("05_Landscape");if(land.Find("Dense meadow and hill groves"))throw new Exception("Vegetation pass already exists");
EditorSceneManager.SaveScene(scene);File.Copy(scene.path,"Temp/NewMap/Gameplay.before-dense-vegetation.unity",true);
terrain=root.GetComponentInChildren<Terrain>();road=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(line=>{var s=line.Split(',');return new Vector3(float.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[2],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();
var parent=new GameObject("Dense meadow and hill groves").transform;parent.SetParent(land,false);
var treeParent=new GameObject("Uphill tree groves").transform;treeParent.SetParent(parent,false);
var templates=new[]{"TFP_Beech_Tree_01A","TFP_Beech_Tree_02A"}.Select(n=>land.Cast<Transform>().First(t=>t.name==n&&t.gameObject.activeSelf)).ToArray();
var occupied=land.GetComponentsInChildren<LODGroup>().Where(g=>g.name.Contains("Tree")).Select(g=>g.transform.position).ToList();
var centers=new[]{new Vector2(-76,166),new Vector2(-122,222),new Vector2(-63,253),new Vector2(-10,249),new Vector2(49,245),new Vector2(108,222),new Vector2(111,167),new Vector2(68,121),new Vector2(-43,110)};
int trees=0;
for(int attempt=0;attempt<6000&&trees<150;attempt++){
var center=centers[rng.Next(centers.Length)];float a=R(0,Mathf.PI*2),r=Mathf.Sqrt(R(0,1))*30;var p=new Vector3(center.x+Mathf.Cos(a)*r,0,center.y+Mathf.Sin(a)*r);
if(p.x<-155||p.x>155||p.z<85||p.z>285||Developed(p,4)||RoadDistance(p)<10)continue;
// Retain the doorway-to-village sight line, with trees framing both sides.
if(p.z<205&&p.x>-28&&p.x<32)continue;
if(occupied.Any(q=>new Vector2(p.x-q.x,p.z-q.z).sqrMagnitude<49))continue;
p.y=Ground(p);var source=templates[rng.Next(templates.Length)];var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject)),treeParent);
g.name=source.name+" - Hill "+trees.ToString("000");g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,R(0,360),0);float scale=source==templates[0]?R(1.05f,1.6f):R(1.3f,1.9f);g.transform.localScale=Vector3.one*scale;
var mats=source.GetComponentsInChildren<Renderer>(true).ToDictionary(x=>x.name,x=>x.sharedMaterials);foreach(var renderer in g.GetComponentsInChildren<Renderer>(true))if(mats.TryGetValue(renderer.name,out var m))renderer.sharedMaterials=m;
occupied.Add(p);trees++;
}
var grassSources=new[]{"TFP_Grass_Patch_03A","TFP_Grass_Patch_01A"}.Select(n=>land.Cast<Transform>().First(t=>t.name==n&&t.gameObject.activeSelf)).ToArray();
var near=grassSources.Select(t=>t.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith("LOD1"))).ToArray();
var far=grassSources.Select(t=>t.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith("LOD3"))).ToArray();
string dir="Assets/NewMap/Meshes/DenseMeadow";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
int patches=0,chunks=0;long vertices=0;
for(int cz=0;cz<16;cz++)for(int cx=0;cx<18;cx++){
Vector3 origin=new Vector3(-162+cx*18,0,12+cz*18);var lists=new[]{new List<CombineInstance>(),new List<CombineInstance>()};var distant=new[]{new List<CombineInstance>(),new List<CombineInstance>()};
for(int attempt=0;attempt<160;attempt++){
var p=origin+new Vector3(R(0,18),0,R(0,18));float distance=RoadDistance(p);if(distance<6.7f||Developed(p,1))continue;
float noise=Mathf.PerlinNoise(p.x*.035f+40,p.z*.035f+70);float chance=distance<29?.94f:Mathf.Lerp(.2f,.72f,noise);if(R(0,1)>chance)continue;
int kind=rng.NextDouble()<.77?0:1;p.y=Ground(p)-.025f;float size=kind==0?R(.8f,1.3f):R(1.1f,1.65f);float height=R(.65f,1.1f);
var normal=terrain.terrainData.GetInterpolatedNormal((p.x+192)/384,(p.z+70)/384);var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,R(0,360),0);
var mat=Matrix4x4.TRS(p-origin,rotation,new Vector3(size,height,size))*grassSources[kind].worldToLocalMatrix*near[kind].transform.localToWorldMatrix;
lists[kind].Add(new CombineInstance{mesh=near[kind].sharedMesh,transform=mat});distant[kind].Add(new CombineInstance{mesh=far[kind].sharedMesh,transform=mat});patches++;
}
if(lists.Sum(l=>l.Count)==0)continue;
var group=new GameObject("Meadow cell "+cx.ToString("00")+"-"+cz.ToString("00")).transform;group.SetParent(parent,false);group.position=origin;
var levels=new[]{new List<Renderer>(),new List<Renderer>()};
for(int lod=0;lod<2;lod++)for(int kind=0;kind<2;kind++){
var list=lod==0?lists[kind]:distant[kind];if(list.Count==0)continue;var mesh=new Mesh{name="Meadow "+cx+"_"+cz+"_"+kind+"_LOD"+lod,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(list.ToArray(),true,true,false);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,dir+"/"+mesh.name+".asset");vertices+=mesh.vertexCount;
var obj=new GameObject("Grass "+kind+" LOD"+lod);obj.transform.SetParent(group,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=near[kind].GetComponent<Renderer>().sharedMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;levels[lod].Add(renderer);
}
var lodGroup=group.gameObject.AddComponent<LODGroup>();lodGroup.SetLODs(new[]{new LOD(.7f,levels[0].ToArray()),new LOD(.035f,levels[1].ToArray())});lodGroup.fadeMode=LODFadeMode.None;lodGroup.RecalculateBounds();chunks++;
}
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Directory.CreateDirectory("output/NewMap/Vegetation");string report="Added "+trees+" uphill trees and "+patches+" grass patches, batched into "+chunks+" spatial cells with two LODs. Total stored mesh vertices (both LODs): "+vertices+". Road, farmyard, crop field and village footprint excluded; central hangar sightline preserved.";File.WriteAllText("output/NewMap/Vegetation/summary.txt",report);return report;
}
}
