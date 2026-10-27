using UnityEngine;using UnityEditor;using System.Linq;using System.Text;
public static class InspectFenceMesh{public static string Main(){var sb=new StringBuilder();foreach(var col in Physics.OverlapBox(new Vector3(40,9,131),new Vector3(3,3,3)).OfType<MeshCollider>()){
sb.AppendLine(col.transform.parent.name+" mesh="+AssetDatabase.GetAssetPath(col.sharedMesh)+" convex="+col.convex+" scale="+col.transform.lossyScale);
foreach(var f in col.transform.parent.GetComponentsInChildren<MeshFilter>())sb.AppendLine("Render "+f.name+" "+f.GetComponent<Renderer>().bounds+" mesh="+AssetDatabase.GetAssetPath(f.sharedMesh));
var v=col.sharedMesh.vertices;var tri=col.sharedMesh.triangles;int n=0;for(int i=0;i<tri.Length;i+=3){var a=col.transform.TransformPoint(v[tri[i]]);var b=col.transform.TransformPoint(v[tri[i+1]]);var c=col.transform.TransformPoint(v[tri[i+2]]);var mid=(a+b+c)/3;if(mid.x<43&&mid.z>126&&mid.z<137&&n++<25)sb.AppendLine("tri "+a+" "+b+" "+c);}}
return sb.ToString();}}
