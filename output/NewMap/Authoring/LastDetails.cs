using System;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class LastDetails {
public static string Main(){
var root=GameObject.Find("New_Map").transform;
foreach(var mf in root.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("Dense maize")||m.name=="Maize inside filling sack")){
var mesh=mf.sharedMesh;var old=mesh.vertices;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
for(int k=0;k+7<old.Length;k+=8){var top=old[k];var bottom=Vector3.zero;for(int j=0;j<7;j++)bottom+=old[k+1+j];bottom/=7;int b=v.Count;
v.Add(top);uv.Add(Vector2.zero);
for(int j=0;j<7;j++){var rim=old[k+1+j];v.Add(new Vector3(Mathf.Lerp(bottom.x,rim.x,.75f),Mathf.Lerp(bottom.y,top.y,.75f),Mathf.Lerp(bottom.z,rim.z,.75f)));uv.Add(Vector2.zero);}
for(int j=0;j<7;j++){v.Add(old[k+1+j]);uv.Add(Vector2.zero);}
for(int j=0;j<7;j++){int next=(j+1)%7;tri.AddRange(new[]{b,b+1+next,b+1+j,b+1+j,b+1+next,b+8+j,b+1+next,b+8+next,b+8+j});}}
mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
}
root.Find("03_Farmyard/Weigh station").localRotation=Quaternion.Euler(0,-55,0);
var signs=root.Find("03_Farmyard/Farm direction signs");signs.localPosition=new Vector3(19,0,42);signs.localRotation=Quaternion.Euler(0,-56,0);
var terrain=root.GetComponentInChildren<Terrain>();var sp=signs.position;sp.y=terrain.SampleHeight(sp)-8;signs.position=sp;
var hangar=root.Find("01_Modular_Hangar");
foreach(var door in hangar.Cast<Transform>().Where(t=>t.name.StartsWith("Open sliding door"))){
foreach(var member in door.Cast<Transform>().Where(t=>t.name=="Door cross rail"||t.name=="Door diagonal brace").ToArray()){var g=UnityEngine.Object.Instantiate(member.gameObject,door);g.name=member.name+" exterior";var pos=g.transform.localPosition;pos.z=.14f;g.transform.localPosition=pos;}
}
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Final detailing saved.";
}
}
