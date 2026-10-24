using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class SurfaceQualityAndAudit {
static Dictionary<Texture,Texture> copied=new Dictionary<Texture,Texture>();
static Texture CopyTexture(Texture original){
 if(!original)return null;if(copied.TryGetValue(original,out var found))return found;
 string source=AssetDatabase.GetAssetPath(original);if(!source.StartsWith("Assets/Imported/")||!(AssetImporter.GetAtPath(source) is TextureImporter))return original;
 string path="Assets/NewMap/Textures/"+Path.GetFileName(source);
 if(!File.Exists(path))AssetDatabase.CopyAsset(source,path);
 var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=Mathf.Max(2048,original.width);importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=16;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;importer.SaveAndReimport();
 var result=AssetDatabase.LoadAssetAtPath<Texture>(path);copied[original]=result;return result;
}
public static string Quality(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var root=GameObject.Find("New_Map");var cache=new Dictionary<Material,Material>();
 foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
 var materials=r.sharedMaterials;bool changed=false;
 for(int i=0;i<materials.Length;i++){
 var m=materials[i];if(!m||!AssetDatabase.GetAssetPath(m).StartsWith("Assets/Imported/"))continue;
 if(!cache.TryGetValue(m,out var copy)){
 copy=new Material(m){name=m.name+" - NewMap quality"};AssetDatabase.CreateAsset(copy,"Assets/NewMap/Materials/"+copy.name+".mat");
 foreach(var key in m.GetTexturePropertyNames()){var texture=m.GetTexture(key);if(texture)copy.SetTexture(key,CopyTexture(texture));}
 cache[m]=copy;
 }
 materials[i]=copy;changed=true;
 }if(changed)r.sharedMaterials=materials;
 }
 var terrain=root.GetComponentInChildren<Terrain>();
 foreach(var l in terrain.terrainData.terrainLayers){l.diffuseTexture=CopyTexture(l.diffuseTexture) as Texture2D;l.normalMapTexture=CopyTexture(l.normalMapTexture) as Texture2D;EditorUtility.SetDirty(l);}
 AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return cache.Count+" map-owned material copies, "+copied.Count+" filtered texture copies. Shared source assets unchanged.";
}
public static string Audit(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var root=GameObject.Find("New_Map");var terrain=root.GetComponentInChildren<Terrain>();var camera=GameObject.Find("Player").GetComponentInChildren<Camera>();
 var points=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(line=>{var s=line.Split(',');return new Vector3(float.Parse(s[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(s[2],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();
 Physics.SyncTransforms();float grade=0,error=0;var obstructions=new HashSet<string>();
 for(int i=1;i<points.Length;i++){
 var p=points[i];float y=terrain.SampleHeight(p)+terrain.transform.position.y;float prev=terrain.SampleHeight(points[i-1])+terrain.transform.position.y;
 grade=Mathf.Max(grade,Mathf.Abs(y-prev)/Vector2.Distance(new Vector2(p.x,p.z),new Vector2(points[i-1].x,points[i-1].z)));error=Mathf.Max(error,Mathf.Abs(p.y-y));
 var direction=p-points[i-1];direction.y=0;
 foreach(var hit in Physics.OverlapBox(new Vector3(p.x,y+1.1f,p.z),new Vector3(1.65f,.65f,.6f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore)){
 if(hit is TerrainCollider||hit.transform.IsChildOf(camera.transform.root))continue;obstructions.Add(hit.name+" at "+hit.transform.position);
 }
 }
 int missing=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
 int badMaterials=root.GetComponentsInChildren<Renderer>(true).Count(r=>r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported));
 int badMeshes=root.GetComponentsInChildren<MeshFilter>(true).Count(m=>!m.sharedMesh);
 string report="Hill and surface revision audit\nScene: "+scene.path+"\nMissing scripts: "+missing+"\nMissing/unsupported materials: "+badMaterials+"\nMissing meshes: "+badMeshes+"\nTerrain collider valid: "+(terrain.GetComponent<TerrainCollider>().terrainData==terrain.terrainData)+"\nActual sampled road max grade: "+(grade*100).ToString("F2")+"%\nMax terrain/road height deviation: "+error.ToString("F3")+"m\n3.3m-wide road clearance obstructions: "+obstructions.Count+"\n"+string.Join("\n",obstructions)+"\nEnabled cameras: "+Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)+"\nPlayer: "+camera.transform.root.position+"; camera: "+camera.transform.position+"; yaw "+camera.transform.eulerAngles.y+"; FOV "+camera.fieldOfView+"\n"+string.Join("\n",scene.GetRootGameObjects().Select(g=>g.name+" active="+g.activeSelf))+"\n"+File.ReadAllText("output/NewMap/Revision2/layout.txt");
 File.WriteAllText("output/NewMap/Revision2/validation.txt",report);return report;
}
}
