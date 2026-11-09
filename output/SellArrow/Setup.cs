using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupSellArrow {
 public static object Run() {
 if (Application.isPlaying) throw new Exception("Requires edit mode");
 var button=UnityEngine.Object.FindObjectsByType<SellingWeighButton>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.transform.Find("3D RightArrow")!=null);
 var arrow=button.transform.Find("3D RightArrow");
 var so=new SerializedObject(button);
 so.FindProperty("readyArrow").objectReferenceValue=arrow;
 so.FindProperty("player").objectReferenceValue=UnityEngine.Object.FindFirstObjectByType<PlayerInteractor>();
 so.ApplyModifiedProperties();
 EditorSceneManager.MarkSceneDirty(button.gameObject.scene);
 EditorSceneManager.SaveScene(button.gameObject.scene);
 return new { arrow=arrow.name, position=arrow.localPosition.ToString(), wired= new SerializedObject(button).FindProperty("readyArrow").objectReferenceValue==arrow, saved=button.gameObject.scene.path };
 }
}
