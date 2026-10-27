using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System.Linq;
public static class SaveBicyclePrefab {public static string Main(){
if(Application.isPlaying)throw new System.Exception("Must be in Edit Mode");
var bike=Object.FindObjectsByType<BicycleController>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>!b.motorDriven&&b.gameObject.scene.IsValid());
if(bike.cargoSlots.Any(s=>s==null||s.childCount>0))throw new System.Exception("Expected empty cargo anchors");
const string path="Assets/Models/bicycle/Optimized/Bicycle_Rideable.prefab";
var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(bike.gameObject,path,InteractionMode.AutomatedAction,out bool success);
if(!success||prefab==null)throw new System.Exception("Prefab save failed");
var saved=prefab.GetComponent<BicycleController>();
if(saved==null||saved.steering==null||saved.frontWheel==null||saved.rearWheel==null||saved.riderView==null||saved.crank==null||!saved.frontWheel.IsChildOf(saved.steering))throw new System.Exception("Incomplete saved rig");
if(prefab.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh==null)||prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null)))throw new System.Exception("Missing mesh/material");
AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(bike.gameObject.scene);EditorSceneManager.SaveScene(bike.gameObject.scene);Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);
return "Saved and connected "+path+"; verified steering, wheels, crank, rider view, cargo anchors, meshes and materials.";
}}
