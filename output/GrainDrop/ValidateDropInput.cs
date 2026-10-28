using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
public static class ValidateDropInput {
public static string Main(){
var root=new GameObject("Drop test actor");root.transform.position=new Vector3(10000,100,10000);
var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=root.transform.position+new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(10,.2f,10);
var camGo=new GameObject("test camera");camGo.transform.SetParent(root.transform);camGo.transform.localPosition=new Vector3(0,1.5f,0);camGo.transform.rotation=Quaternion.Euler(60,0,0);var cam=camGo.AddComponent<Camera>();cam.enabled=false;
var go=new GameObject("test shovel");go.transform.SetParent(camGo.transform);var blade=new GameObject("tip");blade.transform.SetParent(go.transform);var shovel=go.AddComponent<ShovelTool>();
typeof(ShovelTool).GetField("bladeTip",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(shovel,blade.transform);
try{
Physics.SyncTransforms();
shovel.Cargo.Add(GrainType.Rice,4);shovel.Cargo.Add(GrainType.Soybeans,2.5f);
if(!shovel.TryDrop(cam,root.transform))throw new Exception("Ground drop rejected");
shovel.CancelAction();if(shovel.Kilograms!=6.5f)throw new Exception("Cancel lost cargo");
if(!shovel.TryDrop(cam,root.transform))throw new Exception("Second try failed");shovel.Tick(.7f);shovel.Tick(.7f);
if(shovel.Kilograms>.001)throw new Exception("Cargo retained after valid drop");
var p=UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None).First(x=>Vector3.Distance(x.transform.position,root.transform.position)<5);
if(Math.Abs(p.Kilograms-6.5)>.001)throw new Exception("Dropped mass wrong");
Physics.SyncTransforms();shovel.Cargo.Add(GrainType.Wheat,2);if(!shovel.TryDrop(cam,root.transform))throw new Exception("Pile drop rejected");shovel.Tick(.7f);shovel.Tick(.7f);
if(Math.Abs(p.Kilograms-8.5)>.001)throw new Exception("Existing pile not increased");
if(shovel.TryDrop(cam,root.transform))throw new Exception("Empty shovel accepted");
return "PASS: cancel retains cargo, ground creates pile, mixed stock preserved, repeat adds to existing pile, empty rejected";
}finally{foreach(var p in UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None))if(Vector3.Distance(p.transform.position,root.transform.position)<5)UnityEngine.Object.DestroyImmediate(p.gameObject);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(floor);}
}}
