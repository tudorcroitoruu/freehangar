using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class InspectEconomy
{
    static string Path(Component c) => c.transform.parent == null ? c.name : Path(c.transform.parent) + "/" + c.name;
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        var piles = UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.gameObject.scene == scene).ToArray();
        return new {
            scene = scene.path, dirty = scene.isDirty,
            piles = piles.Select(p => { p.Initialize(); return new {path=Path(p), kg=p.Kilograms, crops=Enumerable.Range(0, GrainCargo.TypeCount).Select(i=>new {type=((GrainType)i).ToString(),kg=p.GrainKilograms((GrainType)i)}).ToArray()}; }).ToArray(),
            products = UnityEngine.Object.FindObjectsByType<ShopProduct>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).Select(p=>new {path=Path(p),json=EditorJsonUtility.ToJson(p)}).ToArray(),
            containers = UnityEngine.Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).Select(p=>new {path=Path(p),active=p.gameObject.activeInHierarchy,kg=p.Kilograms,capacity=p.CapacityKg}).ToArray(),
            zones = UnityEngine.Object.FindObjectsByType<SellingZone>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).Select(p=>new {path=Path(p),active=p.gameObject.activeInHierarchy,json=EditorJsonUtility.ToJson(p)}).ToArray(),
            oldZones = UnityEngine.Object.FindObjectsByType<GrainWeighStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).Select(p=>new {path=Path(p),active=p.gameObject.activeInHierarchy}).ToArray(),
            rewards = UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).Select(p=>new {path=Path(p),json=EditorJsonUtility.ToJson(p)}).ToArray()
        };
    }
}
