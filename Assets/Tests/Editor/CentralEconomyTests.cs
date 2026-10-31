using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CentralEconomyTests
{
    private GrainEconomySettings economy;
    private ShovelUpgradeSettings shovel;
    private StaminaUpgradeSettings stamina;
    private VehicleUpgradeSettings vehicles;
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Central economy fixture");
        economy = ScriptableObject.CreateInstance<GrainEconomySettings>();
        economy.grainConsumeMultiplier = 10;
        economy.grains = new[] { new GrainElementSettings { grainType = GrainType.Corn, grainPrice = 2 }, new GrainElementSettings { grainType = GrainType.Wheat, grainPrice = 3 } };
        economy.hangars = new[]
        {
            new GrainEconomySettings.HangarStock { hangarId=1,physicalGrainKg=new double[]{1000,200},ratLossPercent=10,birdLossPercent=20,containerProductId="crate" },
            new GrainEconomySettings.HangarStock { hangarId=3,physicalGrainKg=new double[]{500,300},ratLossPercent=5,birdLossPercent=5,containerProductId="crate" }
        };
        var prefab = new GameObject("50 kg container");
        prefab.transform.SetParent(root.transform);
        prefab.AddComponent<GrainContainer>();
        economy.containers = new[] { new GrainEconomySettings.ContainerPrice { productId="crate",prefab=prefab,price=4 } };
        shovel = ScriptableObject.CreateInstance<ShovelUpgradeSettings>();
        shovel.shovels = new[] { new ShovelUpgradeSettings.ShovelType { prices=new[]{1,2,3,4} } };
        stamina = ScriptableObject.CreateInstance<StaminaUpgradeSettings>();
        stamina.prices = new[] { 5,7 };
        vehicles = ScriptableObject.CreateInstance<VehicleUpgradeSettings>();
        vehicles.vehicles = new[]
        {
            new VehicleUpgradeSettings.VehicleType{id="start",price=999},
            new VehicleUpgradeSettings.VehicleType{id="second",price=20},
            new VehicleUpgradeSettings.VehicleType{id="last",price=30}
        };
        economy.shovelSettings=shovel;economy.staminaSettings=stamina;economy.vehicleSettings=vehicles;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(economy);
        Object.DestroyImmediate(shovel);
        Object.DestroyImmediate(stamina);
        Object.DestroyImmediate(vehicles);
    }

    [Test]
    public void NewGameStartsWithNoCashAndCropForecastsSeparateBothPestLosses()
    {
        Assert.That(new PlayerData().cash,Is.Zero);
        Assert.That(economy.StartingCash,Is.Zero);
        var corn=economy.ForecastCrop(GrainType.Corn);
        Assert.That(corn.physicalKg,Is.EqualTo(1500));
        Assert.That(corn.collectibleKg,Is.EqualTo(150));
        Assert.That(corn.ratLossKg,Is.EqualTo(12.5));
        Assert.That(corn.birdLossKg,Is.EqualTo(22.5));
        Assert.That(corn.SaleableKg,Is.EqualTo(115));
        Assert.That(corn.grossRevenue,Is.EqualTo(300));
        Assert.That(corn.expectedRevenue,Is.EqualTo(230));
        Assert.That(economy.TotalGrossRevenue,Is.EqualTo(450));
        Assert.That(economy.TotalExpectedRevenue,Is.EqualTo(353));
    }

    [Test]
    public void AllUpgradeTotalsReadTheirReferencedAssetsAndDoNotChargeForStartingVehicle()
    {
        Assert.That(economy.ShovelUpgradeCost,Is.EqualTo(10));
        Assert.That(economy.StaminaUpgradeCost,Is.EqualTo(12));
        Assert.That(economy.VehicleUpgradeCost,Is.EqualTo(50));
        Assert.That(economy.TotalUpgradeCost,Is.EqualTo(72));
        shovel.shovels[0].prices[0]=11;
        stamina.prices[0]=15;
        vehicles.vehicles[2].price=40;
        Assert.That(economy.TotalUpgradeCost,Is.EqualTo(102));
        Assert.That(economy.TotalContainerAllowance,Is.EqualTo(20));
        Assert.That(economy.ExpectedBalance,Is.EqualTo(231));
    }

    [Test]
    public void PlanReadsArbitraryUpgradeCountsAndContainerCapacityFromPrefab()
    {
        var stock=economy.hangars[0];
        stock.targetShovelLevel=2;stock.targetStaminaLevel=1;stock.targetVehicleId="second";
        Assert.That(economy.PlannedCumulativeUpgradeCost(stock),Is.EqualTo(28));
        Assert.That(economy.ContainerAllowance(stock),Is.EqualTo(12));
        economy.containers[0].price=7;
        Assert.That(economy.ContainerAllowance(stock),Is.EqualTo(21));
    }

    [Test]
    public void EditingSellingPriceUpdatesForecastAndSellingZoneWithoutCopyingPrices()
    {
        var zone=root.AddComponent<SellingZone>();
        Set(zone,"economySettings",economy);
        Set(zone,"allGrainsSettings",new[]{new GrainElementSettings{grainType=GrainType.Corn,grainPrice=999}});
        Assert.IsTrue(zone.TryGetSettings(GrainType.Corn,out var quote));
        Assert.AreSame(economy.grains[0],quote);
        economy.grains[0].grainPrice=4;
        Assert.IsTrue(zone.TryGetSettings(GrainType.Corn,out quote));
        Assert.That(quote.grainPrice,Is.EqualTo(4));
        Assert.That(economy.TotalExpectedRevenue,Is.EqualTo(583));
        Assert.IsFalse(zone.TryGetSettings(GrainType.Rice,out _));
    }

    [Test]
    public void ShopManagerSuppliesLiveContainerPricesToItsProducts()
    {
        var shop=root.AddComponent<ShopManager>();
        var productObject=new GameObject("Priced container");productObject.transform.SetParent(root.transform);
        var product=productObject.AddComponent<ShopProduct>();
        Set(product,"productID","crate");Set(product,"productPrice",999);
        Set(shop,"economySettings",economy);Set(shop,"_shopProducts",new[]{product});
        shop.ConfigureProductPricing();
        Assert.That(product.Price,Is.EqualTo(4));
        Assert.AreSame(economy.containers[0].prefab,product.ProductPrefab);
        economy.containers[0].price=9;
        Assert.That(product.Price,Is.EqualTo(9));
        Assert.That(shop.ProductPrice("legacy",12),Is.EqualTo(12));
    }

    [Test]
    public void ForecastCapsCombinedPestLossAndHandlesInvalidMass()
    {
        economy.hangars[0].ratLossPercent=80;economy.hangars[0].birdLossPercent=80;
        var row=economy.ForecastCrop(GrainType.Corn,economy.hangars[0]);
        Assert.That(row.ratLossKg+row.birdLossKg,Is.EqualTo(100));
        Assert.That(row.expectedRevenue,Is.Zero);
        economy.hangars[0].physicalGrainKg[0]=double.NaN;
        Assert.That(economy.ForecastCrop(GrainType.Corn,economy.hangars[0]).collectibleKg,Is.Zero);
        Assert.That(economy.StressRevenue(economy.hangars[1]),Is.EqualTo(152));
    }

    [TestCase(false,false,2f)]
    [TestCase(true,false,6f)]
    [TestCase(true,true,2f)]
    public void ActualPestBitesUseEconomyAmountsAndInterval(bool bird,bool flock,float expectedLoss)
    {
        economy.ratGroupKgPerBite=8;economy.birdGroupKgPerBite=6;economy.pestBiteIntervalSeconds=7;
        var container=root.GetComponentInChildren<GrainContainer>();container.SetDemonstrationFill(20);
        var pestObject=new GameObject("Pest");pestObject.transform.SetParent(root.transform);
        var pest=pestObject.AddComponent<GrainPest>();
        pest.ConfigureEconomy(economy);pest.Initialize(container,bird,null,null);
        if(!bird)pest.ConfigureBagSlot(0,4,false);
        else if(flock)pest.ConfigureCrowArrival(Vector3.zero,Vector3.zero,0);
        Set(pest,"nextBite",0f);
        typeof(GrainPest).GetMethod("EatFromTarget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pest,null);
        Assert.That(container.Kilograms,Is.EqualTo(20-expectedLoss).Within(.001));
        Assert.That((float)typeof(GrainPest).GetField("nextBite",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pest),Is.EqualTo(Time.time+7).Within(.01));
    }

    private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);

    [Test]
    public void ExistingPestsReadEditedBiteSettingsOnTheirNextBite()
    {
        var container=root.GetComponentInChildren<GrainContainer>();container.SetDemonstrationFill(20);
        var pestObject=new GameObject("Live configured rat");pestObject.transform.SetParent(root.transform);
        var pest=pestObject.AddComponent<GrainPest>();pest.ConfigureEconomy(economy);pest.Initialize(container,false,null,null);
        pest.ConfigureBagSlot(0,4,false);
        economy.ratGroupKgPerBite=8;economy.pestBiteIntervalSeconds=2;
        Set(pest,"nextBite",0f);
        typeof(GrainPest).GetMethod("EatFromTarget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pest,null);
        Assert.That(container.Kilograms,Is.EqualTo(18).Within(.001));
        Assert.That((float)typeof(GrainPest).GetField("nextBite",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pest),Is.EqualTo(Time.time+2).Within(.01));
    }
}
