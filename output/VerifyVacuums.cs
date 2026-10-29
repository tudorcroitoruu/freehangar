using UnityEngine;using UnityEditor;using System;using System.Reflection;using System.Linq;using System.Collections.Generic;
public static class VerifyVacuums {
 public static object Run(){if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var results=new List<object>();var source=UnityEngine.Object.FindFirstObjectByType<PlayerController>().HeldInstrument;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
 for(int tier=5;tier<=6;tier++){
 GameObject actor=null,pileGo=null,receiver=null,wall=null;try{
 actor=new GameObject("Vacuum test actor");actor.transform.position=new Vector3(5000,0,5000);var cameraGo=new GameObject("Test camera");cameraGo.transform.SetParent(actor.transform,false);var camera=cameraGo.AddComponent<Camera>();camera.transform.localPosition=new Vector3(0,3.5f,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
 var hands=UnityEngine.Object.Instantiate(source.gameObject,actor.transform);var tool=hands.GetComponent<ShovelTool>();var levels=hands.transform.Find("Item In Hands");foreach(Transform level in levels)level.gameObject.SetActive(level.name=="LVL_"+tier);
 typeof(ShovelTool).GetMethod("Awake",flags).Invoke(tool,null);var vac=tool.ActiveVacuum;vac.Initialize();
 pileGo=new GameObject("Test grain pile",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));pileGo.transform.position=actor.transform.position;var pile=pileGo.AddComponent<GrainPile>();pile.Initialize();Physics.SyncTransforms();double start=pile.Kilograms;float startHeight=pile.SurfaceHeight(actor.transform.position);
 for(int i=0;i<900 && tool.Kilograms<tool.CapacityKg-.001f;i++){camera.transform.position=actor.transform.position+Vector3.up*(pile.SurfaceHeight(actor.transform.position)+1.6f);tool.TryUse(camera,actor.transform);tool.Tick(1f/60);}
 float collected=tool.Kilograms;if(collected<tool.CapacityKg-.01f||collected>tool.CapacityKg+.001f)throw new Exception("Capacity/suction failed "+tier+": "+collected);
 if(Math.Abs(start-pile.Kilograms-collected)>.1)throw new Exception("Collection lost mass");
 var moving=vac.fillFrame.GetComponent<MeshFilter>().sharedMesh.vertices;tool.Tick(.8f);var settled=vac.fillFrame.GetComponent<MeshFilter>().sharedMesh.vertices;bool animated=false;for(int n=0;n<moving.Length;n++)if(Vector3.Distance(moving[n],settled[n])>.002f){animated=true;break;}if(!animated)throw new Exception("Cylinder grain did not animate from inlet");
 tool.Tick(1f/60);if(vac.Sucking)throw new Exception("Suction did not stop after release");
 receiver=GameObject.CreatePrimitive(PrimitiveType.Cube);receiver.transform.position=actor.transform.position+new Vector3(2,1,0);var bag=receiver.AddComponent<GrainContainer>();typeof(GrainContainer).GetField("capacityKg",flags).SetValue(bag,4000f);camera.transform.position=receiver.transform.position+Vector3.up*2;camera.transform.rotation=Quaternion.Euler(90,0,0);Physics.SyncTransforms();var tray=vac.dustTray.localPosition;
 if(!tool.TryDrop(camera,actor.transform))throw new Exception("Empty command rejected");float excursion=0;for(int i=0;i<240;i++){tool.Tick(1f/60);excursion=Mathf.Max(excursion,Vector3.Distance(vac.dustTray.localPosition,tray));}
 if(tool.Kilograms>.01f||Mathf.Abs(bag.Kilograms-collected)>.01f)throw new Exception("Unloading failed "+tool.Kilograms+" received="+bag.Kilograms);
 if(excursion<.01f||Vector3.Distance(vac.dustTray.localPosition,tray)>.0001f||tool.IsBusy)throw new Exception("Tray animation/reinsert failed");
 tool.Cargo.Add(GrainType.Corn,2);vac.BeginEmpty();tool.Tick(.3f);tool.CancelAction();if(Mathf.Abs(tool.Kilograms-2)>.001f||Vector3.Distance(vac.dustTray.localPosition,tray)>.0001f)throw new Exception("Cancellation lost cargo or tray");
 bag.Add(4000);float retained=tool.Kilograms;if(tool.TryDrop(camera,actor.transform)||Mathf.Abs(tool.Kilograms-retained)>.001f)throw new Exception("Full container accepted unloading");
 camera.transform.position=actor.transform.position+Vector3.up*3.5f;wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=actor.transform.position+Vector3.up*2.6f;wall.transform.localScale=new Vector3(1,.2f,1);Physics.SyncTransforms();
 for(int i=0;i<30;i++){tool.TryUse(camera,actor.transform);tool.Tick(1f/60);}if(Mathf.Abs(tool.Kilograms-retained)>.001f)throw new Exception("Suction went through obstacle");
 results.Add(new{inletAnimation=animated,pileRemoved=start-pile.Kilograms,surfaceDrop=startHeight-pile.SurfaceHeight(actor.transform.position),blockedSuction=true,fullContainerSafe=true,tier,collected,received=bag.Kilograms,excursion,returned=true,capacity=tool.CapacityKg});
 }finally{if(wall!=null)UnityEngine.Object.DestroyImmediate(wall);if(actor!=null)UnityEngine.Object.DestroyImmediate(actor);if(pileGo!=null)UnityEngine.Object.DestroyImmediate(pileGo);if(receiver!=null)UnityEngine.Object.DestroyImmediate(receiver);}}
 return results;
 }
}

