using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class InspectEconomyAssets
{
    public static object Main()
    {
        var scene=SceneManager.GetActiveScene();
        return new {
            supplies=Object.FindObjectsByType<GrainBagSupply>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).Select(x=>new{name=x.name,active=x.gameObject.activeInHierarchy,json=EditorJsonUtility.ToJson(x)}).ToArray(),
            shop=Object.FindObjectsByType<ShopManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>EditorJsonUtility.ToJson(x)).ToArray(),
            goals=Object.FindObjectsByType<GoalsManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>EditorJsonUtility.ToJson(x)).ToArray(),
            prefab=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Gameplay/Containers"}).Select(AssetDatabase.GUIDToAssetPath).Select(p=>new{path=p,capacity=AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<GrainContainer>().CapacityKg}).ToArray()
        };
    }
}
