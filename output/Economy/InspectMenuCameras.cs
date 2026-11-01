using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class InspectMenuCameras
{
    public static object Main() => new {
        scene=SceneManager.GetActiveScene().path,
        playing=Application.isPlaying,
        managers=Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>new{x.name,active=x.gameObject.activeSelf,parent=x.transform.parent?.name}).ToArray(),
        cameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>new{x.name,active=x.gameObject.activeInHierarchy,self=x.gameObject.activeSelf,parent=x.transform.parent?.name,x.enabled}).ToArray()
    };
}
