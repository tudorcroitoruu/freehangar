using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;
public static class ProbeSharedMenu {
 public static object Main(){
 if(!Application.isPlaying)throw new Exception("Enter Play Mode");
 var menu=UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
 var so=new SerializedObject(menu);
 string action=File.ReadAllText("output/Economy/menu-probe-action.txt").Trim();
 if(action=="continue"){
 var game=((GameObject)so.FindProperty("gameManager").objectReferenceValue).GetComponent<GameManager>();game.skipIntroCutscene=true;menu.ContinueGame();
 }else if(action=="menu")menu.ReturnToMenu();else if(action=="new")menu.NewGame();
 var cameraArray=so.FindProperty("menuCameras");
 return new {action,menu=menu.gameObject.activeSelf,player=((GameObject)so.FindProperty("player").objectReferenceValue).activeSelf,
 hud=((GameObject)so.FindProperty("gameplayCanvas").objectReferenceValue).activeSelf,game=((GameObject)so.FindProperty("gameManager").objectReferenceValue).activeSelf,
 Time.timeScale,loading=LoadingScreen.isLoadingOn,saved=SaveSystem.savesLoaded,cash=SaveSystem.Money,
 cameras=Enumerable.Range(0,cameraArray.arraySize).Select(i=>{var c=(Camera)cameraArray.GetArrayElementAtIndex(i).objectReferenceValue;return new{c.name,active=c.gameObject.activeSelf,c.enabled,target=c.targetTexture!=null};}).ToArray()};
 }
}
