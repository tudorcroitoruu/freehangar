using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;
public static class RestoreOldRoomGround{
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var terrain=Terrain.activeTerrain;var data=terrain.terrainData;
 var original=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/LaunchRoom/Temporary original terrain.asset");
 if(original==null||original.heightmapResolution!=data.heightmapResolution)throw new Exception("Missing matching original terrain");
 var h=GameObject.Find("Hangar 5 - Large").transform;var center=h.TransformPoint(new Vector3(10.1f,.05f,5));var rotation=h.rotation*Quaternion.Euler(0,90,0);
 int res=data.heightmapResolution;var current=data.GetHeights(0,0,res,res);var old=original.GetHeights(0,0,res,res);Undo.RegisterCompleteObjectUndo(data,"Restore ground at former electrical room");int count=0;float max=0;
 for(int z=0;z<res;z++)for(int x=0;x<res;x++){
  Vector3 world=terrain.transform.position+new Vector3(x/(float)(res-1)*data.size.x,0,z/(float)(res-1)*data.size.z);
  var p=Quaternion.Inverse(rotation)*(world-center);
  float dx=Mathf.Max(0,Mathf.Abs(p.x)-5.1f);float dz=Mathf.Max(0,Mathf.Max(-.7f-p.z,p.z-13.1f));
  float outside=Mathf.Max(dx,dz);if(outside>=2)continue;
  float weight=1-Mathf.SmoothStep(0,1,outside/2);
  float target=Mathf.Lerp(current[z,x],old[z,x],weight);max=Mathf.Max(max,Mathf.Abs(target-current[z,x])*data.size.y);if(Mathf.Abs(target-current[z,x])>.000001f){current[z,x]=target;count++;}
 }
 data.SetHeights(0,0,current);terrain.Flush();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);EditorSceneManager.SaveOpenScenes();
 return "Restored "+count+" height samples, maximum correction "+max+" m, with 2 m blended edge";
 }}
