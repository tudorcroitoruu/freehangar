using System;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class RefineMap {
static Transform root,h;static Material wood;static GameObject Box(string n,Transform p,Vector3 pos,Vector3 scale,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
static void Beam(string n,Vector3 a,Vector3 b,float w){var g=Box(n,h,(a+b)/2,new Vector3(w,(b-a).magnitude,w),wood);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
public static string Main(){
root=GameObject.Find("New_Map").transform;h=root.Find("01_Modular_Hangar");wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Warm oak structural beams.mat");
foreach(var t in h.Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Hall_Arch")).ToArray())t.gameObject.SetActive(false);
for(int z=-14;z<=14;z+=4){
foreach(int side in new[]{-1,1}){
Box("Clear span oak post",h,new Vector3(side*9.4f,3.6f,z),new Vector3(.3f,7.2f,.32f),wood);
Beam("Oak rafter",new Vector3(side*9.4f,7,z),new Vector3(0,9.4f,z),.26f);
Beam("Knee brace",new Vector3(side*9.4f,5.5f,z),new Vector3(side*7.6f,7,z),.19f);}
Box("Clear span crossbeam",h,new Vector3(0,7,z),new Vector3(19,.25f,.3f),wood);
}
foreach(var tm in root.GetComponentsInChildren<TMPro.TextMeshPro>(true)){tm.fontSize*=10;tm.text=tm.text.Replace("\\n",Environment.NewLine);}
var volume=root.Find("08_Lighting").GetComponentsInChildren<Volume>();
foreach(var v in volume){
if(v.sharedProfile.TryGet<HDRISky>(out var sky))sky.exposure.Override(1.3f);
if(v.sharedProfile.TryGet<ColorAdjustments>(out var ca)){ca.saturation.Override(-6);ca.postExposure.Override(.6f);}
}
foreach(var l in root.GetComponentsInChildren<Light>()){
if(l.type==LightType.Directional){l.intensity=22;l.transform.rotation=Quaternion.Euler(32,205,0);}
else if(l.name=="Soft warm bounce"||l.name=="Doorway sky fill")l.GetComponent<HDAdditionalLightData>().SetIntensity(9500,LightUnit.Lumen);
else l.GetComponent<HDAdditionalLightData>().SetIntensity(1200,LightUnit.Lumen);
}
var fill=new GameObject("Front softbox bounce");fill.transform.SetParent(h,false);fill.transform.localPosition=new Vector3(-3,4,-7);var light=fill.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.83f,.64f);light.range=24;fill.AddComponent<HDAdditionalLightData>().SetIntensity(6500,LightUnit.Lumen);
var gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Golden maize.mat");gold.SetTextureScale("_BaseColorMap",new Vector2(1.3f,1.3f));gold.SetColor("_BaseColor",new Color(1,.68f,.12f));
var props=root.Find("02_Hangar_Props");
var sack=props.Cast<Transform>().First(t=>t.name=="TFP_Cereal_Sack_01A"&&t.position.z<0);sack.localScale=Vector3.one*2.3f;sack.localPosition=new Vector3(.1f,.08f,-5.9f);
var wheel=props.Cast<Transform>().First(t=>t.name=="TFP_Wheelbarrow_02A");Object.DestroyImmediate(wheel.gameObject);
var path=AssetDatabase.FindAssets("TFP_Wheelbarrow_01A t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.EndsWith("TFP_Wheelbarrow_01A.prefab"));
var barrow=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),props);barrow.transform.localPosition=new Vector3(-.5f,.06f,-1.6f);barrow.transform.localRotation=Quaternion.Euler(0,-70,0);barrow.transform.localScale=Vector3.one*1.4f;
var red=new Material(Shader.Find("HDRP/Lit"));red.SetColor("_BaseColor",new Color(.48f,.065f,.025f));red.SetFloat("_Smoothness",.23f);AssetDatabase.CreateAsset(red,"Assets/NewMap/Materials/Wheelbarrow red.mat");
foreach(var r in barrow.GetComponentsInChildren<Renderer>())if(r.name.Contains("Body"))r.sharedMaterial=red;
var cam=root.Find("Player").GetComponentInChildren<Camera>();cam.transform.localRotation=Quaternion.Euler(-3,0,0);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "refined";
}
}

