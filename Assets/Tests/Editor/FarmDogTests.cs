using NUnit.Framework;
using UnityEngine;

public sealed class FarmDogTests
{
    [Test]
    public void DeliveryRejectsAChickenLegHiddenBehindAWall()
    {
        var dogObject = new GameObject("Delivery dog"); dogObject.SetActive(false);
        var player = new GameObject("Delivery player");
        var wall = new GameObject("Delivery wall");
        try
        {
            var dog = dogObject.AddComponent<FarmDog>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FarmDog).GetField("nearbyPlayer", flags).SetValue(dog, player.transform);
            var visible = typeof(FarmDog).GetMethod("DeliveryVisible", flags);
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(3, 3, .2f);
            wall.transform.position = new Vector3(0, 1.5f, 2);
            Physics.SyncTransforms();
            Assert.IsFalse((bool)visible.Invoke(dog, new object[] { new Vector3(0, 0, 4) }));
            collider.enabled = false;
            Physics.SyncTransforms();
            Assert.IsTrue((bool)visible.Invoke(dog, new object[] { new Vector3(0, 0, 4) }));
        }
        finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(player); Object.DestroyImmediate(dogObject); }
    }

    private PlayerData previous;
    [SetUp] public void Setup() { previous = SaveSystem.Data; SaveSystem.Data = new PlayerData(); }
    [TearDown] public void Cleanup() { SaveSystem.Data = previous; }

    [TestCase(1, 0f, false)]
    [TestCase(2, 480f, false)]
    [TestCase(3, 240f, false)]
    [TestCase(4, 480f, true)]
    [TestCase(5, 240f, true)]
    public void LevelsMatchHuntSchedule(int level, float seconds, bool bringsLeg)
    {
        Assert.AreEqual(seconds, FarmDog.HuntInterval(level));
        Assert.AreEqual(bringsLeg, FarmDog.BringsLeg(level));
    }

    [Test] public void UpgradeChargesOnceAndPersistsLevelAndDelivery()
    {
        SaveSystem.Data.cash = 500;
        Assert.IsTrue(SaveSystem.TryPurchaseDogUpgrade(500, 1));
        Assert.AreEqual(0, SaveSystem.Data.cash);
        Assert.AreEqual(2, SaveSystem.Data.dogLevel);
        Assert.IsFalse(SaveSystem.TryPurchaseDogUpgrade(500, 1));
        SaveSystem.Data.dogRestSeconds = 72.5f; SaveSystem.Data.dogHasDelivery = true;
        var restored = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(SaveSystem.Data));
        Assert.AreEqual(2, restored.dogLevel);
        Assert.AreEqual(72.5f, restored.dogRestSeconds);
        Assert.IsTrue(restored.dogHasDelivery);
    }

    [Test] public void UpgradeRejectsUnaffordableMaxAndStaleLevels()
    {
        Assert.IsFalse(SaveSystem.TryPurchaseDogUpgrade(500, 1));
        SaveSystem.Data.cash = 10000;
        Assert.IsFalse(SaveSystem.TryPurchaseDogUpgrade(500, 2));
        SaveSystem.Data.dogLevel = 5;
        Assert.IsFalse(SaveSystem.TryPurchaseDogUpgrade(500, 5));
        Assert.AreEqual(10000, SaveSystem.Data.cash);
    }

    [Test] public void UpgradeUsesSingleVoucherAndShortensRest()
    {
        SaveSystem.Data.dogLevel = 2; SaveSystem.Data.dogRestSeconds = 470;
        SaveSystem.Data.freeUpgradeVouchers = 1;
        Assert.IsTrue(SaveSystem.TryPurchaseDogUpgrade(1000, 2));
        Assert.AreEqual(0, SaveSystem.Data.freeUpgradeVouchers);
        Assert.AreEqual(240, SaveSystem.Data.dogRestSeconds);
        Assert.AreEqual(0, SaveSystem.Data.cash);
    }

    [Test] public void CarriedRewardCannotBeCollectedAndIsReleasedAtHome()
    {
        var root = new GameObject("Test reward");
        var mouth = new GameObject("Test mouth");
        var player = new GameObject("Test interactor"); player.SetActive(false);
        try
        {
            var leg = root.AddComponent<ChickenLegPickup>();
            var collider = root.AddComponent<SphereCollider>();
            var interactor = player.AddComponent<PlayerInteractor>();
            Assert.IsTrue(leg.Carry(mouth.transform));
            Assert.IsFalse(collider.enabled);
            Assert.IsFalse(leg.TryGetPrompt(interactor, out _));
            Assert.IsFalse(leg.TryInteract(interactor));
            Assert.IsFalse(leg.Carry(mouth.transform));
            leg.PutDown(new Vector3(2, 3, 4));
            Assert.IsTrue(collider.enabled);
            Assert.IsNull(leg.transform.parent);
            Assert.AreEqual(new Vector3(2, 3, 4), leg.transform.position);
            Assert.IsTrue(leg.TryGetPrompt(interactor, out _));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mouth); Object.DestroyImmediate(player); }
    }
}
