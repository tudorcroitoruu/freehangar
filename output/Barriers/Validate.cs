var all=UnityEngine.Object.FindObjectsByType<BreakableGrainBarrier>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var ids=new System.Collections.Generic.HashSet<string>();
int added=0,flat=0,wood=0,metal=0;
var report=new System.Text.StringBuilder();
foreach(var b in all){
var data=new UnityEditor.SerializedObject(b);string id=data.FindProperty("barrierId").stringValue;
if(!ids.Add(id))throw new System.Exception("Duplicate ID "+id);
if(!id.Contains(".independent."))continue;
added++;if(data.FindProperty("material").enumValueIndex==0)wood++;else metal++;
if(UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(b.transform.up,UnityEngine.Vector3.up))<.1f)flat++;
var pile=data.FindProperty("coveringPile").objectReferenceValue as GrainPile;
var bounds=b.GetComponent<UnityEngine.Renderer>().bounds;
if(pile==null || !pile.IsInsideBoundary(bounds.center,1.7f))throw new System.Exception("Outside pile "+id);
if(pile.SurfaceHeight(bounds.center)<bounds.max.y+.2f)throw new System.Exception("Not buried "+id);
if(b.GetComponents<UnityEngine.Collider>().Length!=1 || !b.GetComponent<UnityEngine.BoxCollider>().enabled)throw new System.Exception("Invalid collider "+id);
foreach(var other in all)if(other!=b && other.GetComponent<UnityEngine.Renderer>().bounds.Intersects(bounds))throw new System.Exception("Barrier overlap "+id);
report.AppendLine("PASS "+id+" independent collider, unique save ID, buried, no barrier overlap");
}
if(added!=24 || flat!=8 || wood!=12 || metal!=12)throw new System.Exception("Unexpected layout counts");
// Exercise the existing hit path on temporary clones without reaching a save-producing final hit.
var originalData=SaveSystem.Data;
var temporary=new System.Collections.Generic.List<UnityEngine.GameObject>();
try{
SaveSystem.Data=new PlayerData();
foreach(int material in new[]{0,1}){
var source=System.Linq.Enumerable.First(all,b=>new UnityEditor.SerializedObject(b).FindProperty("material").enumValueIndex==material);
var a=UnityEngine.Object.Instantiate(source.gameObject);temporary.Add(a);
var b=UnityEngine.Object.Instantiate(source.gameObject);temporary.Add(b);
var first=a.GetComponent<BreakableGrainBarrier>();var second=b.GetComponent<BreakableGrainBarrier>();
var awake=typeof(BreakableGrainBarrier).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
awake.Invoke(first,null);awake.Invoke(second,null);first.RestoreSave(0,true);second.RestoreSave(0,true);
var right=material==0?PlayerCombatTools.Tool.Axe:PlayerCombatTools.Tool.Hammer;
var wrong=material==0?PlayerCombatTools.Tool.Hammer:PlayerCombatTools.Tool.Axe;
if(first.TryHit(wrong)||!first.TryHit(right)||first.SavedHits!=1||second.SavedHits!=0)throw new System.Exception("Independent damage failed");
report.AppendLine("PASS "+right+" damages only the aimed piece; wrong tool rejected.");
}
}finally{foreach(var go in temporary)UnityEngine.Object.DestroyImmediate(go);SaveSystem.Data=originalData;}
var scene=all[0].gameObject.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))throw new System.Exception("Scene save failed");
report.AppendLine("PASS saved "+scene.path+"; 24 additions: 12 wood, 12 metal; 16 upright, 8 horizontal.");
System.IO.File.WriteAllText("output/Barriers/validation.txt",report.ToString());
return report.ToString();
