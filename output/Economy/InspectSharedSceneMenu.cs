using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public static class InspectSharedSceneMenu {
 public static object Main() {
 var scene=SceneManager.GetActiveScene();
 return new {scene=scene.path,playing=Application.isPlaying,
 roots=scene.GetRootGameObjects().Select(x=>new{x.name,active=x.activeSelf,components=x.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name).ToArray()}).ToArray(),
 buttons=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.GetComponentInParent<MenuManager>(true)!=null || b.name.ToLower().Contains("menu") || b.name.ToLower().Contains("leave")).Select(b=>new{b.name,parent=b.transform.parent?.name,calls=Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>new{target=b.onClick.GetPersistentTarget(i) != null ? b.onClick.GetPersistentTarget(i).name : "MISSING",method=b.onClick.GetPersistentMethodName(i)}).ToArray()}).ToArray(),
 loading=Object.FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>new{x.name,active=x.gameObject.activeSelf,parent=x.transform.parent?.name}).ToArray()};
 }
}

