using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class FixFarmAccess {
 static float Distance(Vector3 p,Vector3[] road){float best=float.MaxValue;for(int i=1;i<road.Length;i++){var a=new Vector2(road[i-1].x,road[i-1].z);var d=new Vector2(road[i].x,road[i].z)-a;var q=new Vector2(p.x,p.z)-a;best=Mathf.Min(best,(q-d*Mathf.Clamp01(Vector2.Dot(q,d)/d.sqrMagnitude)).magnitude);}return best;}
 public static string Main(){var root=GameObject.Find("New_Map").transform;var farms=root.Find("12_Farmyards_and_boundaries");var terrain=root.GetComponentInChildren<Terrain>();var plot=farms.Find("Whitewashed market garden");
  foreach(var t in plot.Cast<Transform>()){var p=t.position;p.x-=51;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;t.position=p;}
  foreach(var cell in farms.Cast<Transform>().Where(t=>t.name.StartsWith("Static fence cell")).ToArray()){var r=cell.GetComponentInChildren<Renderer>();if(!r)continue;var b=r.bounds;if(b.center.x>-80&&b.center.x<-35&&b.center.z>130&&b.center.z<190)cell.position+=Vector3.left*51;}
  var road=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(l=>{var s=l.Split(',');return new Vector3(float.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),0,float.Parse(s[2],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();int changed=0;
  foreach(var cell in farms.Cast<Transform>().Where(t=>t.name.StartsWith("Static fence cell")))foreach(var mesh in cell.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Concat(cell.GetComponentsInChildren<MeshCollider>().Select(c=>c.sharedMesh)).Distinct()){
   var vertices=mesh.vertices;for(int s=0;s<mesh.subMeshCount;s++){var triangles=mesh.GetTriangles(s);var kept=new List<int>();for(int i=0;i<triangles.Length;i+=3){var p=cell.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);if(Distance(p,road)<5.2f){changed++;continue;}kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}mesh.SetTriangles(kept,s);}mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
   foreach(var c in cell.GetComponentsInChildren<MeshCollider>())if(c.sharedMesh==mesh){c.sharedMesh=null;c.sharedMesh=mesh;}
  }
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.gameObject.scene);return "Market garden shifted away from road; fence triangles removed for route gates="+changed;}
}
