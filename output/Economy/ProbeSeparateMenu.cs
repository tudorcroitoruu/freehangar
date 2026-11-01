using System;using System.Linq;using System.IO;using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor;
public static class ProbeSeparateMenu {
 public static object Main(){
 if(!Application.isPlaying)throw new Exception("Enter Play Mode");
 string action=File.ReadAllText("output/Economy/menu-probe-action.txt").Trim();
 var menu=UnityEngine.Object.FindFirstObjectByType<MenuManager>();
 if(action=="settings")menu.ShowSettingsScreen();else if(action=="back")menu.ShowMenuScreen();else if(action=="quit")menu.ShowQuitGamePopup();else if(action=="continue")menu.ContinueGame();else if(action=="menu")Pause.Instance.GoToMenu();
 if(action=="capture-settings"||action=="capture-menu")ScreenCapture.CaptureScreenshot("output/Economy/"+action+".png");
 return new {action,scene=SceneManager.GetActiveScene().path,menu=menu!=null,loading=LoadingScreen.isLoadingOn,saved=SaveSystem.savesLoaded,
 screens=menu==null?new object[0]:new[]{"menuScreen","settingsScreen","quitGamePopup"}.Select(name=>{var go=(GameObject)new SerializedObject(menu).FindProperty(name).objectReferenceValue;return (object)new{go.name,active=go.activeSelf};}).ToArray(),
 cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled).Select(c=>new{c.name,target=c.targetTexture!=null}).ToArray()};
 }
}
