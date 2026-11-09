using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class InspectSellArrow {
 public static object Run() {
 return new { playing=Application.isPlaying, scene=SceneManager.GetActiveScene().path, dirty=SceneManager.GetActiveScene().isDirty,
 buttons=Object.FindObjectsByType<SellingWeighButton>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(b=>new {name=b.name,children=b.GetComponentsInChildren<Transform>(true).Select(t=>new {name=t.name,id=t.GetInstanceID(),components=t.GetComponents<Component>().Select(c=>c.GetType().Name).ToArray()}).ToArray()}).ToArray()};
 }
}
