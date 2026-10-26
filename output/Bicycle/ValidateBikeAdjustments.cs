using UnityEngine;using System;using System.IO;using System.Reflection;
public static class ValidateBikeAdjustments {
 public static string Run(){
 var source=UnityEngine.Object.FindFirstObjectByType<BicycleController>(); var bikeObject=UnityEngine.Object.Instantiate(source.gameObject);bikeObject.name="Isolated bike validation";var bike=bikeObject.GetComponent<BicycleController>();var rig=new GameObject("Isolated rider validation");var camera=new GameObject("Test view").AddComponent<Camera>();camera.enabled=false;camera.transform.SetParent(rig.transform,false);var cc=rig.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;var player=rig.AddComponent<PlayerController>();player.enabled=false;player.characterCamera=camera;player.characterController=cc;var rider=rig.AddComponent<PlayerBicycleInteraction>();rider.player=player;rider.bicycle=bike;var staminaObject=new GameObject("External stamina validation",typeof(RectTransform),typeof(UnityEngine.UI.Image));var stamina=staminaObject.AddComponent<StaminaManager>();typeof(StaminaManager).GetField("staminaBarFill",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(stamina,staminaObject.GetComponent<UnityEngine.UI.Image>());typeof(StaminaManager).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stamina,null);typeof(PlayerController).GetField("staminaManager",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,stamina);
 GameObject floor=null;int checks=0;void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 string key="Bicycle.View."+bike.gameObject.scene.name+"."+bike.gameObject.name;bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");
 try{
 floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Bike test ground";floor.transform.position=new Vector3(10000,-.5f,10000);floor.transform.localScale=new Vector3(500,1,500);
 bike.GetComponent<CharacterController>().enabled=false;bike.transform.SetPositionAndRotation(new Vector3(10000,.59f,10000),Quaternion.identity);bike.GetComponent<CharacterController>().enabled=true;
 player.characterController.enabled=false;player.transform.position=bike.transform.position+Vector3.right*1.1f-Vector3.up*.54f;player.characterController.enabled=true;Physics.SyncTransforms();
 Check(rig.GetComponent<StaminaManager>()==null,"Stamina lives outside the Player, as in Gameplay");Check(player.Stamina==stamina,"Player shares external stamina reference");Check(rider.TryMount(),"Mount");
 rider.DriveRiding(0,1,true,false,.02f);float first=bike.CurrentSteeringAngle;Check(first>0&&first<10,"Steering eases instead of snapping");
 for(int i=0;i<60;i++)rider.DriveRiding(0,1,true,false,.02f);Check(bike.CurrentSteeringAngle>29,"Steering reaches target");
 rider.DriveRiding(0,-1,true,false,.02f);Check(bike.CurrentSteeringAngle>0,"Direction reversal is smooth");
 for(int i=0;i<60;i++)rider.DriveRiding(0,0,true,false,.02f);Check(Mathf.Abs(bike.CurrentSteeringAngle)<.1f,"Steering centers smoothly");
 float initial=stamina.CurrentStamina;
 for(int i=0;i<120;i++)rider.DriveRiding(1,0,false,false,.02f);
 Check(stamina.CurrentStamina>=initial-.001f,"Normal pedaling costs no stamina");
 for(int i=0;i<80;i++)rider.DriveRiding(1,0,false,true,.02f);
 Check(bike.Speed>bike.maximumSpeed+.2f,"Shift exceeds normal speed");Check(stamina.CurrentStamina<initial-5,"Shift consumes stamina");Check(rider.IsBoosting,"Boost state active");Check(staminaObject.GetComponent<UnityEngine.UI.Image>().fillAmount<.99f,"Boost updates stamina HUD");
 for(int i=0;i<500;i++)rider.DriveRiding(1,0,false,true,.02f);
 Check(!rider.IsBoosting,"Exhaustion ends boost");Check(bike.Speed<=bike.maximumSpeed+.1f,"Exhaustion returns to normal speed");
 float exhausted=stamina.CurrentStamina;
 for(int i=0;i<100;i++)rider.DriveRiding(1,0,false,false,.02f);
 Check(stamina.CurrentStamina>exhausted,"Normal pedaling recovers stamina after delay");
 rider.DriveRiding(0,0,true,false,.3f);float rest=stamina.CurrentStamina;rider.DriveRiding(0,0,true,true,.02f);Check(stamina.CurrentStamina>=rest,"No boost drain while stopped/braking");
 bicycleView(bike,new Vector3(.1f,.2f,-.2f),7);rider.Tick(false);
 Check(Vector3.Distance(player.characterCamera.transform.localPosition,bike.RidingCameraOffset)<.0001f,"Camera offset applied");
 var original=bike.RidingCameraOffset;
 bike.SetRidingCamera(new Vector3(-.1f,.12f,-.13f),8);bike.SaveRidingCamera();Check(PlayerPrefs.HasKey(key),"Saved view retained");var expected=bike.RidingCameraOffset;bike.SetRidingCamera(Vector3.zero,0);typeof(BicycleController).GetMethod("LoadRidingCamera",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bike,null);Check(Vector3.Distance(expected,bike.RidingCameraOffset)<.0001f,"Authored view loads");
 bike.SetRidingCamera(Vector3.one*100,100);Check(bike.RidingCameraOffset==new Vector3(.5f,.7f,.6f)&&bike.RidingCameraPitch==35,"View bounds are clamped");
 Check(rider.TryDismount(),"Dismount after adjustment");Check(player.characterCamera.transform.parent!=bike.riderView&&player.characterController.enabled,"Walking camera restored");
 string result="PASS: "+checks+" checks for steering smoothing, boost/stamina/exhaustion/recovery, camera offsets/save/reload/clamps and dismount.";File.WriteAllText("output/Bicycle/adjustments-validation.txt",result);return result;
 }finally{if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(staminaObject);UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(bikeObject);if(floor)UnityEngine.Object.DestroyImmediate(floor);}
 }
 static void bicycleView(BicycleController bike,Vector3 offset,float pitch)=>bike.SetRidingCamera(offset,pitch);
}




