using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class FinishOriginalStyle {
const string A="Assets/NewMap/OriginalStyle";
static float Warp(float y)=>y<=4?y*1.7f:6.8f+(y-4)*.95f;
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var root=GameObject.Find("New_Map");var terrain=root.GetComponentInChildren<Terrain>();var power=root.transform.Find("10_Power_Line");var poles=power.Cast<Transform>().Where(t=>t.name.StartsWith("TFP_Wooden_Electricity_Pole")).OrderBy(t=>t.name).ToArray();
var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Props/Fence Kits/TFP_Electricity_Pole_Cable_01A.prefab");var instance=Object.Instantiate(prefab);var skinned=instance.GetComponent<SkinnedMeshRenderer>();skinned.SetBlendShapeWeight(0,100);var source=new Mesh();skinned.BakeMesh(source);source.RecalculateBounds();Object.DestroyImmediate(instance);
float minClearance=999;for(int i=1;i<poles.Length;i++){
var a=poles[i-1];var b=poles[i];var filter=power.Find("TFP_Electricity_Pole_Cable_01A - span "+i).GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var vertices=source.vertices;float span=Vector3.Distance(a.position,b.position);float sagScale=Mathf.Clamp(span/24,.65f,1.6f);
for(int j=0;j<vertices.Length;j++){var v=vertices[j];float t=Mathf.InverseLerp(source.bounds.max.x,source.bounds.min.x,v.x);var start=a.TransformPoint(new Vector3(-.57272f,8.84f,v.z));var end=b.TransformPoint(new Vector3(-.57272f,8.84f,v.z));var p=Vector3.Lerp(start,end,t)+Vector3.up*((v.y-8.84f)*sagScale);vertices[j]=p-a.position;minClearance=Mathf.Min(minClearance,p.y-terrain.SampleHeight(p)-terrain.transform.position.y);}
mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
}
Object.DestroyImmediate(source);
for(int cable=0;cable<3;cable++){var line=power.Find("Hangar service drop "+(cable+1)).GetComponent<LineRenderer>();var a=new Vector3(-9.8f,8.2f,15.4f+cable*.15f);var b=poles[0].TransformPoint(new Vector3(-.57272f,8.84f,-1.33f+cable*1.33f));for(int j=0;j<25;j++){float t=j/24f;line.SetPosition(j,Vector3.Lerp(a,b,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.65f);}}
// Match the separate collision meshes to the fitted visible barn meshes.
int colliders=0;AssetDatabase.StartAssetEditing();try{
var barn=root.transform.Find("01_Modular_Hangar/Red barn exterior - TFP_Barn_04A");foreach(var collider in barn.GetComponentsInChildren<MeshCollider>()){
if(AssetDatabase.GetAssetPath(collider.sharedMesh).StartsWith(A))continue;var mesh=Object.Instantiate(collider.sharedMesh);var vertices=mesh.vertices;var barnRoot=collider.transform;while(barnRoot.parent!=barn)barnRoot=barnRoot.parent;
var toRoot=barnRoot.worldToLocalMatrix*collider.transform.localToWorldMatrix;var fromRoot=toRoot.inverse;for(int i=0;i<vertices.Length;i++){var p=toRoot.MultiplyPoint3x4(vertices[i]);p.y=Warp(p.y);vertices[i]=fromRoot.MultiplyPoint3x4(p);}mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.name="Fitted barn collision "+colliders;AssetDatabase.CreateAsset(mesh,A+"/"+mesh.name+".asset");collider.sharedMesh=mesh;colliders++;
}
var atlas=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/TFP_Atlas_1A - NewMap quality.mat");var rut=new Material(Shader.Find("HDRP/Lit")){name="Original road detail warm earth"};rut.SetTexture("_BaseColorMap",atlas.GetTexture("_TextureSample"));rut.SetColor("_BaseColor",new Color(.68f,.55f,.4f));rut.SetFloat("_Smoothness",.05f);HDMaterial.ValidateMaterial(rut);AssetDatabase.CreateAsset(rut,A+"/Road detail warm earth.mat");
int index=0;foreach(var t in root.transform.Find("09_Original_Road_Details/Curved dirt ruts - original TFP meshes").Cast<Transform>()){t.GetComponent<MeshRenderer>().sharedMaterial=rut;if(index++%3==2)t.gameObject.SetActive(false);}
}finally{AssetDatabase.StopAssetEditing();}
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());var report="20 poles, 19 connected native cable spans; minimum sampled wire/terrain clearance "+minClearance.ToString("F2")+"m. Three hangar service drops. "+colliders+" separate barn collision meshes fitted to visible shell. Road ruts spaced and toned to original dirt.";File.WriteAllText("output/NewMap/OriginalStyle/power-line.txt",report);return report;
}
}
