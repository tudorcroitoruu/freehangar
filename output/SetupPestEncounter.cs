using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System.Linq;
public static class SetupPestEncounter{
 public static object Run(){
 if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
 var player=GameObject.Find("Player");var combat=player.GetComponent<PlayerCombatTools>();
 var broom=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Broom");
 var dust=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/HitDustExplosion.prefab");
 var co=new SerializedObject(combat);co.FindProperty("heldBroom").objectReferenceValue=broom;co.FindProperty("dustEffect").objectReferenceValue=dust;co.ApplyModifiedProperties();broom.gameObject.SetActive(false);
 var doors=Object.FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 var h2=doors.First(x=>x.HangarNumber==2);var h3=doors.First(x=>x.HangarNumber==3);var h4=doors.First(x=>x.HangarNumber==4);
 var hv=(HangarGrainVisibility)new SerializedObject(h2).FindProperty("grainVisibility").objectReferenceValue;
 var pile=hv.GrainRoot.GetComponent<GrainPile>();if(pile==null)pile=hv.GrainRoot.GetComponentInChildren<GrainPile>(true);
 if(GameObject.Find("Buried broom - Hangar 2")==null){
 var g=new GameObject("Buried broom - Hangar 2");g.transform.position=pile.transform.position+Vector3.up*.15f;
 var model=Object.Instantiate(broom.gameObject,g.transform);model.name="Broom discovery model";model.SetActive(true);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
 foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
 var rs=model.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new System.Exception("No broom renderer");
 var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
 float scale=1.6f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);model.transform.localScale*=scale;
 bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
 model.transform.position+=g.transform.position-bounds.center;var box=g.AddComponent<BoxCollider>();box.center=Vector3.zero;box.size=Vector3.one*1.5f;
 var find=g.AddComponent<BuriedFind>();var o=new SerializedObject(find);o.FindProperty("kind").enumValueIndex=(int)BuriedFind.Kind.Broom;o.FindProperty("findId").stringValue=PlayerCombatTools.BroomFindId;o.FindProperty("coveringPile").objectReferenceValue=pile;o.FindProperty("settleOnReveal").boolValue=true;o.ApplyModifiedProperties();
 }
 var director=Object.FindFirstObjectByType<GrainPestDirector>();if(director==null)director=new GameObject("Grain pests and broom tutorial").AddComponent<GrainPestDirector>();
 director.hangar3=h3;director.hangar4=h4;director.player=player.GetComponent<PlayerInteractor>();director.playerCamera=player.GetComponent<PlayerController>().characterCamera;
 director.ratPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Polygonmaker/Wild Life Series/Wild Life - Random Beasts/Characters/rat_01.prefab");
 director.crowPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Animals/Crows/TFP_Crow_01A.prefab");
 director.tutorialBagTemplate=Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.name=="Sack");
 EditorUtility.SetDirty(director);EditorSceneManager.MarkSceneDirty(player.scene);EditorSceneManager.SaveOpenScenes();
 return "Configured broom, two enemy prefabs, tutorial and dust. Rat="+(director.ratPrefab!=null)+" crow="+(director.crowPrefab!=null)+" pile="+(pile!=null);
 }}
