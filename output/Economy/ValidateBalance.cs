using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static class ValidateGrainEconomyBalance
{
    public static object Main()
    {
        var economy=GrainEconomySettings.Default;
        if(economy==null || economy.hangars.Length!=5 || economy.grains.Length!=10 || economy.containers.Length!=5)throw new Exception("Incomplete central economy");
        if(new PlayerData().cash!=0 || economy.StartingCash!=0)throw new Exception("New games must start at zero");
        if(economy.shovelSettings==null || !economy.shovelSettings.IsConfigured || economy.staminaSettings==null || !economy.staminaSettings.IsConfigured || economy.vehicleSettings==null)throw new Exception("Missing upgrade sources");
        long actualShovel=economy.shovelSettings.shovels.Sum(s=>s.prices.Sum());
        long actualStamina=economy.staminaSettings.prices.Sum();
        long actualVehicles=economy.vehicleSettings.vehicles.Skip(1).Sum(v=>v.price);
        if(economy.TotalUpgradeCost!=actualShovel+actualStamina+actualVehicles)throw new Exception("Upgrade totals differ from referenced assets");
        var scene=SceneManager.GetActiveScene();
        var zones=UnityEngine.Object.FindObjectsByType<SellingZone>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(z=>z.gameObject.scene==scene&&z.isActiveAndEnabled).ToArray();
        if(zones.Length!=1)throw new Exception("Expected one active SellingZone");
        for(int i=0;i<GrainCargo.TypeCount;i++)
            if(!zones[0].TryGetSettings((GrainType)i,out var quote) || !economy.TryGetGrain((GrainType)i,out var source) || !ReferenceEquals(quote,source))throw new Exception("SellingZone is not using central crop settings");
        var shops=UnityEngine.Object.FindObjectsByType<ShopManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
        foreach(var shop in shops)
        {
            if(shop.EconomySettings!=economy)throw new Exception("Shop has a different economy");
            shop.ConfigureProductPricing();
        }
        foreach(var product in UnityEngine.Object.FindObjectsByType<ShopProduct>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene))
        using(var so=new SerializedObject(product))
        {
            string id=so.FindProperty("productID").stringValue;
            if(!economy.TryGetContainer(id,out var container)||product.Price!=container.price||product.ProductPrefab!=container.prefab)throw new Exception("Shop product differs from central container: "+id);
        }
        foreach(var director in UnityEngine.Object.FindObjectsByType<GrainPestDirector>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene))
        using(var so=new SerializedObject(director))if(so.FindProperty("economySettings").objectReferenceValue!=economy)throw new Exception("Pests use a different economy");
        foreach(var pile in UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene))
        {
            var stock=economy.hangars.Single(s=>s.pileName==pile.name);pile.Initialize();
            for(int i=0;i<GrainCargo.TypeCount;i++)if(Math.Abs(stock.physicalGrainKg[i]-pile.GrainKilograms((GrainType)i))>.1)throw new Exception("Stale stock snapshot: "+pile.name);
        }
        var free=UnityEngine.Object.FindObjectsByType<InventoryProduct>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(p=>p.gameObject.scene==scene&&p.isActiveAndEnabled&&p.ProductID=="freeBags"&&p.UnlimitedCount);
        if(free==null)throw new Exception("No free container recovery path with zero cash");
        double balance=0,stress=0;long previous=0;
        var stages=economy.hangars.OrderBy(h=>h.hangarId).Select(h=>{
            double gross=0,expected=0;for(int i=0;i<GrainCargo.TypeCount;i++){var crop=economy.ForecastCrop((GrainType)i,h);gross+=crop.grossRevenue;expected+=crop.expectedRevenue;}
            double containers=economy.ContainerAllowance(h);long cumulative=economy.PlannedCumulativeUpgradeCost(h),upgrades=cumulative-previous;previous=cumulative;
            balance+=expected-containers-upgrades;stress+=economy.StressRevenue(h)-containers-upgrades;
            if(upgrades<0||balance<0||stress<0)throw new Exception("Unaffordable purchase route at hangar "+h.hangarId);
            return new{h.hangarId,gross,expected,containers,upgrades,balance,stress};
        }).ToArray();
        if(previous!=economy.TotalUpgradeCost)throw new Exception("Purchase route does not cover every upgrade");
        File.WriteAllText("output/Economy/current-balance.md",GrainEconomyReport.BuildReport(economy));
        return new{startingCash=0,stages,economy.TotalGrossRevenue,economy.TotalExpectedRevenue,economy.ShovelUpgradeCost,economy.StaminaUpgradeCost,economy.VehicleUpgradeCost,economy.TotalUpgradeCost,economy.TotalContainerAllowance,economy.ExpectedBalance,stressBalance=stress,freeBagRecovery=free.name};
    }
}
