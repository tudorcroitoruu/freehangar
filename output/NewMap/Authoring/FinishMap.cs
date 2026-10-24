using System;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class FinishMap {
public static string Main(){
var root=GameObject.Find("New_Map").transform;var p=root.Find("02_Hangar_Props");
foreach(var m in AssetDatabase.FindAssets("t:Material",new[]{"Assets/NewMap/Materials"}).Select(AssetDatabase.GUIDToAssetPath).Where(s=>s.Contains("SignPanel"))){var mat=AssetDatabase.LoadAssetAtPath<Material>(m);mat.SetTextureScale("_BaseColorMap",new Vector2(-1,-1));mat.SetTextureOffset("_BaseColorMap",Vector2.one);EditorUtility.SetDirty(mat);}
foreach(var t in p.Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Utility_Pick_Up_02A")||(t.name=="TFP_Cereal_Sack_01A"&&t.position.z>5))){t.localPosition+=new Vector3(-3.2f,0,-1);}
p.Find("TFP_Wheelbarrow_01A").localPosition=new Vector3(-2.3f,.06f,-.4f);
var terrain=root.GetComponentInChildren<Terrain>();var td=terrain.terrainData;var weigh=root.Find("03_Farmyard/Weigh station");weigh.localPosition=new Vector3(-5,0,51);
var heights=td.GetHeights(0,0,513,513);for(int z=0;z<513;z++)for(int x=0;x<513;x++){float wx=x*.75f-192,wz=z*.75f-70;float d=new Vector2(wx+5,wz-51).magnitude;float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(10,19,d));heights[z,x]=Mathf.Lerp(heights[z,x],8f/80,w);}td.SetHeights(0,0,heights);
var alpha=td.GetAlphamaps(0,0,512,512);for(int z=0;z<512;z++)for(int x=0;x<512;x++){float wx=x/511f*384-192,wz=z/511f*384-70;float d=new Vector2(wx+5,wz-51).magnitude;float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,12,d));alpha[z,x,0]*=1-w;alpha[z,x,2]*=1-w;alpha[z,x,1]=1-alpha[z,x,0]-alpha[z,x,2];}td.SetAlphamaps(0,0,alpha);
foreach(var t in root.Find("05_Landscape").Cast<Transform>().Where(t=>t.name.StartsWith("TFP_")).ToArray()){
if(new Vector2(t.position.x+5,t.position.z-51).magnitude<10){t.gameObject.SetActive(false);continue;}
var pos=t.position;pos.y=terrain.SampleHeight(pos)-8;t.position=pos;
}
var ext=root.Find("07_Reference_Views/Exterior Review");ext.position=new Vector3(25,2.1f,38);ext.LookAt(new Vector3(-1,3,34));ext.GetComponent<Camera>().fieldOfView=65;
var village=root.Find("04_Mini_Village");var sign=village.Find("Sign - WILLOW CREEK GRAIN COOPERATIVE");sign.localPosition=new Vector3(-6,7.4f,224);
foreach(var post in village.Cast<Transform>().Where(t=>t.name=="Village gateway post"))post.localPosition+=new Vector3(-14,0,-1);
var hay=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Canvas beige.mat");hay.SetColor("_BaseColor",new Color(.62f,.49f,.32f));
// Warm, gently mottled cloth, kept local to the generated filling sack.
var cloth=new Texture2D(128,128,TextureFormat.RGBA32,true);var pixels=new Color[128*128];
for(int y=0;y<128;y++)for(int x=0;x<128;x++){float f=.82f+.12f*Mathf.PerlinNoise(x*.15f,y*.15f)+((x%3==0||y%3==0)?-.04f:.03f);pixels[y*128+x]=new Color(f,f,f,1);}cloth.SetPixels(pixels);cloth.Apply();AssetDatabase.CreateAsset(cloth,"Assets/NewMap/Materials/Burlap weave.asset");hay.SetTexture("_BaseColorMap",cloth);hay.SetTextureScale("_BaseColorMap",new Vector2(8,8));
var sun=root.Find("08_Lighting").GetComponentInChildren<Light>();sun.transform.rotation=Quaternion.Euler(42,160,0);
var refs=root.Find("07_Reference_Views");refs.GetChild(0).SetPositionAndRotation(root.Find("Player").GetComponentInChildren<Camera>().transform.position,root.Find("Player").GetComponentInChildren<Camera>().transform.rotation);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Final composition saved.";
}
}
