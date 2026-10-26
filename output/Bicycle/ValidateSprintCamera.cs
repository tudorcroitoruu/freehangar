using UnityEngine;using System;using System.Reflection;using System.IO;
public static class ValidateSprintCamera {
 public static string Run(){var rig=new GameObject("Sprint camera validation");try{
 var eye=new GameObject("Test eye").AddComponent<Camera>();eye.enabled=false;eye.transform.SetParent(rig.transform,false);eye.transform.localPosition=Vector3.up*1.74f;eye.transform.localRotation=Quaternion.Euler(10,0,0);
 var cc=rig.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;var player=rig.AddComponent<PlayerController>();player.characterCamera=eye;player.characterController=cc;var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(PlayerController).GetMethod("Awake",flags).Invoke(player,null);player.enabled=false;
 var update=typeof(PlayerController).GetMethod("UpdateSprintCamera",flags);float minY=0,maxY=0,minPitch=0,maxPitch=0;
 for(int i=0;i<200;i++){update.Invoke(player,new object[]{true,.02f});float y=eye.transform.localPosition.y-1.74f;float pitch=Mathf.DeltaAngle(10,eye.transform.localEulerAngles.x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);minPitch=Mathf.Min(minPitch,pitch);maxPitch=Mathf.Max(maxPitch,pitch);}
 if(minY>-.003f||maxY<.003f||Mathf.Abs(minY)>.0076f||maxY>.0076f||minPitch>-.1f||maxPitch<.1f||Mathf.Abs(minPitch)>.282f||maxPitch>.282f)throw new Exception("Sprint motion missing or excessive");
 for(int i=0;i<120;i++)update.Invoke(player,new object[]{false,.02f});if(Vector3.Distance(eye.transform.localPosition,Vector3.up*1.74f)>.0001f||Mathf.Abs(Mathf.DeltaAngle(10,eye.transform.localEulerAngles.x))>.001f)throw new Exception("Sprint motion failed to settle");
 cc.height=1.2f;for(int i=0;i<100;i++)update.Invoke(player,new object[]{false,.02f});if(Mathf.Abs(eye.transform.localPosition.y-1.14f)>.0001f)throw new Exception("Crouch baseline drift");
 cc.height=1.8f;for(int i=0;i<20;i++)update.Invoke(player,new object[]{true,.02f});player.ClearSprintCameraMotion();if(Vector3.Distance(eye.transform.localPosition,Vector3.up*1.74f)>.0001f||Mathf.Abs(Mathf.DeltaAngle(10,eye.transform.localEulerAngles.x))>.001f)throw new Exception("Mount cleanup retained sprint offset");
 string result="PASS: sprint camera bob bounded to 0.75cm / 0.28125 degrees, resting fade, crouch baseline and bike-mount cleanup.";File.WriteAllText("output/Bicycle/sprint-camera-validation.txt",result);return result;
 }finally{UnityEngine.Object.DestroyImmediate(rig);}}
}
