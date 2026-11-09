using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
public static class ValidateMowerParking {
 public static string Main() {
  var scene=EditorSceneManager.NewPreviewScene();
  try {
   var physics=scene.GetPhysicsScene();
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
   SceneManager.MoveGameObjectToScene(floor,scene);
   floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(50,1,50);
   var copy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/4_Lawnmower_With_Trailer.prefab"));
   SceneManager.MoveGameObjectToScene(copy,scene);
   copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
   var mower=copy.GetComponentInChildren<LawnmowerController>();
   var rig=mower.GetComponent<BicycleController>();
   rig.Initialize();if(!Application.isPlaying)mower.SendMessage("Awake");
   var body=mower.trailerJoint.GetComponent<Rigidbody>();
   for(int i=0;i<4;i++){
    var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);
    SceneManager.MoveGameObjectToScene(cargo,scene);
    cargo.name="Stowed bag physics proxy "+i;
    cargo.transform.SetParent(body.transform,false);
    cargo.transform.localPosition=new Vector3(i%2==0?-.45f:.45f,1.1f,-2.1f-(i/2)*1.1f);
    cargo.transform.localScale=new Vector3(.7f,.8f,.8f);
    cargo.AddComponent<Rigidbody>().isKinematic=true;
   }
   Physics.SyncTransforms();
   for(int i=0;i<100;i++){mower.UpdateHitch();physics.Simulate(.02f);}
   Vector3 start=body.position;Quaternion rotation=body.rotation;
   float drift=0,angle=0;
   for(int i=0;i<1000;i++){mower.UpdateHitch();physics.Simulate(.02f);drift=Mathf.Max(drift,Vector3.Distance(start,body.position));angle=Mathf.Max(angle,Quaternion.Angle(rotation,body.rotation));}
   bool parked=body.isKinematic;
   rig.SetRidden(true);mower.UpdateHitch();bool drivingDynamic=!body.isKinematic;
      var trailerStart=body.position;
   for(int i=0;i<100;i++){rig.transform.position+=Vector3.forward*.02f;Physics.SyncTransforms();mower.UpdateHitch();physics.Simulate(.02f);}
   bool follows=body.position.z>trailerStart.z+1f;
   typeof(BicycleController).GetProperty("Speed").SetValue(rig,2f);
   rig.SetRidden(false);mower.UpdateHitch();bool coastingDynamic=rig.IsCoasting&&!body.isKinematic;
   rig.TickUnoccupied(1f);mower.UpdateHitch();bool parkedAgain=body.isKinematic;
   mower.ReconnectTrailer();bool restoredParked=body.isKinematic;
   if(!parked||drift>.00001f||angle>.001f||!drivingDynamic||!follows||!coastingDynamic||!parkedAgain||!restoredParked)
    throw new Exception("Parking regression: parked="+parked+" drift="+drift+" angle="+angle+" dynamic="+drivingDynamic+" follows="+follows+" coast="+coastingDynamic+" parkedAgain="+parkedAgain+" restored="+restoredParked);
   return "parked="+parked+"; 20s loaded idle drift="+drift+"m; rotation="+angle+"deg; drivingDynamic="+drivingDynamic+"; follows="+follows+"; coastingDynamic="+coastingDynamic+"; parkedAgain="+parkedAgain+"; restoredParked="+restoredParked;
  } finally {EditorSceneManager.ClosePreviewScene(scene);}
 }
}


