using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
public static class VerifyWheelbarrows
{
 public static object Run()
 {
  if(UnityEditor.EditorApplication.isPlaying) throw new Exception("Edit mode required");
  var sources=UnityEngine.Object.FindObjectsByType<WheelbarrowCargo>(FindObjectsSortMode.None);
  var results=new List<object>(); var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  foreach(var source in sources)
  {
   GameObject ground=null,player=null,copy=null,wall=null;
   try
   {
    ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="Temporary test ground"; ground.transform.position=new Vector3(10000,9.5f,10000); ground.transform.localScale=new Vector3(200,1,200);
    copy=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(10000,10.02f,10000),Quaternion.identity); var barrow=copy.GetComponent<WheelbarrowCargo>();
    typeof(WheelbarrowCargo).GetMethod("Awake",flags).Invoke(barrow,null);
    var selected=(Transform[])typeof(WheelbarrowCargo).GetField("wheels",flags).GetValue(barrow);
    if(selected.Length==0 || selected.Any(t=>t.name.Contains("Body")))throw new Exception("Wheel selection includes body or is empty");
    player=new GameObject("Temporary test pusher"); var cc=player.AddComponent<CharacterController>(); cc.height=2;cc.center=Vector3.up;cc.radius=.3f;
    var interactor=player.AddComponent<PlayerInteractor>();
    var pc=player.AddComponent<PlayerController>();
    var cameraObject=new GameObject("Test view");cameraObject.transform.SetParent(player.transform,false);pc.characterCamera=cameraObject.AddComponent<Camera>();pc.characterController=cc;
    var instrument=new GameObject("Test shovel");instrument.transform.SetParent(cameraObject.transform,false);var shovel=instrument.AddComponent<ShovelTool>();
    typeof(PlayerController).GetField("heldInstrument",flags).SetValue(pc,instrument.transform);
    typeof(PlayerController).GetField("shovelTool",flags).SetValue(pc,shovel);
    typeof(PlayerController).GetField("interactor",flags).SetValue(pc,interactor);
    var mesh=copy.GetComponent<MeshCollider>(); float distance=-mesh.sharedMesh.bounds.min.z*Mathf.Abs(copy.transform.lossyScale.z)+.42f;
    player.transform.position=copy.transform.position-Vector3.forward*distance; Physics.SyncTransforms();
    if(!barrow.TryInteract(interactor))throw new Exception("Push rejected");
    if(instrument.activeSelf || shovel.enabled)throw new Exception("Shovel not hidden");
    var look=typeof(PlayerController).GetMethod("ApplyLook",flags);
    look.Invoke(pc,new object[]{new Vector2(1000,-1000)});
    if(Mathf.Abs((float)typeof(PlayerController).GetField("pushLookTarget",flags).GetValue(pc)-65)>.01f)throw new Exception("View yaw not clamped");
    if(Mathf.Abs((float)typeof(PlayerController).GetField("cameraRotationX",flags).GetValue(pc)-65)>.01f)throw new Exception("View pitch not clamped");
    if(Quaternion.Angle(player.transform.rotation,Quaternion.identity)>.01f)throw new Exception("Mouse steers player while pushing");
    if(!Physics.GetIgnoreCollision(cc,mesh))throw new Exception("Player collision not ignored");
    var bodies=copy.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Body")).ToArray();
    var original=bodies.Select(t=>t.localRotation).ToArray(); var wheelStart=selected[0].localRotation;
    var update=typeof(WheelbarrowCargo).GetMethod("LateUpdate",flags); float maxY=player.transform.position.y;
    for(int i=0;i<180;i++){barrow.Move(new Vector2(0,1),3,-2,1f/60);Physics.SyncTransforms();maxY=Mathf.Max(maxY,player.transform.position.y);}
    float moved=copy.transform.position.z-10000;
    bool stable=bodies.Select((b,i)=>Quaternion.Angle(b.localRotation,original[i])<.001f).All(x=>x);
    bool rolled=Quaternion.Angle(wheelStart,selected[0].localRotation)>.1f;
    if(!stable || !rolled || moved<1 || maxY>10.1f)throw new Exception("Movement failed: stable="+stable+" roll="+rolled+" moved="+moved+" y="+maxY);
    float beforeReverse=copy.transform.position.z;
    for(int i=0;i<60;i++){barrow.Move(new Vector2(0,-1),3,-2,1f/60);Physics.SyncTransforms();}
    if(copy.transform.position.z>=beforeReverse-.2f)throw new Exception("Reverse failed");
    float maxHandleError=0;
    for(int i=0;i<240;i++)
    {
     barrow.Move(new Vector2(i%60<30?1:-1,1),6,-2,1f/60);Physics.SyncTransforms();
     var expected=copy.transform.position-copy.transform.forward*distance;
     maxHandleError=Mathf.Max(maxHandleError,Vector3.ProjectOnPlane(player.transform.position-expected,Vector3.up).magnitude);
    }
    if(maxHandleError>.025f)throw new Exception("Handle separation changed during sprint/turn: "+maxHandleError);
    float turn=Quaternion.Angle(player.transform.rotation,copy.transform.rotation);
    if(turn>.01f)throw new Exception("Player and barrow rotations diverged");
    var before=copy.transform.position;
    for(int i=0;i<30;i++)barrow.Move(new Vector2(0,1),6,-2,1f/60);
    float sprintDistance=Vector3.Distance(before,copy.transform.position);
    if(sprintDistance<2.8f)throw new Exception("Sprint too slow: "+sprintDistance);
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=copy.transform.position+copy.transform.forward*2+Vector3.up;
    wall.transform.rotation=copy.transform.rotation;wall.transform.localScale=new Vector3(10,4,.2f);Physics.SyncTransforms();
    before=copy.transform.position;
    for(int i=0;i<120;i++)barrow.Move(new Vector2(0,1),6,-2,1f/60);
    if(Vector3.Distance(before,copy.transform.position)>2)throw new Exception("Passed through wall");
    barrow.TryInteract(interactor);
    if(!instrument.activeSelf || !shovel.enabled)throw new Exception("Shovel not restored");
    bool restored=!Physics.GetIgnoreCollision(cc,mesh) && interactor.PushedWheelbarrow==null;
    if(!restored)throw new Exception("Release did not restore collisions");
    results.Add(new{source.name,wheels=selected.Select(w=>w.name).ToArray(),stable,rolled,moved,maxY,maxHandleError,sprintDistance,turnError=turn,restored,shovelRestored=true,mouseSteeringDisabled=true,viewClamped=true});
   }
   finally {if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);if(player!=null)UnityEngine.Object.DestroyImmediate(player);if(ground!=null)UnityEngine.Object.DestroyImmediate(ground);if(wall!=null)UnityEngine.Object.DestroyImmediate(wall);}
  }
  return results;
 }
}

