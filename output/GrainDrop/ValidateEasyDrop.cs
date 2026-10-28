using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
public static class ValidateEasyDrop {
public static string Main(){
var root=new GameObject("Drop test actor");root.transform.position=new Vector3(10000,100,10000);
var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=root.transform.position+new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(10,.2f,10);
var camGo=new GameObject("test camera");camGo.transform.SetParent(root.transform);camGo.transform.localPosition=new Vector3(0,1.5f,0);camGo.transform.rotation=Quaternion.Euler(60,0,0);var cam=camGo.AddComponent<Camera>();cam.enabled=false;
var go=new GameObject("test shovel");go.transform.SetParent(camGo.transform);var blade=new GameObject("tip");blade.transform.SetParent(go.transform);var shovel=go.AddComponent<ShovelTool>();
typeof(ShovelTool).GetField("bladeTip",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(shovel,blade.transform);
try{
cam.transform.rotation=Quaternion.identity;
Physics.SyncTransforms();
shovel.Cargo.Add(GrainType.Rice,4);shovel.Cargo.Add(GrainType.Soybeans,2.5f);
if(!shovel.TryDrop(cam,root.transform))throw new Exception("Ground drop rejected");
shovel.CancelAction();if(shovel.Kilograms!=6.5f)throw new Exception("Cancel lost cargo");
if(!shovel.TryDrop(cam,root.transform))throw new Exception("Second try failed");cam.transform.rotation=Quaternion.Euler(0,80,0);shovel.Tick(.7f);shovel.Tick(.7f);
if(shovel.Kilograms>.001)throw new Exception("Cargo retained after valid drop");
var p=UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None).First(x=>Vector3.Distance(x.transform.position,root.transform.position)<5);
if(Math.Abs(p.Kilograms-6.5)>.001)throw new Exception("Dropped mass wrong");
// Leave a 50 gram remnant, below the previous 2.5 cm targeting cutoff.
var recovered=new GrainCargo();var top=p.transform.position;top.y=p.SurfaceHeight(top);
p.Scoop(top,2,2,6.45f,recovered);
cam.transform.LookAt(p.transform.position+Vector3.right*.3f);
Physics.SyncTransforms();
if(!shovel.TryUse(cam,root.transform)||!shovel.IsScooping)throw new Exception("Small pile near miss not acquired");
shovel.Tick(.6f);shovel.Tick(.7f);
if(shovel.Kilograms<.049f || p.Kilograms>.001)throw new Exception("Thin remnant not recovered: "+shovel.Kilograms+" remaining "+p.Kilograms);
shovel.Cargo.Clear();
// An intervening wall must stop the assisted scoop.
var refill=new GrainCargo();refill.Add(GrainType.Rice,.05f);p.Deposit(p.transform.position,refill);
var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=root.transform.position+new Vector3(0,.8f,.5f);wall.transform.localScale=new Vector3(4,2,.1f);
try{Physics.SyncTransforms();shovel.TryUse(cam,root.transform);if(shovel.IsScooping)throw new Exception("Scooped through wall");shovel.CancelAction();}
finally{UnityEngine.Object.DestroyImmediate(wall);}
return "PASS: forward-looking drop, camera turn during pour, 50 gram near-miss scrape, mass recovery, wall rejection";
}finally{foreach(var p in UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None))if(Vector3.Distance(p.transform.position,root.transform.position)<5)UnityEngine.Object.DestroyImmediate(p.gameObject);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(floor);}
}}
