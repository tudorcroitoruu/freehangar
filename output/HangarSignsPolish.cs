using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using TMPro;using System.Linq;
public static class HangarSignsPolish {
public static object Run(){
 if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
 var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Baloo_2/static/Baloo2-Bold SDF.asset");
 var oak=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/TallHangar/Rich oak framing.mat");
 var baseMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Discoveries/Hangar key blue.mat");
 Color[] colors={new Color(.65f,.28f,.12f),new Color(.08f,.35f,.7f),new Color(.16f,.48f,.29f),new Color(.78f,.52f,.12f)};
 foreach(var lockDoor in Object.FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
  int n=lockDoor.HangarNumber;string path="Assets/NewMap/Discoveries/Hangar "+n+" key color.mat";
  var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(baseMat);AssetDatabase.CreateAsset(mat,path);}
  mat.SetColor("_BaseColor",colors[n-2]);EditorUtility.SetDirty(mat);
  var key=GameObject.Find("Blue key - HANGAR "+n);
  if(key!=null){var tag=key.transform.Find("Blue hangar tag");if(tag!=null)foreach(var r in tag.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=mat;
   var label=key.GetComponentInChildren<TMP_Text>(true);if(label!=null){label.font=font;EditorUtility.SetDirty(label);}
   // Each tag has a distinctive silhouette as well as its color.
   if(tag!=null){float[] widths={.85f,1f,1.14f,.92f};tag.localScale=Vector3.Scale(tag.localScale,new Vector3(widths[n-2],1,1));}
  }
  var panel=lockDoor.transform.Find("Numbered blue lock");panel.GetComponent<Renderer>().sharedMaterial=oak;
  var so=new SerializedObject(lockDoor);var label2=so.FindProperty("label").objectReferenceValue as TMP_Text;
  if(label2!=null){label2.font=font;label2.color=new Color(1,.9f,.69f);label2.text="HANGAR "+n+"\n"+HangarKeyDoor.KeyColor(n).ToUpperInvariant()+" KEY REQUIRED";EditorUtility.SetDirty(label2);}
  var sign=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Hangar number and locked sign" && t.parent.name.StartsWith("Hangar "+n+" -"));
  if(sign!=null){var plaque=sign.Find("Number plaque");if(plaque!=null)plaque.GetComponent<Renderer>().sharedMaterial=oak;
   foreach(var text in sign.GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.color=new Color(1,.88f,.65f);EditorUtility.SetDirty(text);}
  }
  // A small color-coded key icon alongside the matching wording.
  var icon=new GameObject("Matching key symbol").transform;icon.SetParent(lockDoor.transform,false);icon.localPosition=new Vector3(-.38f,-.12f,.085f);
  var shaft=GameObject.CreatePrimitive(PrimitiveType.Cube);shaft.name="Key shaft";shaft.transform.SetParent(icon,false);shaft.transform.localPosition=new Vector3(.055f,0,0);shaft.transform.localScale=new Vector3(.11f,.025f,.018f);shaft.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(shaft.GetComponent<Collider>());
  var bow=GameObject.CreatePrimitive(PrimitiveType.Sphere);bow.name="Key bow";bow.transform.SetParent(icon,false);bow.transform.localPosition=new Vector3(-.015f,0,0);bow.transform.localScale=new Vector3(.055f,.055f,.018f);bow.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(bow.GetComponent<Collider>());
 }
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Hangar keys and matching signs updated";
}}
