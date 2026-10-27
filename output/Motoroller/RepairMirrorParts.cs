#pragma warning disable 0649
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;using System.Linq;using System.IO;
public static class RepairMirrorParts{
const string Dir="Assets/Models/motorcycle-old/Optimized/";
[Serializable]class Data{public Part[] parts;}
[Serializable]class Part{public string name,side,kind;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;public Vector3 center;}
static Material Surface(string name,bool glass){var path=Dir+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=glass?new Material(Shader.Find("HDRP/Lit")):new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/motorcycle-old/source/Materials/map_BodyM05_BaseColor.mat"));AssetDatabase.CreateAsset(mat,path);}mat.SetFloat("_DoubleSidedEnable",1);mat.SetFloat("_DoubleSidedNormalMode",0);mat.doubleSidedGI=true;if(glass){mat.SetColor("_BaseColor",new Color(.8f,.84f,.87f));mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",.94f);}UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(mat);EditorUtility.SetDirty(mat);return mat;}
public static string Main(){if(Application.isPlaying)throw new Exception("Edit Mode required");var root=PrefabUtility.LoadPrefabContents(Dir+"Motoroller_Rideable.prefab");try{
var body=Surface("Mirror housing double sided",false);var glass=Surface("Mirror glass double sided",true);
foreach(var data in Newtonsoft.Json.JsonConvert.DeserializeObject<Data>(File.ReadAllText("output/Motoroller/mirror-parts.json")).parts){
var parent=root.GetComponentsInChildren<Transform>().First(t=>t.name==data.side+"Mirror");var oldFilter=parent.GetComponent<MeshFilter>();if(oldFilter!=null)UnityEngine.Object.DestroyImmediate(oldFilter);var oldRenderer=parent.GetComponent<MeshRenderer>();if(oldRenderer!=null)UnityEngine.Object.DestroyImmediate(oldRenderer);
var node=parent.Find(data.kind);if(node==null){node=new GameObject(data.kind).transform;node.SetParent(root.transform,false);node.localPosition=data.center;node.SetParent(parent,true);}
node.position=root.transform.TransformPoint(data.center);node.rotation=root.transform.rotation;var mesh=new Mesh{name=data.name};var v=data.vertices.Select(p=>p-data.center).ToArray();mesh.vertices=v;mesh.triangles=data.triangles;mesh.normals=data.normals;mesh.uv=data.uv;mesh.RecalculateBounds();mesh.RecalculateTangents();var path=Dir+data.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
var filter=node.GetComponent<MeshFilter>();if(filter==null)filter=node.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=saved;var renderer=node.GetComponent<MeshRenderer>();if(renderer==null)renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=data.kind=="Glass"?glass:body;
}
// Repair every previously baked mesh in this prefab, including the steering assembly.
foreach(var f in root.GetComponentsInChildren<MeshFilter>()){f.sharedMesh.RecalculateBounds();EditorUtility.SetDirty(f.sharedMesh);}
PrefabUtility.SaveAsPrefabAsset(root,Dir+"Motoroller_Rideable.prefab");AssetDatabase.SaveAssets();
}finally{PrefabUtility.UnloadPrefabContents(root);}
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Separated six mirror meshes; repaired mesh bounds; enabled HDRP double-sided housing and glass";}}



