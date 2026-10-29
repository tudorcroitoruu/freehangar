using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using TMPro;using System.Linq;
public static class UpdateHangarSigns {
 public static object Run(){
 foreach(var d in Object.FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
  int n=d.HangarNumber;var o=new SerializedObject(d);var label=(TMP_Text)o.FindProperty("label").objectReferenceValue;
  label.fontSize=.8f;label.rectTransform.sizeDelta=new Vector2(1.2f,.48f);
  var panel=d.transform.Find("Numbered blue lock");panel.localScale=new Vector3(1.28f,.58f,.12f);
  var old=d.transform.Find("Matching key symbol");if(old!=null)Object.DestroyImmediate(old.gameObject);
  var source=GameObject.Find("Blue key - HANGAR "+n).transform.Find("Industrial silver key");
  var key=Object.Instantiate(source.gameObject,d.transform);key.name="Matching key model";
  key.transform.localPosition=new Vector3(.48f,-.14f,.095f);
  key.transform.localRotation=Quaternion.Euler(90,0,90);key.transform.localScale=Vector3.one*.5f;
  var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Discoveries/Hangar "+n+" key color.mat");
  foreach(var r in key.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=mat;
  foreach(var c in key.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
  var overhead=(TMP_Text)o.FindProperty("overheadLabel").objectReferenceValue;
  var sign=overhead.transform.parent;overhead.gameObject.SetActive(false);
  var title=sign.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name.StartsWith("HANGAR"));
  title.transform.localPosition=new Vector3(0,0,.09f);title.rectTransform.sizeDelta=new Vector2(6.2f,1.1f);
  var plaque=sign.Find("Number plaque");plaque.localScale=new Vector3(6.3f,1.2f,.13f);
  EditorUtility.SetDirty(label);EditorUtility.SetDirty(title);
 }
 EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Updated key models and sign sizes";
 }}
