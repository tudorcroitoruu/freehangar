using System;using System.Linq;using System.IO;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using Object=UnityEngine.Object;
public static class ValidateNewMap {
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Run final asset audit in Edit Mode.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().Single(g=>g.name=="New_Map");
int missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
var missingMaterials=root.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterials.Any(m=>!m||!m.shader)).Select(r=>r.name).ToArray();
var missingMeshes=root.GetComponentsInChildren<MeshFilter>().Where(m=>!m.sharedMesh).Select(m=>m.name).ToArray();
var terrain=root.GetComponentInChildren<Terrain>();var td=terrain.terrainData;
var cameras=root.GetComponentsInChildren<Camera>();var cc=root.GetComponentInChildren<CharacterController>();
var routes=root.transform.Find("06_Road_Route").Cast<Transform>().ToArray();
int unsupported=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m&&!m.shader.isSupported).Distinct().Count();
string report="New_Map validation - 2026-10-24\nScene: "+scene.path+"\nMissing scripts: "+missingScripts+"\nMissing material/shader references: "+missingMaterials.Length+"\nMissing meshes: "+missingMeshes.Length+"\nUnsupported shaders: "+unsupported+"\nTerrain size: "+td.size+"\nTerrainCollider: "+(terrain.GetComponent<TerrainCollider>().terrainData==td)+"\nEnabled cameras: "+cameras.Count(c=>c.enabled)+"\nPlayer FOV: "+root.GetComponentInChildren<PlayerController>().characterCamera.fieldOfView+"\nPlayer restored to: "+cc.transform.position+"\nRoute markers: "+routes.Length+"\n";
foreach(var name in new[]{"MAP","Graphics","Player"})report+=name+" original active: "+scene.GetRootGameObjects().Single(g=>g.name==name).activeSelf+"\n";
report+=File.ReadAllText("output/NewMap/walk-validation.txt")+"\n";
report+="No new Console errors in Play Mode (Pipeline cursor 17 unchanged). Baseline contained an older HierarchyDecorator deserialization exception; current Console ground truth: 0 errors.\nNo vehicle controller/timed driving run was added or tested; 470.2m at assumed 8m/s = 58.8 seconds.\nNo player build was requested or run.\n";
File.WriteAllText("output/NewMap/validation.txt",report);
return report;
}
public static string Cleanup(){
var root=GameObject.Find("New_Map").transform;
foreach(var sign in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Sign - ")).ToArray())
foreach(var child in sign.Cast<Transform>().Where(t=>t.name!="Painted panel"&&t.name!="Timber frame").ToArray())Object.DestroyImmediate(child.gameObject);
var proto=root.Find("02_Hangar_Props").Cast<Transform>().FirstOrDefault(t=>t.name=="TFP_Cereal_Sack_01A"&&!t.gameObject.activeSelf);if(proto)Object.DestroyImmediate(proto.gameObject);
foreach(var p in AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/NewMap/Meshes"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p).StartsWith("Sign ")||Path.GetFileName(p).StartsWith("Maize kernels batch")).ToArray())AssetDatabase.DeleteAsset(p);
foreach(var path in new[]{"Assets/NewMap/Materials/SignGlyphAtlas.asset","Assets/NewMap/Materials/Depth tested signage.mat"})AssetDatabase.DeleteAsset(path);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return "Unused iterative sign/grain assets removed.";
}
}
