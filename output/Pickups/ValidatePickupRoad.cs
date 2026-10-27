using UnityEngine;using System.Linq;using System;using System.Text;using System.IO;
public static class ValidatePickupRoad {
 public static string Main(){
 if(!Application.isPlaying)throw new Exception("Run in Play Mode");
 var sources=UnityEngine.Object.FindObjectsByType<MotorollerController>(FindObjectsSortMode.None).Where(m=>m.GetComponent<PickupController>()!=null);var report=new StringBuilder();int failures=0;
 foreach(var source in sources)foreach(float offset in new[]{-1f,0f,1f})foreach(bool returning in new[]{false,true}){
 GameObject copy=null;try{
 var a=new Vector3(39,0,87.9f+offset);var b=new Vector3(60,0,97.56f+offset);Vector3 start=returning?b:a;Vector3 end=returning?a:b;Vector3 direction=(end-start).normalized;
 Physics.Raycast(start+Vector3.up*100,Vector3.down,out var ground,200,~0,QueryTriggerInteraction.Ignore);start.y=ground.point.y+.03f;
 copy=UnityEngine.Object.Instantiate(source.gameObject,start,Quaternion.LookRotation(direction));var engine=copy.GetComponent<MotorollerController>();engine.Movement.SetRidden(true);Physics.SyncTransforms();
 int blocked=0;bool reached=false;
 for(int i=0;i<600;i++){engine.Drive(1,0,false,.02f);Physics.SyncTransforms();if(engine.Speed<.01f)blocked++;if(Vector3.Dot(copy.transform.position-start,direction)>=23.1f){reached=true;break;}}
 if(!reached)failures++;
 report.AppendLine(source.name+" offset="+offset+" return="+returning+": end="+copy.transform.position+" blockedFrames="+blocked+" reached="+reached);
 }finally{if(copy)UnityEngine.Object.DestroyImmediate(copy);}}
 File.WriteAllText("output/Pickups/road-travel-results.txt",report.ToString());if(failures>0)throw new Exception(failures+" road traversals failed\n"+report);return report.ToString();}
}

