using System;
using System.Reflection;
using UnityEngine;
public static class ValidateSellArrow {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
 static void Call(object o,string m)=>o.GetType().GetMethod(m,Flags).Invoke(o,null);
 static void Check(bool pass,string message) { if(!pass) throw new Exception(message); }
 public static object Run() {
 var root=new GameObject("Arrow validation"); root.transform.position=new Vector3(30000,0,30000);
 try {
 var area=root.AddComponent<BoxCollider>();area.isTrigger=true;area.size=new Vector3(10,6,10);
 var zone=root.AddComponent<SellingZone>();Set(zone,"allGrainsSettings",new[]{new GrainElementSettings{grainType=GrainType.Corn,grainPrice=2}});
 var p=new GameObject("Player");p.transform.SetParent(root.transform,false);p.transform.localPosition=Vector3.right*20;var player=p.AddComponent<PlayerInteractor>();
 var b=new GameObject("Button");b.transform.SetParent(root.transform,false);var button=b.AddComponent<SellingWeighButton>();
 var a=new GameObject("Arrow");a.transform.SetParent(b.transform,false);a.transform.localPosition=Vector3.up*3;
 Set(button,"readyArrow",a.transform);Set(button,"sellingZone",zone);Set(button,"player",player);Call(button,"Awake");Physics.SyncTransforms();Call(button,"Update");Check(!a.activeSelf,"Empty scale arrow");
 var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);cargo.transform.SetParent(root.transform,false);var bag=cargo.AddComponent<GrainContainer>();bag.SetDemonstrationFill(10);Physics.SyncTransforms();Set(button,"nextArrowScan",0f);Call(button,"Update");Check(a.activeSelf,"Loaded scale arrow");
 Set(button,"arrowTime",.75f);Call(button,"Update");Check(a.transform.localPosition.y>3.2f,"Arrow does not bob upward");
 Set(button,"arrowTime",0f);Call(button,"Update");Check(Mathf.Abs(a.transform.localPosition.y-3f)<.001f,"Arrow loop does not return");
 player.transform.localPosition=Vector3.zero;Physics.SyncTransforms();Set(button,"nextArrowScan",0f);Call(button,"Update");Check(!a.activeSelf,"Player on scale arrow");
 player.transform.localPosition=Vector3.right*20;bag.SetDemonstrationFill(0);Physics.SyncTransforms();Set(button,"nextArrowScan",0f);Call(button,"Update");Check(!a.activeSelf,"Sold/empty cargo arrow");
 bag.SetDemonstrationFill(10);Set(button,"nextArrowScan",0f);Call(button,"Update");Check(a.activeSelf,"Arrow reactivation");Call(button,"OnDisable");Check(!a.activeSelf && a.transform.localPosition==Vector3.up*3,"Disable cleanup");
 return "PASS: empty, loaded, bob up, loop return, blocked, emptied after sale, reactivation, disable reset (Edit Mode fixture)";
 } finally { UnityEngine.Object.DestroyImmediate(root); }
 }
}
