using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class SoftenRoadAndFarm {
static Terrain terrain;
static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
static float Shape(float x,float z){float dx=(x+67)/33f,dz=(z-90)/35f;float a=Mathf.Atan2(dz,dx);return Mathf.Sqrt(dx*dx+dz*dz)/(1+.055f*Mathf.Sin(a*3+.8f)+.035f*Mathf.Sin(a*5));}
static Vector3 Edge(float angle){float a=angle*Mathf.Deg2Rad,r=1+.055f*Mathf.Sin(a*3+.8f)+.035f*Mathf.Sin(a*5);var p=new Vector3(-67+Mathf.Cos(a)*(33*r+4),0,90+Mathf.Sin(a)*(35*r+4));p.y=Ground(p);return p;}
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var root=GameObject.Find("New_Map").transform;var land=root.Find("05_Landscape");if(land.Find("Rounded field boundary"))throw new Exception("Already authored");terrain=root.GetComponentInChildren<Terrain>();var td=terrain.terrainData;
Directory.CreateDirectory("output/NewMap/RoadRefinement");Directory.CreateDirectory("Temp/NewMap");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());File.Copy("Assets/Scenes/Gameplay.unity","Temp/NewMap/Gameplay.before-road-refinement.unity",true);
var old=td.GetAlphamaps(0,0,td.alphamapWidth,td.alphamapHeight);var layers=td.terrainLayers.ToList();layers.Add(AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/NewMap/Terrain/Farm dirt.terrainlayer"));td.terrainLayers=layers.ToArray();int n=td.alphamapWidth;var alpha=new float[n,n,7];
for(int z=0;z<n;z++)for(int x=0;x<n;x++){
float wx=terrain.transform.position.x+x/(float)(n-1)*td.size.x,wz=terrain.transform.position.z+z/(float)(n-1)*td.size.z;
float dirt=old[z,x,1]+old[z,x,2]+old[z,x,5];float field=.85f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.87f,1.05f,Shape(wx,wz))));float meadow=(1-dirt)*(1-field);float variation=Mathf.PerlinNoise(wx*.06f+100,wz*.06f+500);
alpha[z,x,0]=meadow*(.55f+.3f*variation);alpha[z,x,4]=meadow*(.45f-.3f*variation);alpha[z,x,3]=(1-dirt)*field;
alpha[z,x,1]=dirt*.30f;alpha[z,x,2]=dirt*.08f;alpha[z,x,5]=dirt*.02f;alpha[z,x,6]=dirt*.60f;
}td.SetAlphamaps(0,0,alpha);
var tracks=root.Find("09_Original_Road_Details/Curved dirt ruts - original TFP meshes");int active=0;int[] keep={3,10,18,27,36,43};for(int i=0;i<tracks.childCount;i++){bool on=keep.Contains(i);tracks.GetChild(i).gameObject.SetActive(on);if(on)active++;}
var fence=land.Cast<Transform>().Where(t=>t.name=="Fence post"||t.name=="Fence rail").ToArray();var post=fence.First(t=>t.name=="Fence post");var rail=fence.First(t=>t.name=="Fence rail");var group=new GameObject("Rounded field boundary").transform;group.SetParent(land,false);Vector3 prev=Vector3.zero;int posts=0;
for(int i=0;i<=43;i++){var p=Edge(Mathf.Lerp(90,295,i/43f));var g=UnityEngine.Object.Instantiate(post.gameObject,group);g.SetActive(true);g.name="Curved field fence post";g.transform.position=p+Vector3.up*.7f;posts++;if(i>0)foreach(float h in new[]{.55f,1.12f}){var a=prev+Vector3.up*h;var b=p+Vector3.up*h;var beam=UnityEngine.Object.Instantiate(rail.gameObject,group);beam.SetActive(true);beam.name="Curved field fence rail";beam.transform.position=(a+b)*.5f;beam.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);beam.transform.localScale=new Vector3(.12f,(b-a).magnitude,.12f);}prev=p;}
foreach(var f in fence)f.gameObject.SetActive(false);
int moved=0;foreach(var w in land.Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Wheat_Patch"))){float q=Shape(w.position.x,w.position.z);if(q>.83f){var p=w.position;float factor=.79f/q;p.x=-67+(p.x+67)*factor;p.z=90+(p.z-90)*factor;p.y=Ground(p);w.position=p;moved++;}}
var grassSource=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Vegetation/Grass/TFP_Grass_Patch_03A.prefab");if(!grassSource){var guid=AssetDatabase.FindAssets("TFP_Grass_Patch_03A t:Prefab").First();grassSource=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));}
var verge=new GameObject("Soft field verge").transform;verge.SetParent(land,false);var rng=new System.Random(2468);int grass=0;for(int i=0;i<550;i++){float x=Mathf.Lerp(-103,-32,(float)rng.NextDouble()),z=Mathf.Lerp(52,128,(float)rng.NextDouble());float s=Shape(x,z);if(s<1.01f||s>1.47f)continue;var p=new Vector3(x,0,z);p.y=Ground(p);var g=(GameObject)PrefabUtility.InstantiatePrefab(grassSource,verge);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);g.transform.localScale=Vector3.one*Mathf.Lerp(1.2f,2,(float)rng.NextDouble());grass++;}
EditorUtility.SetDirty(td);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());string report=active+" separated rut sections (previously 30 active); brighter road blend 60% prior packed earth, 30% original dirt, 8% stone, 2% soil. Rounded irregular field with feathered edge, "+posts+" curved fence posts; "+moved+" crop patches moved inside boundary; "+grass+" verge patches. Terrain heights and road route unchanged.";File.WriteAllText("output/NewMap/RoadRefinement/summary.txt",report);return report;
}
}
