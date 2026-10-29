using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
public static class VerifyCargoRacks
{
 static float Bottom(GameObject obj){return obj.GetComponent<BoxCollider>().bounds.min.y;}
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var results=new List<object>();
  foreach(var path in new[]{"Assets/Models/bicycle/Optimized/Bicycle_Rideable.prefab","Assets/Models/motorcycle-old/Optimized/Motoroller_Rideable.prefab"})
  {
   GameObject root=null,a=null,b=null,bag=null;
   try
   {
    root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.transform.position=new Vector3(1000,20,1000);root.transform.localScale=Vector3.one*1.46f;
    var rack=root.GetComponent<VehicleCargoRack>();var rig=root.GetComponent<BicycleController>();
    a=GameObject.CreatePrimitive(PrimitiveType.Cube);a.transform.localScale=new Vector3(.4f,.25f,.4f);var ca=a.AddComponent<BicycleCargoBox>();
    b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.transform.localScale=new Vector3(.4f,.3f,.4f);var cb=b.AddComponent<BicycleCargoBox>();
    var lower=rig.FreeCargoSlot();if(!ca.Load(lower))throw new Exception("First box rejected");
    rack.Reflow();var upper=rig.FreeCargoSlot();if(!cb.Load(upper))throw new Exception("Second box rejected");rack.Reflow();Physics.SyncTransforms();
    float rackGap=Bottom(a)-rack.surface.position.y;float stackGap=Bottom(b)-a.GetComponent<BoxCollider>().bounds.max.y;
    if(Mathf.Abs(rackGap)>.002f||Mathf.Abs(stackGap)>.002f)throw new Exception("Box floating or intersecting: "+rackGap+","+stackGap);
    if(rack.UsedSlots!=2)throw new Exception("Box capacity wrong");
    if(rig.FreeBagCargoSlot()==lower || rig.FreeBagCargoSlot()==upper)throw new Exception("Bag overlaps boxes");
    ca.Unload(new Vector3(1010,20,1000));rack.Reflow();Physics.SyncTransforms();float settled=Bottom(b)-rack.surface.position.y;
    if(Mathf.Abs(settled)>.002f)throw new Exception("Upper box did not settle");
    cb.Unload(new Vector3(1011,20,1000));
    bag=GameObject.CreatePrimitive(PrimitiveType.Cube);bag.name="Test sack";bag.transform.localScale=new Vector3(.6f,.5f,.6f);bag.GetComponent<BoxCollider>().center=new Vector3(.1f,.35f,-.2f);bag.AddComponent<GrainContainer>();
    var slot=rig.FreeBagCargoSlot();bag.transform.SetParent(slot,true);VehicleCargoRack.Seat(bag.transform,slot);rack.Reflow();Physics.SyncTransforms();float bagGap=Bottom(bag)-rack.surface.position.y;
    if(Mathf.Abs(bagGap)>.002f || rack.UsedSlots!=2)throw new Exception("Bag support/capacity wrong");
    if(rig.FreeCargoSlot()==lower||rig.FreeCargoSlot()==upper)throw new Exception("Box overlaps sack");
    results.Add(new{path,rackGap,stackGap,settled,bagGap,bagSlotCost=rack.UsedSlots});
   }
   finally{if(a!=null)UnityEngine.Object.DestroyImmediate(a);if(b!=null)UnityEngine.Object.DestroyImmediate(b);if(bag!=null)UnityEngine.Object.DestroyImmediate(bag);if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
  }
  return results;
 }
}
