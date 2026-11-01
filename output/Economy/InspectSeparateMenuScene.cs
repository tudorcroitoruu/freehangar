using System.Linq;using UnityEngine;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static class InspectSeparateMenuScene {
 public static object Main(){
 if(Application.isPlaying)return "Stop Play Mode before scene inspection";
 var active=SceneManager.GetActiveScene();var path="Assets/Scenes/Menu.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
 if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
 try {return new{active=active.path,menuRoots=scene.GetRootGameObjects().Select(x=>new{x.name,active=x.activeSelf,components=x.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name).ToArray()}).ToArray(),managers=Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).Select(x=>x.name).ToArray()};}
 finally{if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
 }
}
