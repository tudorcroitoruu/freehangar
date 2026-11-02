using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class CapturePedalRepair {
 static void Shot(Camera c, Vector3 eye, Quaternion look, string path) {
 var pos=c.transform.position;var rot=c.transform.rotation;var target=c.targetTexture;var active=RenderTexture.active;float fov=c.fieldOfView;
 var rt=RenderTexture.GetTemporary(1200,850,24);var tex=new Texture2D(1200,850,TextureFormat.RGB24,false);
 try { c.transform.SetPositionAndRotation(eye,look);c.fieldOfView=65;c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,850),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG()); }
 finally {c.transform.SetPositionAndRotation(pos,rot);c.fieldOfView=fov;c.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
 }
 public static string Run() {
 var b=UnityEngine.Object.FindObjectsByType<BicycleController>(FindObjectsSortMode.None).First(x=>!x.motorDriven);var c=SceneView.lastActiveSceneView.camera;
 var cr=b.crank.localRotation;var lp=b.leftPedal.position;var rp=b.rightPedal.position;var lr=b.leftPedal.rotation;var rr=b.rightPedal.rotation;
 var left=b.transform.InverseTransformVector(lp-b.crank.position);var right=b.transform.InverseTransformVector(rp-b.crank.position);
 try {
 foreach(int a in new[]{0,90,180,270}) {
 var q=Quaternion.Euler(a,0,0);b.crank.localRotation=cr*q;
 b.leftPedal.position=b.crank.position+b.transform.TransformVector(q*left);b.rightPedal.position=b.crank.position+b.transform.TransformVector(q*right);
 b.leftPedal.rotation=lr;b.rightPedal.rotation=rr;
 Shot(c,b.riderView.TransformPoint(b.RidingCameraOffset),b.riderView.rotation*Quaternion.Euler(75,0,0),"output/Bicycle/pedals-repaired-"+a+".png");
 }
 return "Captured pedals at 0/90/180/270 degrees.";
 } finally {b.crank.localRotation=cr;b.leftPedal.SetPositionAndRotation(lp,lr);b.rightPedal.SetPositionAndRotation(rp,rr);}
 }
}
