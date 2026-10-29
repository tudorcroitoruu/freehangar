using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class VerifySlopeSpeed {
 public static object Run(){ return Check("Assets/Models/bicycle/Optimized/Bicycle_Rideable.prefab"); }
 public static object Motor(){ return Check("Assets/Models/motorcycle-old/Optimized/Motoroller_Rideable.prefab"); }
 static object Check(string path){
 var m=typeof(BicycleController).GetMethod("SlopeSpeed",BindingFlags.Static|BindingFlags.NonPublic);
 Func<float,float,bool,float,float,float> step=(speed,throttle,brake,grade,dt)=>(float)m.Invoke(null,new object[]{speed,throttle,brake,dt,7f,2.5f,12f,grade,1.2f});
 Func<float,float,bool,float> simulate=(throttle,grade,brake)=>{float v=0;for(int i=0;i<600;i++)v=step(v,throttle,brake,grade,1f/60);return v;};
 float flat=simulate(1,0,false),gentle=simulate(1,.15f,false),steep=simulate(1,.5f,false),coast=simulate(0,.5f,false),reverse=simulate(-1,-.5f,false);
 float stopped=steep;for(int i=0;i<120;i++)stopped=step(stopped,0,true,.5f,1f/60);
 if(Mathf.Abs(flat-7)>.01||gentle<=flat||gentle>=steep||Mathf.Abs(steep-8.4f)>.01||coast<1||stopped!=0||Mathf.Abs(reverse+2.52f)>.01)throw new Exception("Slope speed regression");
 float transition=step(steep,1,false,0,1f/60);if(transition<=7||transition>=steep)throw new Exception("Flat transition snaps");
 GameObject ramp=null,bike=null;
 try {
 ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.transform.position=new Vector3(2000,100,2000);ramp.transform.rotation=Quaternion.Euler(30,0,0);ramp.transform.localScale=new Vector3(100,1,400);
 bike=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));bike.transform.position=new Vector3(2000,102,2000);bike.transform.rotation=Quaternion.identity;var rig=bike.GetComponent<BicycleController>();bike.GetComponent<MotorollerController>()?.Configure();rig.SetRidden(true);Physics.SyncTransforms();
 for(int i=0;i<900;i++){rig.Drive(1,0,false,1f/60);Physics.SyncTransforms();}
 var alignment=bike.GetComponent<VehicleGroundAlignment>();
 if(!alignment.HasSupport||alignment.GroundNormal.z<.4f||rig.Speed<rig.maximumSpeed*1.07f||rig.Speed>rig.maximumSpeed*1.201f)throw new Exception("Ramp integration failed speed="+rig.Speed+" normal="+alignment.GroundNormal);
 return new{flat,gentle,steep,coast,reverse,stopped,transition,rampSpeed=rig.Speed};
 } finally {if(bike!=null)UnityEngine.Object.DestroyImmediate(bike);if(ramp!=null)UnityEngine.Object.DestroyImmediate(ramp);}
 }
}

