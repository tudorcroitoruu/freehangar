using UnityEngine;using System;using System.Linq;using System.IO;
public static class ValidateSteeringRig {
 public static string Run(){GameObject copy=null,floor=null;Mesh baked=null;int checks=0;void Check(bool ok,string m){if(!ok)throw new Exception(m);checks++;}
 try{var source=UnityEngine.Object.FindFirstObjectByType<BicycleController>();floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(10000,-.5f,10000);floor.transform.localScale=new Vector3(100,1,100);copy=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(10000,1,10000),Quaternion.identity);var bike=copy.GetComponent<BicycleController>();bike.SetRidden(true);Physics.SyncTransforms();
 Check(bike.frontWheel.IsChildOf(bike.steering),"Wheel must share the steering pivot");
 var fork=bike.steering.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Steering");var vertices=fork.sharedMesh.vertices;int nearest=0;float restGap=float.MaxValue;
 for(int i=0;i<vertices.Length;i++){float gap=Vector3.ProjectOnPlane(fork.transform.TransformPoint(vertices[i])-bike.frontWheel.position,bike.frontWheel.right).magnitude;if(gap<restGap){restGap=gap;nearest=i;}}
 var frontRest=bike.frontWheel.localRotation;var skin=bike.GetComponentInChildren<SkinnedMeshRenderer>();var cableVertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;int moving=Array.FindIndex(weights,w=>(w.boneIndex0==1?w.weight0:0)+(w.boneIndex1==1?w.weight1:0)>.999f);int fixedIndex=Array.FindIndex(weights,w=>(w.boneIndex0==0?w.weight0:0)+(w.boneIndex1==0?w.weight1:0)>.999f);Check(moving>=0&&fixedIndex>=0,"Flexible cable has both anchors");
 var movingLocal=bike.steering.InverseTransformPoint(skin.transform.TransformPoint(cableVertices[moving]));var staticLocal=bike.transform.InverseTransformPoint(skin.transform.TransformPoint(cableVertices[fixedIndex]));baked=new Mesh();float worstGapError=0,worstCableError=0;
 foreach(float turn in new[]{1f,-1f,0f})
 {
  for(int i=0;i<100;i++)bike.Drive(0,turn,true,.02f);
  Check(Quaternion.Angle(frontRest,bike.frontWheel.localRotation)<.01f,"Front wheel must not receive steering twice");
  float gap=Vector3.ProjectOnPlane(fork.transform.TransformPoint(vertices[nearest])-bike.frontWheel.position,bike.frontWheel.right).magnitude;worstGapError=Mathf.Max(worstGapError,Mathf.Abs(gap-restGap));Check(Mathf.Abs(gap-restGap)<.001f,"Fork remains attached to axle at full lock");
  skin.BakeMesh(baked);var actual=baked.vertices;float movingError=Vector3.Distance(skin.transform.TransformPoint(actual[moving]),bike.steering.TransformPoint(movingLocal));float fixedError=Vector3.Distance(skin.transform.TransformPoint(actual[fixedIndex]),bike.transform.TransformPoint(staticLocal));worstCableError=Mathf.Max(worstCableError,Mathf.Max(movingError,fixedError));Check(movingError<.003f&&fixedError<.003f,"Cable stays attached to moving lever and fixed frame: "+movingError+", "+fixedError);
 }
 var heading=bike.transform.rotation;for(int i=0;i<100;i++)bike.Drive(1,.5f,false,.02f);Check(Quaternion.Angle(heading,bike.transform.rotation)>5,"Whole bike turns while riding");float lastGap=Vector3.ProjectOnPlane(fork.transform.TransformPoint(vertices[nearest])-bike.frontWheel.position,bike.frontWheel.right).magnitude;Check(Mathf.Abs(lastGap-restGap)<.002f,"Wheel spin does not detach fork");
 string result="PASS: "+checks+" steering hierarchy/axle/skin/full-lock/whole-bike-turn checks. Worst axle gap change="+worstGapError.ToString("F6")+"m, cable anchor error="+worstCableError.ToString("F6")+"m.";File.WriteAllText("output/Bicycle/steering-rig-validation.txt",result);return result;
 }finally{if(copy)UnityEngine.Object.DestroyImmediate(copy);if(floor)UnityEngine.Object.DestroyImmediate(floor);if(baked)UnityEngine.Object.DestroyImmediate(baked);}}
}

