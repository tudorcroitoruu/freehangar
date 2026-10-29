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
   GameObject root=null,a=null,b=null,bag=null,carrier=null;
   try
   {
    root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.transform.position=new Vector3(1000,20,1000);root.transform.localScale=Vector3.one*1.46f;
    var upgrades=root.GetComponent<VehicleUpgrade>()??root.AddComponent<VehicleUpgrade>();
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
    bag=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GrainPrototype/Grains/Sorting Sack.prefab"));
    var container=bag.GetComponent<GrainContainer>();var data=new SerializedObject(container);
    var closed=data.FindProperty("closedBounds").boundsValue;bag.GetComponent<BoxCollider>().center=closed.center;bag.GetComponent<BoxCollider>().size=closed.size;
    float closedVolume=VehicleUpgrade.MeasureVolume(bag);
    if(closedVolume<=.4f)throw new Exception("Regression fixture must exceed old bicycle volume limit");
    if(!upgrades.CanLoadVolume(closedVolume,2))throw new Exception("Empty rack rejects real closed sack");
    carrier=new GameObject("Temporary bag carrier");var carry=carrier.AddComponent<BagInteraction>();
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(BagInteraction).GetField("carryAnchor",flags).SetValue(carry,carrier.transform);
    if(!(bool)typeof(BagInteraction).GetMethod("Take",flags).Invoke(carry,new object[]{container}))throw new Exception("Take failed");
    if(!carry.TryStow(rig.FreeBagCargoSlot()))throw new Exception("Real closed sack stow rejected");
    rack.Reflow();Physics.SyncTransforms();float bagGap=Bottom(bag)-rack.surface.position.y;
    if(Mathf.Abs(bagGap)>.002f || rack.UsedSlots!=2)throw new Exception("Bag support/capacity wrong");
    if(rig.FreeCargoSlot()==lower||rig.FreeCargoSlot()==upper)throw new Exception("Box overlaps sack");
    results.Add(new{path,rackGap,stackGap,settled,bagGap,bagSlotCost=rack.UsedSlots,closedVolume,realSackStowed=true});
   }
   finally{if(carrier!=null)UnityEngine.Object.DestroyImmediate(carrier);if(a!=null)UnityEngine.Object.DestroyImmediate(a);if(b!=null)UnityEngine.Object.DestroyImmediate(b);if(bag!=null)UnityEngine.Object.DestroyImmediate(bag);if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
  }
  return results;
 }
}
