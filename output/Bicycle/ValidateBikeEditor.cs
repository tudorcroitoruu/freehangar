using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;using System.IO;
public static class ValidateBikeEditor {
 public static string Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");
 var bike=UnityEngine.Object.FindFirstObjectByType<BicycleController>();var offset=bike.RidingCameraOffset;float pitch=bike.RidingCameraPitch;int checks=0;void Check(bool ok,string m){if(!ok)throw new Exception(m);checks++;}
 var editor=Editor.CreateEditor(bike);BicycleRidingPositionWindow window=null;
 var sceneView=SceneView.lastActiveSceneView;var pivot=sceneView.pivot;var rotation=sceneView.rotation;float size=sceneView.size;
 Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
 try{
 Check(editor is BicycleControllerEditor,"Custom Inspector with adjustment button");
 Undo.RecordObject(bike,"Validate riding camera undo");bike.SetRidingCamera(new Vector3(.12f,.15f,-.12f),8);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Check(Vector3.Distance(bike.RidingCameraOffset,offset)<.0001f,"Camera offset undo");Undo.PerformRedo();Check(Vector3.Distance(bike.RidingCameraOffset,new Vector3(.12f,.15f,-.12f))<.0001f,"Camera offset redo");
 BicycleRidingPositionWindow.Open(bike);window=EditorWindow.GetWindow<BicycleRidingPositionWindow>();window.Preview();
 var expected=bike.riderView.TransformPoint(bike.RidingCameraOffset);
 Check(Vector3.Distance(sceneView.pivot-sceneView.rotation*Vector3.forward*sceneView.cameraDistance,expected)<.02f,"Scene preview camera is at seated eye position: "+sceneView.camera.transform.position+" vs "+expected);
 Check(Quaternion.Angle(sceneView.rotation,bike.riderView.rotation*Quaternion.Euler(8,0,0))<.01f,"Preview rotation");
 window.RestorePreview();Check(Vector3.Distance(sceneView.pivot,pivot)<.001f&&Mathf.Abs(sceneView.size-size)<.001f&&Quaternion.Angle(sceneView.rotation,rotation)<.01f,"Scene view restored");
 return "PASS: "+checks+" Inspector/Undo/Redo/seat-preview/restore checks.";
 }finally{if(window!=null)window.Close();UnityEngine.Object.DestroyImmediate(editor);Undo.RevertAllDownToGroup(group);bike.SetRidingCamera(offset,pitch);EditorUtility.SetDirty(bike);}
 }
}

