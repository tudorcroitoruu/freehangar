using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
public static class CombineHangar {
 const string Folder="Assets/NewMap/CombinedHangar";
 static string Key(MeshRenderer r) {
  string n=r.name; Vector3 p=r.transform.localPosition;
  if(n.Contains("Window")||n.Contains("clerestory")) {float z=new[]{-6f,0f,6f}.OrderBy(v=>Mathf.Abs(v-p.z)).First(); return "Window "+(p.x<0?"Left ":"Right ")+(Array.IndexOf(new[]{-6f,0f,6f},z)+1)+(n.Contains("Glazed")?" Glass":" Frame");}
  if(n.Contains("rooflight")||n.Contains("Rooflight")) {float z=Mathf.Abs(p.z+4)<Mathf.Abs(p.z-6)?-4:6;return "Skylight "+(p.x<0?"Left ":"Right ")+(z<0?1:2)+(n.Contains("Glass")?" Glass":" Frame");}
  if(n.Contains("door")||n.StartsWith("Door "))return "Door "+(p.x<0?"Left":"Right");
  if(n.Contains("roof panel")||n.Contains("gable fascia")||n.Contains("ridge beam"))return "Roof";
  if(n.Contains("end wall")||n.Contains("end cladding")||n.Contains("Front ")||n.Contains("Exterior vertical")||n.Contains("Entrance oak"))return p.z>0?"Wall Front":"Wall Rear";
  if(n.Contains("wall panel")||n=="Red exterior cladding"||n.Contains("panel seam")||n.Contains("wall rail"))return p.x<0?"Wall Left":"Wall Right";
  return "Timber Structure";
 }
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Unexpected scene");
  var root=GameObject.Find("New_Map").transform.Find("01_Modular_Hangar/Tall timber hall with glazing");
  if(root.Find("Roof"))throw new Exception("Already combined");
  var sources=root.GetComponentsInChildren<MeshRenderer>();
  if(sources.Length!=587)throw new Exception("Unexpected source count "+sources.Length);
  Directory.CreateDirectory("Temp/HangarCombine");
  EditorSceneManager.SaveScene(scene,"Temp/HangarCombine/Gameplay.before-combine.unity",true);
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  long beforeTriangles=sources.Sum(r=>(long)r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
  int beforeColliders=root.GetComponentsInChildren<BoxCollider>().Length;
  Bounds before=sources[0].bounds;foreach(var r in sources)before.Encapsulate(r.bounds);
  var originals=root.Cast<Transform>().ToArray();
  int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Combine hangar sections");
  var report=new List<string>();
  foreach(var group in sources.GroupBy(Key)) {
   var g=new GameObject(group.Key);Undo.RegisterCreatedObjectUndo(g,"Combined section");g.transform.SetParent(root,false);g.layer=group.First().gameObject.layer;
   var materials=new List<Material>();var submeshes=new List<Mesh>();
   foreach(var mat in group.SelectMany(r=>r.sharedMaterials).Distinct()){
    var instances=new List<CombineInstance>();
    foreach(var r in group){var mesh=r.GetComponent<MeshFilter>().sharedMesh;for(int s=0;s<mesh.subMeshCount;s++)if(r.sharedMaterials[s]==mat)instances.Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=g.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});}
    var sub=new Mesh{indexFormat=IndexFormat.UInt32};sub.CombineMeshes(instances.ToArray(),true,true);submeshes.Add(sub);materials.Add(mat);
   }
   var result=new Mesh{name=group.Key,indexFormat=IndexFormat.UInt32};result.CombineMeshes(submeshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);result.RecalculateBounds();
   AssetDatabase.CreateAsset(result,Folder+"/"+group.Key+".asset");foreach(var m in submeshes)UnityEngine.Object.DestroyImmediate(m);
   g.AddComponent<MeshFilter>().sharedMesh=result;var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();
   var first=group.First();renderer.shadowCastingMode=first.shadowCastingMode;renderer.receiveShadows=first.receiveShadows;renderer.lightProbeUsage=first.lightProbeUsage;renderer.reflectionProbeUsage=first.reflectionProbeUsage;renderer.renderingLayerMask=first.renderingLayerMask;
   // Bake the exact original box shapes, independently of visible geometry.
   var collisionParts=new List<CombineInstance>();var boxes=new List<Mesh>();
   foreach(var r in group)foreach(var box in r.GetComponents<BoxCollider>()){
    if(box.isTrigger)throw new Exception("Unexpected trigger");
    var mesh=new Mesh();var v=new Vector3[8];for(int i=0;i<8;i++)v[i]=box.center+Vector3.Scale(box.size,new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f));
    mesh.vertices=v;mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};boxes.Add(mesh);collisionParts.Add(new CombineInstance{mesh=mesh,transform=g.transform.worldToLocalMatrix*box.transform.localToWorldMatrix});
   }
   if(collisionParts.Count>0){var collision=new Mesh{name=group.Key+" Collision",indexFormat=IndexFormat.UInt32};collision.CombineMeshes(collisionParts.ToArray(),true,true);AssetDatabase.CreateAsset(collision,Folder+"/"+group.Key+" Collision.asset");g.AddComponent<MeshCollider>().sharedMesh=collision;}
   foreach(var m in boxes)UnityEngine.Object.DestroyImmediate(m);
   report.Add(group.Key+": "+group.Count()+" renderers -> 1; "+collisionParts.Count+" boxes baked");
  }
  var combined=root.Cast<Transform>().Except(originals).Select(t=>t.GetComponent<MeshRenderer>()).ToArray();
  long afterTriangles=combined.Sum(r=>(long)r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
  Bounds after=combined[0].bounds;foreach(var r in combined)after.Encapsulate(r.bounds);
  if(beforeTriangles!=afterTriangles||(before.center-after.center).magnitude>.002f||(before.size-after.size).magnitude>.002f)throw new Exception("Geometry validation failed");
  foreach(var t in originals)Undo.DestroyObjectImmediate(t.gameObject);
  Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  report.Insert(0,"Renderers: "+sources.Length+" -> "+combined.Length+"; triangles preserved: "+afterTriangles+"; original box shapes baked: "+beforeColliders+". World bounds match within 2mm.");
  Directory.CreateDirectory("output/NewMap/CombinedHangar");File.WriteAllLines("output/NewMap/CombinedHangar/validation.txt",report);
  Selection.activeGameObject=root.gameObject;return string.Join("\n",report);
 }
}

