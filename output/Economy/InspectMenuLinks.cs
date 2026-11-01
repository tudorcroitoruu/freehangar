using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public static class InspectMenuLinks {
 static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
 public static object Main()=>new {
 pause=Object.FindObjectsByType<Pause>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>new{path=Path(x.transform),active=x.gameObject.activeSelf}).ToArray(),
 canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.name.Contains("Menu")||c.GetComponentInChildren<LoadingScreen>(true)!=null).Select(x=>new{path=Path(x.transform),mode=x.renderMode.ToString(),order=x.sortingOrder,active=x.gameObject.activeSelf}).ToArray(),
 menuButtons=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.GetComponentInParent<MenuManager>(true)!=null && b.transform.parent.name=="Buttons").Select(b=>new{path=Path(b.transform),calls=Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>b.onClick.GetPersistentMethodName(i)).ToArray()}).ToArray()
 };
}
