using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ApplyGrainEconomyBalance
{
    static void Edit(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        Undo.RecordObject(target,"Balance grain economy");
        using(var so=new SerializedObject(target)) { edit(so); so.ApplyModifiedProperties(); }
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }
    public static object Main()
    {
        if(Application.isPlaying) throw new Exception("Edit Mode required");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity") throw new Exception("Gameplay must be open");
        Directory.CreateDirectory("output/Economy/Backups");
        if(!File.Exists("output/Economy/Backups/Gameplay.before-economy.unity"))
            EditorSceneManager.SaveScene(scene,"output/Economy/Backups/Gameplay.before-economy.unity",true);
        foreach(string name in new[]{"Shovel","Stamina","Vehicle"})
        {
            string path="Assets/Resources/Upgrades/"+name+"UpgradeSettings.asset";
            string backup="output/Economy/Backups/"+name+"UpgradeSettings.asset";
            if(!File.Exists(backup))File.Copy(path,backup);
        }
        if(!AssetDatabase.IsValidFolder("Assets/Resources/Economy"))AssetDatabase.CreateFolder("Assets/Resources","Economy");
        const string settingsPath="Assets/Resources/Economy/GrainEconomySettings.asset";
        var economy=AssetDatabase.LoadAssetAtPath<GrainEconomySettings>(settingsPath);
        if(economy==null){economy=ScriptableObject.CreateInstance<GrainEconomySettings>();AssetDatabase.CreateAsset(economy,settingsPath);}
        Edit(economy,so=>so.FindProperty("grainConsumeMultiplier").floatValue=200);
        var piles=UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).ToArray();
        if(piles.Length!=5)throw new Exception("Expected five piles");
        foreach(var pile in piles)Edit(pile,so=>so.FindProperty("economySettings").objectReferenceValue=economy);
        int[][] shovelPrices={
            new[]{50,75,100,150}, new[]{150,200,250,350}, new[]{450,650,850,1100},
            new[]{1400,1800,2300,3000}, new[]{3500,4000,5000,6500}, new[]{12000,15000,19000,24000}
        };
        var shovels=Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
        if(shovels.shovels.Length!=shovelPrices.Length)throw new Exception("Unexpected shovel progression");
        Edit(shovels,so=>{
            var array=so.FindProperty("shovels");
            for(int i=0;i<shovelPrices.Length;i++)
                for(int j=0;j<4;j++)array.GetArrayElementAtIndex(i).FindPropertyRelative("prices").GetArrayElementAtIndex(j).intValue=shovelPrices[i][j];
        });
        var stamina=Resources.Load<StaminaUpgradeSettings>("Upgrades/StaminaUpgradeSettings");
        Edit(stamina,so=>{
            int[] prices={75,400,1800,6000};var array=so.FindProperty("prices");array.arraySize=prices.Length;
            for(int i=0;i<prices.Length;i++)array.GetArrayElementAtIndex(i).intValue=prices[i];
        });
        var vehicles=Resources.Load<VehicleUpgradeSettings>("Upgrades/VehicleUpgradeSettings");
        int[] vehiclePrices={0,200,600,1500,4500,9000,18000};
        if(vehicles.vehicles.Length!=vehiclePrices.Length)throw new Exception("Unexpected vehicle progression");
        Edit(vehicles,so=>{
            var array=so.FindProperty("vehicles");for(int i=0;i<vehiclePrices.Length;i++)array.GetArrayElementAtIndex(i).FindPropertyRelative("price").intValue=vehiclePrices[i];
        });
        string[] ids={"smallBag","largeBag","crate","barrel","bigBag"};int[] containerPrices={4,7,10,14,45};
        var products=UnityEngine.Object.FindObjectsByType<ShopProduct>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).ToArray();
        var ordered=new ShopProduct[5];
        foreach(var product in products)Edit(product,so=>{
            int index=Array.IndexOf(ids,so.FindProperty("productID").stringValue);
            if(index<0)throw new Exception("Unknown product "+product.name);
            ordered[index]=product;so.FindProperty("productPrice").intValue=containerPrices[index];
        });
        if(ordered.Any(p=>p==null))throw new Exception("Missing shop product");
        foreach(var shop in UnityEngine.Object.FindObjectsByType<ShopManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene))Edit(shop,so=>{
            var array=so.FindProperty("_shopProducts");array.arraySize=5;for(int i=0;i<5;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=ordered[i];
        });
        float[] cropPrices={2,2.4f,2.2f,2.6f,2,3.4f,3.6f,4,4.2f,3.8f};
        var zones=UnityEngine.Object.FindObjectsByType<SellingZone>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(z=>z.gameObject.scene==scene).ToArray();
        if(zones.Length!=1)throw new Exception("Expected a single SellingZone");
        Edit(zones[0],so=>{
            var array=so.FindProperty("allGrainsSettings");
            if(array.arraySize!=GrainCargo.TypeCount)throw new Exception("Missing crop selling settings");
            for(int i=0;i<array.arraySize;i++){var entry=array.GetArrayElementAtIndex(i);entry.FindPropertyRelative("grainPrice").floatValue=cropPrices[entry.FindPropertyRelative("grainType").enumValueIndex];}
        });
        // Retain the scale's scenery but remove its separate automatic cash payout.
        foreach(var old in UnityEngine.Object.FindObjectsByType<GrainWeighStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(z=>z.gameObject.scene==scene))Edit(old,so=>so.FindProperty("m_Enabled").boolValue=false);
        int disabledCashFinds=0;
        foreach(var find in UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene))
        {
            using(var so=new SerializedObject(find))
            {
                var kind=(BuriedFind.Kind)so.FindProperty("kind").enumValueIndex;
                if(kind!=BuriedFind.Kind.CashCache&&kind!=BuriedFind.Kind.CashChest)continue;
            }
            Undo.RecordObject(find.gameObject,"Disable cash discovery");find.gameObject.SetActive(false);disabledCashFinds++;
        }
        foreach(var goals in UnityEngine.Object.FindObjectsByType<GoalsManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(g=>g.gameObject.scene==scene))Edit(goals,so=>{
            var array=so.FindProperty("allGoalSettings");
            for(int i=0;i<array.arraySize;i++)
            {
                var goal=array.GetArrayElementAtIndex(i);goal.FindPropertyRelative("rewardAmount").intValue=0;
                string id=goal.FindPropertyRelative("goalId").stringValue;
                if(id=="tutorial_fill_corn_sack"){goal.FindPropertyRelative("goalAmount").floatValue=10;goal.FindPropertyRelative("goalDescription").stringValue="Fill the free sack with 10 kg of grain";}
                if(id=="tutorial_load_200kg_corn"){goal.FindPropertyRelative("goalAmount").floatValue=20;goal.FindPropertyRelative("goalDescription").stringValue="Load 20 kg of grain onto the vehicle";}
                if(id=="h1_deliver_500kg"){goal.FindPropertyRelative("goalAmount").floatValue=200;goal.FindPropertyRelative("goalDescription").stringValue="Deliver 200 kg of grain";}
                if(id=="h1_move_1000kg"){goal.FindPropertyRelative("goalAmount").floatValue=400;goal.FindPropertyRelative("goalDescription").stringValue="Move 400 kg of grain";}
            }
            so.FindProperty("currentGoal").FindPropertyRelative("rewardAmount").intValue=0;
        });
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new {multiplier=economy.ConsumeMultiplier,startingCash=new PlayerData().cash,shovelCost=shovelPrices.Sum(a=>a.Sum()),staminaCost=stamina.prices.Sum(),vehicleCost=vehiclePrices.Sum(),disabledCashFinds,sceneSaved=!scene.isDirty};
    }
}
