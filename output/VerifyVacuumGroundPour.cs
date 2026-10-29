using UnityEngine;using UnityEditor;using System;using System.Reflection;using System.Collections.Generic;
public static class VerifyVacuumGroundPour {
 public static object Run(){if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var results=new List<object>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;var source=UnityEngine.Object.FindFirstObjectByType<PlayerController>().HeldInstrument;
 for(int tier=5;tier<=6;tier++){
 var actor=new GameObject("Ground pour test");actor.transform.position=new Vector3(6000,0,6000);GameObject floor=null;GrainPile dropped=null;
 try{
 var cameraGo=new GameObject("Camera");cameraGo.transform.SetParent(actor.transform,false);var camera=cameraGo.AddComponent<Camera>();camera.transform.localPosition=Vector3.up*2.5f;camera.transform.rotation=Quaternion.Euler(90,0,0);
 var hands=UnityEngine.Object.Instantiate(source.gameObject,actor.transform);var tool=hands.GetComponent<ShovelTool>();foreach(Transform level in hands.transform.Find("Item In Hands"))level.gameObject.SetActive(level.name=="LVL_"+tier);typeof(ShovelTool).GetMethod("Awake",flags).Invoke(tool,null);var vac=tool.ActiveVacuum;
 floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=actor.transform.position-Vector3.up*.1f;floor.transform.localScale=new Vector3(20,.2f,20);Physics.SyncTransforms();float total=tool.CapacityKg;tool.Cargo.Add(GrainType.Corn,total);vac.Tick(tool,1);
 if(!tool.TryDrop(camera,actor.transform))throw new Exception("Ground drop rejected");
 float halfway=0,maxStep=0,last=total;
 for(int i=0;i<200;i++){
 tool.Tick(.02f);maxStep=Mathf.Max(maxStep,last-tool.Kilograms);last=tool.Kilograms;
 if(i==85)halfway=tool.Kilograms;
 if(dropped==null)foreach(var p in UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None))if(Vector3.Distance(p.transform.position,actor.transform.position)<1)dropped=p;
 if(dropped!=null && Math.Abs(dropped.Kilograms+tool.Kilograms-total)>.15)throw new Exception("Mass mismatch");
 }
 if(dropped==null||tool.Kilograms>.01f||halfway<=0||halfway>=total||maxStep>total*.05f)throw new Exception("Progressive empty failed remaining="+tool.Kilograms);
 var bounds=dropped.GetComponent<MeshFilter>().sharedMesh.bounds;if(bounds.size.y>bounds.size.x*.6f)throw new Exception("Pile too tall "+bounds);
 results.Add(new{tier,delivered=dropped.Kilograms,halfwayRemaining=halfway,maxFrameDeposit=maxStep,width=bounds.size.x,height=bounds.size.y});
 }finally{UnityEngine.Object.DestroyImmediate(actor);if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);if(dropped!=null)UnityEngine.Object.DestroyImmediate(dropped.gameObject);}
 }return results;}
}

