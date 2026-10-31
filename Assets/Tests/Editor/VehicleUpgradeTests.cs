using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class VehicleUpgradeTests
{
    private PlayerData previousData;
    private VehicleUpgradeSettings settings;
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData { cash = 100 };
        settings = ScriptableObject.CreateInstance<VehicleUpgradeSettings>();
        settings.vehicles = new[]
        {
            new VehicleUpgradeSettings.VehicleType { id = "first", displayName = "First", slots = 1, movementSpeed = 7 },
            new VehicleUpgradeSettings.VehicleType { id = "second", displayName = "Second", slots = 4, movementSpeed = 12, price = 100 },
            new VehicleUpgradeSettings.VehicleType { id = "last", displayName = "Last", slots = 6, movementSpeed = 15, price = 200 }
        };
        root = new GameObject("Vehicle upgrade test");
    }
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
        SaveSystem.Data = previousData;
    }
    [Test]
    public void EachPurchaseUnlocksExactlyOneNextVehicleAndSurvivesReload()
    {
        var data = SaveSystem.Data;
        Assert.IsTrue(settings.IsUnlocked("first", data));
        Assert.IsFalse(settings.IsUnlocked("second", data));
        Assert.IsFalse(settings.TryPurchase(data, "second"));
        Assert.IsTrue(settings.TryPurchase(data, "first"));
        Assert.AreEqual(0, data.cash);
        Assert.IsTrue(settings.IsUnlocked("second", data));
        Assert.IsFalse(settings.IsUnlocked("last", data));
        Assert.IsFalse(settings.TryPurchase(data, "first"));
        var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.AreSame(settings.vehicles[1], settings.ResolveSavedVehicle("first", loaded));
        loaded.cash = 200;
        Assert.IsTrue(settings.TryPurchase(loaded, "second"));
        Assert.AreSame(settings.vehicles[2], settings.ResolveSavedVehicle("first", loaded));
        Assert.IsFalse(settings.TryPurchase(loaded, "last"));
        Assert.IsNull(settings.Next("last"));
    }
    [Test]
    public void KeyRequiredNeedsBothMoneyAndMatchingCollectedKey()
    {
        var data = SaveSystem.Data;
        var next = settings.vehicles[1];
        next.requiresKey = true;
        Assert.AreEqual("vehicle.key.second", next.RequiredKeyId);
        Assert.IsFalse(settings.TryPurchase(data, "first"));
        Assert.AreEqual(100, data.cash);
        data.foundSecrets = new[] { "wrong-key" };
        Assert.IsFalse(settings.TryPurchase(data, "first"));
        next.keyId = "farm.vehicle.key";
        data.foundSecrets = new[] { next.keyId };
        data.cash = 99;
        Assert.IsFalse(settings.TryPurchase(data, "first"));
        data.cash = 100;
        Assert.IsTrue(settings.TryPurchase(data, "first"));
        CollectionAssert.Contains(data.foundSecrets, next.keyId);
        Assert.AreEqual(0, data.cash);
    }
    [Test]
    public void VoucherPaysPriceButNeverBypassesRequiredKey()
    {
        var data = SaveSystem.Data;
        data.cash = 0;
        data.freeUpgradeVouchers = 1;
        settings.vehicles[1].requiresKey = true;
        Assert.IsFalse(settings.TryPurchase(data, "first"));
        Assert.AreEqual(1, data.freeUpgradeVouchers);
        data.foundSecrets = new[] { "vehicle.key.second" };
        Assert.IsTrue(settings.TryPurchase(data, "first"));
        Assert.AreEqual(0, data.freeUpgradeVouchers);
        Assert.AreEqual(0, data.cash);
    }
    [Test]
    public void LegacyCompletedMiniupgradesRestoreDirectlyToSavedDestination()
    {
        var data = SaveSystem.Data;
        Assert.IsTrue(data.TryUpgradeVehicle("first", 0, 0, 2));
        Assert.AreSame(settings.vehicles[0], settings.ResolveSavedVehicle("first", data));
        Assert.IsTrue(data.TryUpgradeVehicle("first", 0, 1, 2));
        Assert.AreSame(settings.vehicles[1], settings.ResolveSavedVehicle("first", data));
        Assert.IsTrue(data.TryUpgradeVehicle("second", 0, 0, 2));
        Assert.IsTrue(data.TryUpgradeVehicle("second", 0, 1, 2));
        Assert.AreSame(settings.vehicles[2], settings.ResolveSavedVehicle("first", data));
        Assert.AreEqual(100, data.cash);
    }
    [Test]
    public void DisplayStatsNeverOverrideAuthoredSlotsOrSpeed()
    {
        root.SetActive(false);
        var rig = root.AddComponent<BicycleController>();
        rig.motorDriven = true;
        rig.steering = Child("Steering").transform;
        rig.frontWheel = Child("Front wheel").transform;
        rig.rearWheel = Child("Rear wheel").transform;
        var authoredSlots = new[] { Child("Authored cargo slot").transform };
        rig.cargoSlots = authoredSlots;
        rig.maximumSpeed = 9;
        var upgrade = root.AddComponent<VehicleUpgrade>();
        SetField(upgrade, "settings", settings);
        SetField(upgrade, "vehicleId", "first");
        settings.vehicles[0].slots = 999;
        settings.vehicles[0].movementSpeed = 999;
        root.SetActive(true);
        Assert.AreSame(authoredSlots, rig.cargoSlots);
        Assert.AreEqual(9, rig.maximumSpeed);
        Assert.AreEqual(1, upgrade.SlotCount);
        Assert.IsTrue(upgrade.CanLoadCargo());
        Assert.IsFalse(upgrade.CanLoadCargo(2));
    }
    [Test]
    public void ScreenShowsCurrentAndNextVehicleSpeedAndSlotsAndBlocksMissingKey()
    {
        var target = root.AddComponent<VehicleUpgrade>();
        SetField(target, "settings", settings);
        SetField(target, "vehicleId", "first");
        var screen = Child("Screen").AddComponent<UpgradeVehicle>();
        var currentSpeed = Child("Current speed").AddComponent<Text>();
        var nextSpeed = Child("Next speed").AddComponent<Text>();
        var currentSlots = Child("Current slots").AddComponent<Text>();
        var message = Child("Requirements").AddComponent<Text>();
        var button = Child("Upgrade").AddComponent<Button>();
        SetField(screen, "currentLevelMovementSpeed", currentSpeed);
        SetField(screen, "nextLevelMovementSpeed", nextSpeed);
        SetField(screen, "currentLevelSlots", currentSlots);
        SetField(screen, "notEnoughMoney", message);
        SetField(screen, "upgradeButton", button);
        settings.vehicles[1].requiresKey = true;
        screen.SetTarget(target);
        Assert.AreEqual("Movement speed: <b>7 m/s</b>", currentSpeed.text);
        StringAssert.Contains("12 m/s", nextSpeed.text);
        Assert.AreEqual("Slots: <b>1</b>", currentSlots.text);
        StringAssert.Contains("key required", message.text);
        Assert.IsFalse(button.interactable);
        SaveSystem.Data.foundSecrets = new[] { "vehicle.key.second" };
        screen.Refresh();
        Assert.IsTrue(button.interactable);
    }
    [Test]
    public void MissingPrefabDoesNotChargeOrUnlockNextVehicle()
    {
        var next = settings.vehicles[1];
        next.prefabResourcePath = "Tests/VehiclePrefabThatDoesNotExist";
        var target = root.AddComponent<VehicleUpgrade>();
        SetField(target, "settings", settings);
        SetField(target, "vehicleId", "first");
        LogAssert.Expect(LogType.Warning,
            "Cannot upgrade vehicle: Resources/Tests/VehiclePrefabThatDoesNotExist must be a prefab containing BicycleController.");
        Assert.IsFalse(target.TryUpgrade());
        Assert.AreEqual(100, SaveSystem.Data.cash);
        Assert.IsFalse(settings.IsUnlocked("second", SaveSystem.Data));
        Assert.IsNull(target.Replacement);
        Assert.IsFalse(target.TryAdvanceToUnlockedVehicle());
    }
    [Test]
    public void DefaultVehicleProfilesAndResourcesAreValid()
    {
        var shared = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        Assert.IsNotNull(shared);
        foreach (var profile in shared.vehicles)
        {
            Assert.IsTrue(profile.IsConfigured, profile.id);
            var prefab = Resources.Load<GameObject>(profile.PrefabResourcePath);
            Assert.IsNotNull(prefab, profile.PrefabResourcePath);
            Assert.IsNotNull(prefab.GetComponentInChildren<BicycleController>(true), profile.id);
        }
    }
    [Test]
    public void TrailerVehicleReplacementKeepsTheAssemblyAndCanAdvanceToPickup()
    {
        settings.vehicles[1].prefabResourcePath = "Vehicles/4_Lawnmower_With_Trailer";
        settings.vehicles[2].prefabResourcePath = "Vehicles/5_Pick_up_Driveable";
        var source = root.AddComponent<VehicleUpgrade>();
        SetField(source, "settings", settings);
        SetField(source, "vehicleId", "first");
        var create = typeof(VehicleUpgrade).GetMethod("TryCreateReplacement", BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject assembly = null, pickup = null;
        try
        {
            object[] result = { null };
            Assert.IsTrue((bool)create.Invoke(source, result));
            var next = (VehicleUpgrade)result[0];
            Assert.IsNotNull(next.GetComponent<BicycleController>());
            Assert.IsNotNull(next.GetComponent<LawnmowerController>());
            assembly = next.transform.parent.gameObject;
            Assert.IsFalse(assembly.activeSelf);
            Assert.Greater(assembly.GetComponentsInChildren<Transform>(true).Length, 2);
            Assert.IsNotNull(next.GetComponent<LawnmowerController>().trailerJoint);
            Assert.AreEqual("second", next.VehicleId);
            SaveSystem.Data.unlockedVehicles.Add("second");
            result[0] = null;
            Assert.IsTrue((bool)create.Invoke(next, result));
            var last = (VehicleUpgrade)result[0];
            pickup = last.gameObject;
            Assert.IsNotNull(last.GetComponent<PickupController>());
            Assert.AreEqual(assembly.transform.parent, pickup.transform.parent);
            Assert.AreEqual("last", last.VehicleId);
        }
        finally
        {
            Object.DestroyImmediate(pickup);
            Object.DestroyImmediate(assembly);
        }
    }
    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform);
        return child;
    }
    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
