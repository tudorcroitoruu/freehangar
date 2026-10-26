using UnityEngine;using System;using System.IO;
public static class ValidatePedalingView {
 public static string Run(){var source=UnityEngine.Object.FindFirstObjectByType<BicycleController>();GameObject copy=null,floor=null;try{
 floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(10000,-.5f,10000);floor.transform.localScale=new Vector3(200,1,200);copy=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(10000,.59f,10000),Quaternion.identity);var bike=copy.GetComponent<BicycleController>();bike.SetRidden(true);Physics.SyncTransforms();
 float low=0,high=0;for(int i=0;i<250;i++){bike.Drive(1,0,false,.02f);low=Mathf.Min(low,bike.PedalingCameraPitch);high=Mathf.Max(high,bike.PedalingCameraPitch);}
 if(low>-.1f||high<.1f||low<-.46f||high>.46f)throw new Exception("Pedaling pitch missing or excessive: "+low+" to "+high);
 for(int i=0;i<100;i++)bike.Drive(0,0,false,.02f);if(Mathf.Abs(bike.PedalingCameraPitch)>.001f)throw new Exception("Coasting pitch did not settle");
 for(int i=0;i<60;i++)bike.Drive(1,0,false,.02f);for(int i=0;i<100;i++)bike.Drive(1,0,true,.02f);if(Mathf.Abs(bike.PedalingCameraPitch)>.001f)throw new Exception("Braking pitch did not settle");bike.SetRidden(false);if(bike.PedalingCameraPitch!=0)throw new Exception("Unmount reset");
 string result="PASS: subtle bidirectional crank-driven pitch ("+low.ToString("F3")+" to "+high.ToString("F3")+" degrees), fade on coast/brake, reset on dismount.";File.WriteAllText("output/Bicycle/pedaling-view-validation.txt",result);return result;
 }finally{if(copy)UnityEngine.Object.DestroyImmediate(copy);if(floor)UnityEngine.Object.DestroyImmediate(floor);}}
}
