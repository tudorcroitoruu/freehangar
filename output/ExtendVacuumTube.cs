using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;using System.Linq;using System.Collections.Generic;
public static class ExtendVacuumTube {
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var f=Selection.activeGameObject.transform.parent.Find("casing").GetComponent<MeshFilter>();
 var source=f.sharedMesh;var v=source.vertices;var t=source.triangles;var parent=Enumerable.Range(0,v.Length).ToArray();Func<int,int> find=null;find=i=>parent[i]==i?i:parent[i]=find(parent[i]);var weld=new Dictionary<Vector3,int>();for(int i=0;i<v.Length;i++){if(weld.TryGetValue(v[i],out var n))parent[find(i)]=find(n);else weld[v[i]]=i;}for(int i=0;i<t.Length;i+=3){parent[find(t[i+1])]=find(t[i]);parent[find(t[i+2])]=find(t[i]);}
 var groups=t.Distinct().GroupBy(i=>find(i)).Select(g=>{var b=new Bounds(v[g.First()],Vector3.zero);foreach(var i in g)b.Encapsulate(v[i]);return new{indices=g.ToArray(),bounds=b,id=g.Key};}).ToArray();
 var tube=groups.Single(g=>g.bounds.size.x>.35f&&g.bounds.size.y<.06f);
 var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Vacuum casing with extended steel tube";
 foreach(var i in tube.indices)v[i].x=tube.bounds.min.x+(v[i].x-tube.bounds.min.x)*1.2f;
 mesh.vertices=v;var casing=new List<int>();var pipe=new List<int>();for(int i=0;i<t.Length;i+=3){var list=find(t[i])==tube.id?pipe:casing;list.Add(t[i]);list.Add(t[i+1]);list.Add(t[i+2]);}
 mesh.subMeshCount=2;mesh.SetTriangles(casing,0);mesh.SetTriangles(pipe,1);mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
 const string folder="Assets/Models/Models New/Shovel/Lvl_5/toon-vacuum-cleaner/source/Materials/";
 string meshPath=AssetDatabase.GenerateUniqueAssetPath(folder+"Extended vacuum casing.asset");AssetDatabase.CreateAsset(mesh,meshPath);
 var material=new Material(Shader.Find("HDRP/Lit"));material.name="Vacuum tube dark steel";material.SetColor("_BaseColor",new Color(.11f,.13f,.16f,1));material.SetFloat("_Metallic",.8f);material.SetFloat("_Smoothness",.35f);string matPath=AssetDatabase.GenerateUniqueAssetPath(folder+"Vacuum tube dark steel.mat");AssetDatabase.CreateAsset(material,matPath);
 var renderer=f.GetComponent<MeshRenderer>();Undo.RecordObjects(new UnityEngine.Object[]{f,renderer},"Extend vacuum tube and change material");f.sharedMesh=mesh;renderer.sharedMaterials=new[]{renderer.sharedMaterials[0],material};EditorUtility.SetDirty(f);EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(f.gameObject.scene);EditorSceneManager.SaveScene(f.gameObject.scene);
 return new{meshPath,matPath,oldLength=tube.bounds.size.x,newLength=tube.bounds.size.x*1.2f,casingTriangles=casing.Count/3,tubeTriangles=pipe.Count/3,scene=f.gameObject.scene.path};
 }
}
