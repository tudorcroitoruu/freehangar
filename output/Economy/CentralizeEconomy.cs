using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class CentralizeGrainEconomy
{
    static void Assign(UnityEngine.Object target, GrainEconomySettings economy)
    {
        Undo.RecordObject(target,"Assign central economy");
        using(var so=new SerializedObject(target)){so.FindProperty("economySettings").objectReferenceValue=economy;so.ApplyModifiedProperties();}
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Open Gameplay");
        string backup="output/Economy/Backups/Gameplay.before-central-economy.unity";
        if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        var economy=AssetDatabase.LoadAssetAtPath<GrainEconomySettings>("Assets/Resources/Economy/GrainEconomySettings.asset");
        if(economy==null)throw new Exception("Missing economy asset");
        var zone=UnityEngine.Object.FindObjectsByType<SellingZone>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(z=>z.gameObject.scene==scene);
        var products=UnityEngine.Object.FindObjectsByType<ShopProduct>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).ToArray();
        using(var data=new SerializedObject(economy))
        {
            data.FindProperty("shovelSettings").objectReferenceValue=Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
            data.FindProperty("staminaSettings").objectReferenceValue=Resources.Load<StaminaUpgradeSettings>(StaminaUpgradeSettings.ResourcePath);
            data.FindProperty("vehicleSettings").objectReferenceValue=Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
            var grains=data.FindProperty("grains");
            // Preserve any prices already edited in the central asset on a repeated migration.
            if(grains.arraySize==0)
            {
                grains.arraySize=GrainCargo.TypeCount;
                for(int i=0;i<GrainCargo.TypeCount;i++)
                {
                    if(!zone.TryGetSettings((GrainType)i,out var source))throw new Exception("Missing crop "+i);
                    var entry=grains.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("grainType").enumValueIndex=i;
                    entry.FindPropertyRelative("grainPrice").floatValue=source.grainPrice;
                    entry.FindPropertyRelative("grainSprite").objectReferenceValue=source.grainSprite;
                }
            }
            var containers=data.FindProperty("containers");
            if(containers.arraySize==0)
            {
                containers.arraySize=products.Length;
                for(int i=0;i<products.Length;i++)using(var source=new SerializedObject(products[i]))
                {
                    var entry=containers.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("productId").stringValue=source.FindProperty("productID").stringValue;
                    entry.FindPropertyRelative("prefab").objectReferenceValue=source.FindProperty("objectPrefab").objectReferenceValue;
                    entry.FindPropertyRelative("price").intValue=source.FindProperty("productPrice").intValue;
                }
            }
            data.ApplyModifiedProperties();
        }
        GrainEconomyReport.CaptureStock(economy);
        Assign(zone,economy);
        var shops=UnityEngine.Object.FindObjectsByType<ShopManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
        foreach(var shop in shops){Assign(shop,economy);shop.ConfigureProductPricing();}
        foreach(var director in UnityEngine.Object.FindObjectsByType<GrainPestDirector>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene))Assign(director,economy);
        EditorUtility.SetDirty(economy);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("output/Economy");
        File.WriteAllText("output/Economy/current-balance.md",GrainEconomyReport.BuildReport(economy));
        Selection.activeObject=economy;
        return new {economy=AssetDatabase.GetAssetPath(economy),startingCash=new PlayerData().cash,economy.TotalGrossRevenue,economy.TotalExpectedRevenue,economy.ShovelUpgradeCost,economy.StaminaUpgradeCost,economy.VehicleUpgradeCost,economy.TotalUpgradeCost,economy.TotalContainerAllowance,economy.ExpectedBalance,stocks=economy.hangars.Length,grainPrices=economy.grains.Length,containerPrices=economy.containers.Length,shops=shops.Length};
    }
}
