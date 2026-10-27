using UnityEngine;using System;using System.Linq;using System.IO;
public static class ValidateScooterRegression {
 public static string Main(){
 if(!Application.isPlaying)throw new Exception("Run in Play Mode");
 GameObject floor=null,wall=null,copy=null,playerObject=null;int checks=0;
 void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 try{
 var source=UnityEngine.Object.FindObjectsByType<MotorollerController>(FindObjectsSortMode.None).First(m=>m.GetComponent<TrikeController>()==null&&m.GetComponent<LawnmowerController>()==null);
 floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(10000,-.5f,10000);floor.transform.localScale=new Vector3(300,1,300);
 copy=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(10000,.02f,10000),Quaternion.identity);var scooter=copy.GetComponent<MotorollerController>();var bike=scooter.Movement;
 playerObject=new GameObject("Test motoroller rider");playerObject.SetActive(false);var camera=new GameObject("Test camera").AddComponent<Camera>();camera.enabled=false;camera.transform.SetParent(playerObject.transform,false);camera.transform.localPosition=Vector3.up*1.7f;
 var cc=playerObject.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;
 var player=playerObject.AddComponent<PlayerController>();player.characterCamera=camera;player.characterController=cc;player.enabled=false;
 var rider=playerObject.AddComponent<PlayerBicycleInteraction>();rider.player=player;rider.bicycle=bike;
 playerObject.transform.position=copy.transform.position+Vector3.left*1.5f;playerObject.SetActive(true);Physics.SyncTransforms();
 Check(bike.motorDriven&&bike.crank==null,"No dummy pedals needed");
 Check(bike.frontWheel.IsChildOf(bike.steering),"Front wheel shares fork steering pivot");
 Check(copy.transform.InverseTransformPoint(bike.frontWheel.position).z>.6f,"Root forward is +Z");
 Check(copy.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)<14000,"Optimized triangle budget including working gauge");
 var cameraParent=camera.transform.parent;var cameraRest=camera.transform.localPosition;
 Check(rider.TryMount(),"Mount scooter");Check(!cc.enabled&&rider.IsRiding,"Walking disabled while mounted");Check(camera.transform.parent==bike.riderView,"Camera attached");
 var axleLocal=bike.frontWheel.localPosition;var wheelRest=bike.frontWheel.localRotation;
 foreach(float turn in new[]{1f,-1f,0f}){
 for(int i=0;i<80;i++)rider.DriveRiding(0,turn,true,false,.02f);
 Check(Vector3.Distance(axleLocal,bike.frontWheel.localPosition)<.00001f,"Axle fixed relative to fork at lock");
 Check(Quaternion.Angle(wheelRest,bike.frontWheel.localRotation)<.01f,"Steering inherited once");}
 var start=copy.transform.position;float stamina=player.Stamina.CurrentStamina;
 for(int i=0;i<180;i++)rider.DriveRiding(1,0,false,true,.02f);
 Check(scooter.Speed>11.5f,"Engine reaches configured speed");Check(copy.transform.position.z>start.z+10,"Forward travel");
 Check(player.Stamina.CurrentStamina>=stamina-.001f&&!rider.IsBoosting,"Motor drive does not spend stamina even with Shift");
 Check(Quaternion.Angle(wheelRest,bike.frontWheel.localRotation)>1,"Wheel spins about axle");Check(bike.PedalingCameraPitch==0,"No pedaling camera motion");
 for(int i=0;i<90;i++)rider.DriveRiding(0,0,true,false,.02f);Check(Mathf.Abs(scooter.Speed)<.001f,"Brake stops");
 start=copy.transform.position;for(int i=0;i<60;i++)rider.DriveRiding(-1,0,false,false,.02f);Check(copy.transform.position.z<start.z-.5f,"Reverse works");
 for(int i=0;i<50;i++)rider.DriveRiding(0,0,true,false,.02f);
 var heading=copy.transform.rotation;for(int i=0;i<80;i++)rider.DriveRiding(1,.5f,false,false,.02f);Check(Quaternion.Angle(heading,copy.transform.rotation)>10,"Steering turns vehicle");
 for(int i=0;i<80;i++)rider.DriveRiding(0,0,true,false,.02f);
 Check(rider.TryDismount(),"Safe dismount");Check(cc.enabled&&camera.transform.parent==cameraParent&&Vector3.Distance(camera.transform.localPosition,cameraRest)<.001f,"Walking and camera restored");
 // Reposition only the isolated fixture for a frontal obstruction test.
 bike.GetComponent<CharacterController>().enabled=false;copy.transform.SetPositionAndRotation(new Vector3(10000,.02f,10000),Quaternion.identity);bike.GetComponent<CharacterController>().enabled=true;
 cc.enabled=false;playerObject.transform.position=copy.transform.position+Vector3.left*1.5f;cc.enabled=true;Physics.SyncTransforms();Check(rider.TryMount(),"Remount");
 wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(10000,1,10005);wall.transform.localScale=new Vector3(8,2,.2f);Physics.SyncTransforms();
 for(int i=0;i<300;i++){rider.DriveRiding(1,0,false,false,.02f);Physics.SyncTransforms();}
 Check(copy.transform.position.z>10003&&copy.transform.position.z<10004,"Nose stays behind wall");Check(copy.transform.position.y>-.05f&&copy.transform.position.y<.1f,"Ground support");
 Check(rider.TryDismount(),"Dismount near wall");
 string result="PASS: "+checks+" Motoroller Play Mode checks (rig, mounting, engine, reverse, braking, steering, stamina, collision and camera restoration).";File.WriteAllText("output/Trike/scooter-regression.txt",result);return result;
 }finally{if(playerObject)UnityEngine.Object.DestroyImmediate(playerObject);if(copy)UnityEngine.Object.DestroyImmediate(copy);if(wall)UnityEngine.Object.DestroyImmediate(wall);if(floor)UnityEngine.Object.DestroyImmediate(floor);}
 }
}



