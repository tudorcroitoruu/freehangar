using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using TMPro;using System.Linq;
public static class KeyChestColors {
 public static object Run(){
  if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
  foreach(var item in Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.FindKind==BuriedFind.Kind.HangarKey)){
   int n=item.TargetHangar;var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Discoveries/Hangar "+n+" key color.mat");var key=item.transform.Find("Industrial silver key");
   foreach(var r in key.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=mat;
   var roof=item.transform.Find("Barn roof symbol");if(roof!=null)roof.GetComponent<Renderer>().sharedMaterial=mat;
   // Different bow proportions and tooth patterns give each key a recognisable silhouette.
   float[] widths={.72f,1f,1.3f,.86f};
   foreach(Transform part in key){if(part.name=="Key bow"){var p=part.localPosition;p.x*=widths[n-2];part.localPosition=p;var sc=part.localScale;sc.x*=widths[n-2];part.localScale=sc;}}
   if(n>=4){var tooth=key.Cast<Transform>().First(t=>t.name=="Tooth");var copy=Object.Instantiate(tooth.gameObject,key);copy.name="Distinctive tooth";copy.transform.localPosition=new Vector3(-.03f,0,-.10f);if(n==5)copy.transform.localPosition=new Vector3(-.03f,0,-.16f);}
   item.name=char.ToUpperInvariant(HangarKeyDoor.KeyColor(n)[0])+HangarKeyDoor.KeyColor(n).Substring(1)+" key - HANGAR "+n;
   var door=Object.FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.HangarNumber==n);
   var so=new SerializedObject(door);var text=so.FindProperty("label").objectReferenceValue as TMP_Text;
   if(text!=null){text.text="HANGAR "+n+"\n"+HangarKeyDoor.KeyColor(n).ToUpperInvariant()+" KEY REQUIRED";EditorUtility.SetDirty(text);}
  }
  foreach(var chest in Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.FindKind==BuriedFind.Kind.CashChest)){
   int n=chest.name.Contains("Hangar 2")?2:chest.name.Contains("Hangar 4")?3:0;if(n==0)continue;
   var color=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Discoveries/Hangar "+n+" key color.mat");
   var so=new SerializedObject(chest);so.FindProperty("matchingHangarKeyId").stringValue=HangarKeyDoor.KeyFor(n);so.FindProperty("matchingKeyColor").stringValue=HangarKeyDoor.KeyColor(n);so.FindProperty("matchingKeyMaterial").objectReferenceValue=color;so.ApplyModifiedProperties();
   var original=chest.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="chest_base").sharedMaterial;
   string path="Assets/NewMap/Discoveries/"+HangarKeyDoor.KeyColor(n)+" mystery chest.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(original);AssetDatabase.CreateAsset(m,path);}
   m.SetColor("_BaseColor",n==2?new Color(1,.48f,.15f):new Color(.3f,.55f,1f));EditorUtility.SetDirty(m);
   foreach(var r in chest.GetComponentsInChildren<Renderer>(true)){if(r.name=="chest_base"||r.name=="chest_cap")r.sharedMaterial=m;else if(r.name=="Brass lock plate")r.sharedMaterial=color;}
  }
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Colored and shaped four keys; matched orange and blue chests";
 }}
