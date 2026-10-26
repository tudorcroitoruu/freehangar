using UnityEngine;using System;using System.IO;
public static class ValidateBicycle {
 public static string Run(){
 var bike=UnityEngine.Object.FindFirstObjectByType<BicycleController>();var rider=UnityEngine.Object.FindFirstObjectByType<PlayerBicycleInteraction>();var p=rider.player;
 void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 p.characterController.enabled=false;p.transform.position=bike.transform.position+bike.transform.right*1.1f-Vector3.up*.45f;p.characterController.enabled=true;Physics.SyncTransforms();
 Check(bike.frontWheel.localPosition.z>bike.rearWheel.localPosition.z,"Forward axis");
 Check(rider.TryMount(),"Mount");Check(rider.IsRiding&&!p.characterController.enabled,"Walking suspended");Check(p.characterCamera.transform.parent==bike.riderView,"Rider camera");
 Vector3 start=bike.transform.position;var front=bike.frontWheel.localRotation;
 for(int i=0;i<20;i++)bike.Drive(1,.1f,false,.02f);
 Check(bike.Speed>0,"Accelerate");Check(bike.frontWheel.localRotation!=front,"Wheel animation");
 for(int i=0;i<20;i++)bike.Drive(0,0,true,.02f);Check(bike.Speed==0,"Brake");
 Check(rider.TryDismount(),"Safe dismount");Check(!rider.IsRiding&&p.characterController.enabled,"Walking restored");
 Check(p.characterCamera.transform.parent!=bike.riderView,"Camera restored");Check(rider.TryMount(),"Remount");Check(rider.TryDismount(),"Repeat dismount");
 var box=UnityEngine.Object.FindFirstObjectByType<BicycleCargoBox>();Check(box.Load(bike.FreeCargoSlot()),"Load box");Check(box.transform.IsChildOf(bike.transform),"Box attached");Check(box.GetComponent<Rigidbody>().isKinematic,"Cargo physics secured");Check(!box.Load(bike.FreeCargoSlot()),"Reject duplicate");Check(box.Unload(bike.transform.position+bike.transform.right*1.5f),"Unload box");
 string result="17 Play Mode bicycle checks passed: +Z forward, mount/camera, movement/wheels/braking, dismount/remount and box cargo.";File.WriteAllText("output/Bicycle/validation.txt",result);return result;
 }
}

