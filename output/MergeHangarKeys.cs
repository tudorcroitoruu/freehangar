using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class MergeHangarKeys {
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 int done=0;
 foreach(var find in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.FindKind==BuriedFind.Kind.HangarKey)){
 var root=find.transform;if(root.Find("Combined key mesh")!=null)continue;
 var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&f.GetComponent<MeshRenderer>()!=null).ToArray();
 var groups=new Dictionary<Material,List<CombineInstance>>();
 foreach(var f in filters){var r=f.GetComponent<MeshRenderer>();var mats=r.sharedMaterials;for(int i=0;i<f.sharedMesh.subMeshCount;i++){var mat=mats[Mathf.Min(i,mats.Length-1)];if(mat==null)throw new Exception("Missing material on "+f.name);if(!groups.ContainsKey(mat))groups.Add(mat,new List<CombineInstance>());groups[mat].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});}}
 var temporary=new List<Mesh>();var combinedParts=new List<CombineInstance>();foreach(var group in groups){var part=new Mesh();part.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;part.CombineMeshes(group.Value.ToArray(),true,true);temporary.Add(part);combinedParts.Add(new CombineInstance{mesh=part,transform=Matrix4x4.identity});}
 var mesh=new Mesh();mesh.name="Hangar "+find.TargetHangar+" combined key";mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(combinedParts.ToArray(),false,false);mesh.RecalculateBounds();
 string path="Assets/NewMap/Discoveries/Hangar "+find.TargetHangar+" combined key.asset";AssetDatabase.CreateAsset(mesh,path);
 foreach(var m in temporary)UnityEngine.Object.DestroyImmediate(m);
 var go=new GameObject("Combined key mesh");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=groups.Keys.ToArray();
 foreach(var f in filters){var renderer=f.GetComponent<MeshRenderer>();UnityEngine.Object.DestroyImmediate(renderer);UnityEngine.Object.DestroyImmediate(f);var coll=f==null?null:f.GetComponent<Collider>();}
 done++;
 }
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Combined "+done+" key models into single meshes with material submeshes";
 }
}
