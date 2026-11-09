using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateWheelbarrowBags
{
 public static object Run()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");
  int checkedBags=0;
  var sources=UnityEngine.Object.FindObjectsByType<WheelbarrowCargo>(FindObjectsInactive.Include,FindObjectsSortMode.None);
  foreach(var source in sources)foreach(string id in new[]{"FreeBag","SmallBag","LargeBag"})foreach(float fill in new[]{0f,1f})
  {
   var root=new GameObject(source.name);
   try
   {
    var barrow=root.AddComponent<WheelbarrowCargo>();
    EditorUtility.CopySerialized(source,barrow);
    root.transform.SetPositionAndRotation(new Vector3(10000,0,10000),Quaternion.Euler(0,31,0));
    root.transform.localScale=source.transform.lossyScale;
    var settings=new SerializedObject(barrow);
    var center=settings.FindProperty("trayCenter").vector3Value;
    var size=settings.FindProperty("traySize").vector3Value;
    var footprints=new System.Collections.Generic.List<Bounds>();
    for(int i=0;i<barrow.CargoSlotCount;i++)
    {
     var slot=new GameObject("Bag slot "+i).transform;slot.SetParent(root.transform,false);
     slot.localPosition=barrow.SlotPosition(i);slot.localRotation=barrow.SlotRotation;
     var item=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/"+id+".prefab"),slot);
     VehicleCargoPreview.PrepareContainer(item,fill,fill);
     var bag=item.GetComponent<GrainContainer>();
     VehicleCargoPlacement.Attach(item.transform,slot,true);
     item.GetComponent<VehicleCargoPlacement>().Apply();
     var b=VehicleCargoRack.ContactBounds(item.transform,root.transform);
     if(Quaternion.Angle(item.transform.rotation,root.transform.rotation*bag.UprightRotation)>.1f)throw new Exception("Tilted bag");
     if(b.min.x<center.x-size.x/2-.002f||b.max.x>center.x+size.x/2+.002f||b.min.z<center.z-size.z/2-.002f||b.max.z>center.z+size.z/2+.002f)throw new Exception(source.name+" "+id+" outside tray "+b);
     foreach(var previous in footprints)if(b.min.x<previous.max.x&&b.max.x>previous.min.x&&b.min.z<previous.max.z&&b.max.z>previous.min.z)throw new Exception("Overlapping bags");
     footprints.Add(b);checkedBags++;
    }
   }
   finally {UnityEngine.Object.DestroyImmediate(root);}
  }
  return new{wheelbarrows=sources.Length,checkedBags};
 }
}
