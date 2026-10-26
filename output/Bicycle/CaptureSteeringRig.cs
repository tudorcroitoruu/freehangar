using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;
public static class CaptureSteeringRig {
 static void Shot(Camera c,Vector3 eye,Quaternion look,string path){var pos=c.transform.position;var rot=c.transform.rotation;var target=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,850,24);var t=new Texture2D(1200,850,TextureFormat.RGB24,false);try{c.transform.SetPositionAndRotation(eye,look);c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1200,850),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());}finally{c.transform.SetPositionAndRotation(pos,rot);c.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(t);}}
 public static string Run(){var b=UnityEngine.Object.FindFirstObjectByType<BicycleController>();var c=SceneView.lastActiveSceneView.camera;var rest=b.steering.localRotation;try{
 foreach(int angle in new[]{-30,0,30}){b.steering.localRotation=rest*Quaternion.Euler(0,angle,0);Shot(c,b.riderView.TransformPoint(b.RidingCameraOffset),b.riderView.rotation*Quaternion.Euler(b.RidingCameraPitch+12,0,0),"output/Bicycle/steering-"+angle+".png");}
 return "Captured first-person views at -30/0/+30 degrees";
 }finally{b.steering.localRotation=rest;}}
}
