using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GrainEconomyTests
{
    private GameObject root;
    private GrainPile pile;
    private GrainEconomySettings economy;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Economy test pile");
        root.SetActive(false);
        pile = root.AddComponent<GrainPile>();
        Set("fitToWalls", false);
        economy = ScriptableObject.CreateInstance<GrainEconomySettings>();
        Set("economySettings", economy);
        root.SetActive(true);
        pile.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(economy);
    }

    private void Set(string name, object value) => typeof(GrainPile)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pile, value);

    [TestCase(1f, false)]
    [TestCase(10f, false)]
    [TestCase(200f, false)]
    [TestCase(1f, true)]
    [TestCase(10f, true)]
    [TestCase(200f, true)]
    public void CollectionConsumesMultipliedStockButFillsOnlyRequestedCargo(float multiplier, bool spatial)
    {
        economy.grainConsumeMultiplier = multiplier;
        if (spatial) pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Wheat, GrainType.Rice) });
        double before = pile.Kilograms;
        var cargo = new GrainCargo();
        Vector3 contact = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float collected = pile.Scoop(contact, 100, 10, 10, cargo);
        Assert.That(collected, Is.EqualTo(10).Within(.002));
        Assert.That(cargo.Total, Is.EqualTo(collected).Within(.002));
        Assert.That(before - pile.Kilograms, Is.EqualTo(collected * multiplier).Within(.1));
        Assert.That(pile.CollectibleKilograms, Is.EqualTo(pile.Kilograms / multiplier).Within(.001));
        Assert.That(pile.HarvestedKg, Is.EqualTo(collected).Within(.002));
        if (spatial)
        {
            Assert.That(cargo[GrainType.Wheat], Is.EqualTo(collected * .5).Within(.002));
            Assert.That(cargo[GrainType.Rice], Is.EqualTo(collected * .5).Within(.002));
        }
    }

    [TestCase(10f)]
    [TestCase(200f)]
    public void DepositingBackIntoAuthoredStockPreservesRecoverableMass(float multiplier)
    {
        economy.grainConsumeMultiplier = multiplier;
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Wheat) });
        double initial = pile.Kilograms;
        var cargo = new GrainCargo();
        pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, 10, cargo);
        Assert.IsTrue(pile.Deposit(Vector3.zero, cargo, 10));
        Assert.That(cargo.Total, Is.EqualTo(0));
        Assert.That(pile.Kilograms, Is.EqualTo(initial).Within(.2));
        Assert.That(pile.GrainKilograms(GrainType.Wheat), Is.EqualTo(initial).Within(.2));
        Assert.That(pile.ClearedPercent, Is.EqualTo(0).Within(.01));
    }

    [Test]
    public void DroppedCargoIsRecoveredOneForOneEvenWithAnEconomySource()
    {
        economy.grainConsumeMultiplier = 200;
        var dropped = GrainPile.CreateDroppedPile(new Vector3(100, 0, 100), pile, 10);
        try
        {
            Assert.That(dropped.GrainConsumeMultiplier, Is.EqualTo(1));
            var cargo = new GrainCargo();
            cargo.Add(GrainType.Rice, 10);
            Assert.IsTrue(dropped.Deposit(dropped.transform.position, cargo));
            Assert.That(dropped.Kilograms, Is.EqualTo(10).Within(.002));
            Vector3 point = dropped.transform.position;
            point.y = dropped.SurfaceHeight(point);
            float collected = dropped.Scoop(point, 100, 10, 10, cargo);
            Assert.That(collected, Is.EqualTo(10).Within(.002));
            Assert.That(cargo[GrainType.Rice], Is.EqualTo(10).Within(.002));
        }
        finally { Object.DestroyImmediate(dropped.gameObject); }
    }

    [TestCase(0f, 1f)]
    [TestCase(-10f, 1f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(100000f, 10000f)]
    public void InvalidMultipliersCannotProduceNegativeOrInfiniteCargo(float value, float expected)
    {
        economy.grainConsumeMultiplier = value;
        Assert.That(pile.GrainConsumeMultiplier, Is.EqualTo(expected));
    }

    [Test]
    public void LargerStartingShovelFillsFreeBagInTwoLoadsWithoutLosingTheRemainder()
    {
        var settings=GrainEconomySettings.Default.shovelSettings;
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
        var bag=Object.Instantiate(prefab).GetComponent<GrainContainer>();
        try
        {
            Assert.That(settings.Capacity(0),Is.EqualTo(2.5f));
            Assert.That(bag.CapacityKg,Is.EqualTo(10f));
            economy.grainConsumeMultiplier=200;
            double before=pile.Kilograms;
            var cargo=new GrainCargo();
            for(int i=0;i<4;i++)
            {
                Vector3 point=new Vector3(0,pile.SurfaceHeight(Vector3.zero),0);
                float collected=pile.Scoop(point,100,10,settings.Capacity(0),cargo);
                Assert.That(collected,Is.EqualTo(2.5f).Within(.002));
                cargo.PourInto(bag,settings.Capacity(0));
                if(i<3)Assert.IsFalse(bag.IsFull);
            }
            Assert.That(bag.Kilograms,Is.EqualTo(10).Within(.002));
            Assert.IsTrue(bag.IsFull);
            Assert.That(cargo.Total,Is.EqualTo(0).Within(.002));
            Assert.That(before-pile.Kilograms,Is.EqualTo(2000).Within(.2));
        }
        finally { Object.DestroyImmediate(bag.gameObject); }
    }

    [Test]
    public void ManualShovelUpgradeLoadsAreTripledWhileVacuumCapacityIsPreserved()
    {
        var settings = GrainEconomySettings.Default.shovelSettings;
        float[] previousLoads = { 2.5f, 8.5f, 14.5f, 20f };
        for (int type = 0; type < previousLoads.Length; type++)
            for (int dot = 0; dot < ShovelUpgradeSettings.UpgradesPerShovel; dot++)
                Assert.That(settings.Capacity(type * 4 + dot),
                    Is.EqualTo(previousLoads[type] + dot).Within(.001));
        Assert.That(settings.Capacity(16), Is.EqualTo(150));
        Assert.That(settings.Capacity(24), Is.EqualTo(600));
        Assert.That(GrainEconomySettings.Default.ConsumeMultiplier, Is.EqualTo(3000));
        Assert.That(settings.Capacity(0) * GrainEconomySettings.Default.ConsumeMultiplier,
            Is.EqualTo(7.5f * 200f * 5f));
    }

    [Test]
    public void ExhaustingStockCannotMintMoreCargoThanItsCollectibleMass()
    {
        economy.grainConsumeMultiplier = 200;
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Barley) });
        double available = pile.CollectibleKilograms;
        var cargo = new GrainCargo();
        float collected = pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, 100000, cargo);
        Assert.That(collected, Is.EqualTo(available).Within(.002));
        Assert.That(cargo.Total, Is.EqualTo(available).Within(.002));
        Assert.That(pile.Kilograms, Is.LessThan(.01));
        Assert.That(pile.Scoop(Vector3.zero, 100, 10, 1000, cargo), Is.EqualTo(0));
    }
}
