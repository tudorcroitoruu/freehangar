using System;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using Object=UnityEngine.Object;
public static class PolishMap {
static string PathOf(string name)=>AssetDatabase.FindAssets(name+" t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p=>System.IO.Path.GetFileNameWithoutExtension(p)==name);
static GameObject Spawn(string n,Transform p,Vector3 pos,float scale=1){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(n)),p);g.transform.localPosition=pos;g.transform.localScale=Vector3.one*scale;return g;}
public static string Main(){
AssetDatabase.Refresh();
var root=GameObject.Find("New_Map").transform;var terrain=root.GetComponentInChildren<Terrain>();int index=0;
foreach(var sign in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Sign - ")).ToArray()){
string path="Assets/NewMap/SignTextures/sign-"+index+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureCompression=TextureImporterCompression.Uncompressed;imp.mipmapEnabled=true;imp.SaveAndReimport();
var mat=new Material(Shader.Find("HDRP/Lit"));mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(mat,"Assets/NewMap/Materials/SignPanel "+index+++".mat");
sign.Find("Painted panel").GetComponent<Renderer>().sharedMaterial=mat;
foreach(Transform child in sign)if(child.name!="Painted panel"&&child.name!="Timber frame")child.gameObject.SetActive(false);
}
foreach(var s in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Modular silo ")).ToArray()){
var middle=s.Cast<Transform>().First(t=>t.name.Contains("_Middle"));var lid=s.Cast<Transform>().First(t=>t.name.Contains("_Lid"));var basePart=s.Cast<Transform>().First(t=>t.name.Contains("_Base"));
var baseBounds=basePart.GetComponentsInChildren<Renderer>().First().bounds;var midBounds=middle.GetComponentsInChildren<Renderer>().First().bounds;
float bottom=(baseBounds.max.y-s.position.y)/s.lossyScale.y;float height=midBounds.size.y/s.lossyScale.y;middle.localPosition=new Vector3(0,bottom,0);
int rings=12;for(int i=1;i<rings;i++){var ring=Object.Instantiate(middle.gameObject,s);ring.name=middle.name+" ring "+i;ring.transform.localPosition=new Vector3(0,bottom+i*height,0);}
lid.localPosition=new Vector3(0,bottom+rings*height,0);s.localScale=new Vector3(s.localScale.x*1.65f,s.localScale.y,s.localScale.z*1.65f);
}
var props=root.Find("02_Hangar_Props");var rack=props.Find("Container shelves - reference left");
foreach(var t in rack.Cast<Transform>().Where(t=>t.name=="Oak upright").ToArray()){var pos=t.localPosition;t.localScale=new Vector3(.16f,4.1f,.17f);pos.z=-.54f;t.localPosition=pos;var back=Object.Instantiate(t.gameObject,rack);back.name="Rear oak upright";back.transform.localPosition=new Vector3(pos.x,pos.y,.54f);}
var crate=rack.Cast<Transform>().First(t=>t.name=="TFP_Wooden_Crate_01A");crate.localScale=Vector3.one*1.9f;
props.Find("Open filling sack").Find("Maize inside filling sack").localPosition=Vector3.up*.12f;
var landscape=root.Find("05_Landscape");var random=new System.Random(890);Func<float,float,float> rnd=(a,b)=>a+(float)random.NextDouble()*(b-a);
for(int i=0;i<85;i++){
float x=rnd(-140,140),z=rnd(25,295);if(Mathf.Abs(x)<28&&z<240)continue;if(x>-110&&x<-30&&z>50&&z<130)continue;
var p=new Vector3(x,0,z);p.y=terrain.SampleHeight(p)-8;var tree=Spawn("TFP_Beech_Tree_0"+(i%3+1)+"A",landscape,p,rnd(1.4f,2.3f));tree.transform.localRotation=Quaternion.Euler(0,rnd(0,360),0);
}
for(int i=0;i<250;i++){
float a=rnd(0,Mathf.PI*2),r=rnd(29,48);var p=new Vector3(Mathf.Cos(a)*r,0,14+Mathf.Sin(a)*r);if(p.z>25&&Mathf.Abs(p.x)<12)continue;
p.y=terrain.SampleHeight(p)-8;Spawn(i%5==0?"TFP_Bush_01A":"TFP_Grass_Patch_01A",landscape,p,rnd(.6f,1.4f));
}
foreach(var t in landscape.Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Wheat_Patch")))t.localScale=Vector3.one*1.1f;
var yard=root.Find("03_Farmyard");var truck=yard.Find("Weigh station").Find("TFP_Utility_Pick_Up_02B");var bed=Spawn("TFP_Utility_Pick_Up_02A_Wooden_Bed",truck.parent,truck.localPosition);bed.transform.localRotation=truck.localRotation;
foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>!t.gameObject.activeSelf&&(t.name.StartsWith("TFP_Hall_Arch")||t.name.StartsWith("Maize kernels batch"))).ToArray())Object.DestroyImmediate(t.gameObject);
var ext=root.Find("07_Reference_Views").Find("Exterior Review");ext.position=new Vector3(2,2.1f,43);ext.LookAt(new Vector3(7,3,16));ext.GetComponent<Camera>().fieldOfView=65;
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Silos, signs and farmyard complete.";
}
}
