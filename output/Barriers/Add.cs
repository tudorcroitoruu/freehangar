if(UnityEngine.Application.isPlaying) throw new System.Exception("Leave Play mode before editing barriers.");
var existing=UnityEngine.Object.FindObjectsByType<BreakableGrainBarrier>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var wood=System.Linq.Enumerable.First(existing,b=>new UnityEditor.SerializedObject(b).FindProperty("material").enumValueIndex==0);
var metal=System.Linq.Enumerable.First(existing,b=>new UnityEditor.SerializedObject(b).FindProperty("material").enumValueIndex==1);
var occupied=new System.Collections.Generic.List<UnityEngine.Bounds>();
foreach(var b in existing) foreach(var r in b.GetComponentsInChildren<UnityEngine.Renderer>(true)) {var bounds=r.bounds;bounds.Expand(1f);occupied.Add(bounds);}
foreach(var f in UnityEngine.Object.FindObjectsByType<BuriedFind>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
foreach(var r in f.GetComponentsInChildren<UnityEngine.Renderer>(true)){var bounds=r.bounds;bounds.Expand(1f);occupied.Add(bounds);}
var report=new System.Text.StringBuilder();
UnityEditor.Undo.IncrementCurrentGroup();
int undo=UnityEditor.Undo.GetCurrentGroup();UnityEditor.Undo.SetCurrentGroupName("Add independent wood and metal barriers");
int count=0;
for(int hangar=2;hangar<=5;hangar++){
var pile=System.Linq.Enumerable.First(UnityEngine.Object.FindObjectsByType<GrainPile>(UnityEngine.FindObjectsSortMode.None),p=>p.name.StartsWith("Hangar "+hangar+" "));
pile.Initialize();
for(int index=0;index<6;index++){
string id="farm.barrier.h"+hangar+".independent."+index;
if(System.Linq.Enumerable.Any(existing,b=>new UnityEditor.SerializedObject(b).FindProperty("barrierId").stringValue==id))continue;
bool isMetal=hangar==3 || (hangar>=4 && index%2==1);
bool flat=index>=4;
var source=isMetal?metal:wood;
var go=UnityEngine.Object.Instantiate(source.gameObject,source.transform.parent);
UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Independent barrier");
go.name="Hangar "+hangar+" "+(isMetal?"Metal":"Wood")+" independent "+(index+1)+(flat?" horizontal":" upright");
go.transform.rotation=flat?UnityEngine.Quaternion.Euler(90,index%2==0?0:90,0):UnityEngine.Quaternion.Euler(0,index<2?90:0,0);
go.transform.position=UnityEngine.Vector3.zero;
var renderer=go.GetComponent<UnityEngine.Renderer>();var initial=renderer.bounds;
var centerOffset=initial.center;
var half=initial.extents;
UnityEngine.Vector3 best=UnityEngine.Vector3.zero;float bestScore=float.NegativeInfinity;bool found=false;
for(float z=-16;z<=16;z+=2.5f)for(float x=-22;x<=22;x+=2.5f){
var floor=pile.transform.position+new UnityEngine.Vector3(x,0,z);
if(!pile.IsInsideBoundary(floor,2f))continue;
var center=new UnityEngine.Vector3(floor.x,.12f+half.y,floor.z);
var bounds=new UnityEngine.Bounds(center,half*2);
if(pile.SurfaceHeight(center)<bounds.max.y+.25f)continue;
var padded=bounds;padded.Expand(1.4f);
if(System.Linq.Enumerable.Any(occupied,b=>b.Intersects(padded)))continue;
float separation=1000;
foreach(var b in occupied) separation=UnityEngine.Mathf.Min(separation,(b.center-center).sqrMagnitude);
float score=UnityEngine.Mathf.Min(separation,100)-.02f*(x*x+z*z);
if(score<=bestScore)continue;
bestScore=score;best=center-centerOffset;found=true;
}
if(!found){UnityEngine.Object.DestroyImmediate(go);throw new System.Exception("No safe placement H"+hangar+" piece "+index);}
go.transform.position=best;
var data=new UnityEditor.SerializedObject(go.GetComponent<BreakableGrainBarrier>());
data.FindProperty("barrierId").stringValue=id;
data.FindProperty("coveringPile").objectReferenceValue=pile;
data.ApplyModifiedPropertiesWithoutUndo();
go.SetActive(true);go.GetComponent<UnityEngine.BoxCollider>().enabled=true;
foreach(var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))r.enabled=true;
var occupiedBounds=renderer.bounds;occupiedBounds.Expand(2f);occupied.Add(occupiedBounds);
report.AppendLine(id+" "+(flat?"horizontal":"upright")+" "+(isMetal?"metal":"wood")+" position="+go.transform.position+" rotation="+go.transform.eulerAngles);
count++;
}
}
UnityEditor.Undo.CollapseUndoOperations(undo);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wood.gameObject.scene);
System.IO.File.WriteAllText("output/Barriers/added.txt",report.ToString());
return "Added "+count+" independent barriers.\n"+report.ToString();
