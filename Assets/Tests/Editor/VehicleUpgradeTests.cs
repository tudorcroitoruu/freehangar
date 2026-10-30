using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class VehicleUpgradeTests
{
    private PlayerData previousData;
    private GameObject root;
    private VehicleUpgradeSettings settings;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData { cash = 100 };
        settings = ScriptableObject.CreateInstance<VehicleUpgradeSettings>();
        settings.vehicles = new[] { new VehicleUpgradeSettings.VehicleType
        {
            id = "test", displayName = "Test vehicle",
            levels = new[]
            {
                new VehicleUpgradeSettings.Level { capacityCubicMeters = 1f, slots = 1, price = 100 },
                new VehicleUpgradeSettings.Level { capacityCubicMeters = 2f, slots = 2, price = 0 }
            }
        } };
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
    public void SlotsOnlyProfileAllowsZeroCapacityAndStillLimitsCargoCount()
    {
        var profile = settings.vehicles[0];
        profile.upgradeCapacity = false;
        foreach (var level in profile.levels) level.capacityCubicMeters = 0;
        Assert.IsTrue(profile.IsConfigured);
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        Assert.IsTrue(float.IsPositiveInfinity(upgrades.CapacityVolume));
        Assert.IsTrue(upgrades.CanLoadVolume(1000));
        Assert.IsFalse(upgrades.CanLoadVolume(1000, 2));
        profile.upgradeCapacity = true;
        Assert.IsFalse(profile.IsConfigured);
    }

    [Test]
    public void CapacityLabelsHideAndRestoreWhenChangingProfilesIncludingMaxLevel()
    {
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        var screen = Child("Screen").AddComponent<UpgradeVehicle>();
        var current = Child("Current capacity").AddComponent<Text>();
        var next = Child("Next capacity").AddComponent<Text>();
        var max = Child("Max capacity").AddComponent<Text>();
        SetField(screen, "currentLevelCapacity", current);
        SetField(screen, "nextLevelCapacity", next);
        SetField(screen, "maxLevelCapacity", max);
        settings.vehicles[0].upgradeCapacity = false;
        screen.SetTarget(upgrades);
        Assert.IsFalse(current.gameObject.activeSelf);
        Assert.IsFalse(next.gameObject.activeSelf);
        Assert.IsFalse(max.gameObject.activeSelf);
        Assert.IsTrue(SaveSystem.Data.TryUpgradeVehicle("test", 0, 0, 1));
        screen.Refresh();
        Assert.IsFalse(max.gameObject.activeSelf);
        settings.vehicles[0].upgradeCapacity = true;
        screen.Refresh();
        Assert.IsTrue(current.gameObject.activeSelf);
        Assert.IsTrue(next.gameObject.activeSelf);
        Assert.IsTrue(max.gameObject.activeSelf);
        screen.SetTarget(null);
        Assert.IsFalse(current.gameObject.activeSelf);
        Assert.IsFalse(next.gameObject.activeSelf);
        Assert.IsFalse(max.gameObject.activeSelf);
    }

    [Test]
    public void PurchasesAreSavedPerTypeAndRejectStaleUnaffordableOrMaxedRequests()
    {
        var data = SaveSystem.Data;
        Assert.IsFalse(data.TryUpgradeVehicle("test", 101, 0, 2));
        Assert.IsFalse(data.TryUpgradeVehicle("test", -1, 0, 2));
        Assert.IsTrue(data.TryUpgradeVehicle("test", 100, 0, 2));
        Assert.AreEqual(0, data.cash);
        Assert.AreEqual(1, data.GetVehicleUpgradeLevel("test"));
        Assert.AreEqual(0, data.GetVehicleUpgradeLevel("bicycle"));
        Assert.IsFalse(data.TryUpgradeVehicle("test", 0, 0, 2));
        Assert.IsTrue(data.TryUpgradeVehicle("test", 0, 1, 2));
        Assert.IsFalse(data.TryUpgradeVehicle("test", 0, 2, 2));
        var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.AreEqual(2, loaded.GetVehicleUpgradeLevel("test"));
    }

    [Test]
    public void OldWheelbarrowUpgradesAndFreeVouchersRemainUsable()
    {
        var data = new PlayerData { cash = 0, wheelbarrowUpgradeLevels = new[] { 0, 1, 0 }, vehicleUpgrades = null, freeUpgradeVouchers = 1 };
        Assert.AreEqual(1, data.GetVehicleUpgradeLevel("wheelbarrow.1"));
        Assert.IsTrue(data.TryUpgradeVehicle("wheelbarrow.1", 250, 1, 2));
        Assert.AreEqual(2, data.GetVehicleUpgradeLevel("wheelbarrow.1"));
        Assert.AreEqual(0, data.freeUpgradeVouchers);
        Assert.AreEqual(0, data.cash);
        Assert.AreEqual(0, data.GetVehicleUpgradeLevel("wheelbarrow.0"));
    }

    [Test]
    public void BicycleSlotAndVolumeLimitsApplyToActualBoxLoadingAndUnloading()
    {
        root.SetActive(false);
        var bicycle = root.AddComponent<BicycleController>();
        var slot = new GameObject("Original slot").transform;
        slot.SetParent(root.transform);
        bicycle.cargoSlots = new[] { slot };
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        Call(upgrades, "Awake");
        Call(upgrades, "OnEnable");
        var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var second = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var firstBox = first.AddComponent<BicycleCargoBox>();
            var secondBox = second.AddComponent<BicycleCargoBox>();
            Assert.IsTrue(firstBox.Load(bicycle.FreeCargoSlot()));
            Assert.AreEqual(1f, upgrades.OccupiedVolume);
            Assert.IsNull(bicycle.FreeCargoSlot());
            Assert.IsFalse(upgrades.CanLoadVolume(.1f));
            Assert.IsTrue(SaveSystem.Data.TryUpgradeVehicle("test", 100, 0, 1));
            Call(upgrades, "RefreshSlots", 0);
            Assert.AreEqual(2f, upgrades.CapacityVolume);
            Assert.IsNotNull(bicycle.FreeCargoSlot());
            Assert.IsTrue(secondBox.Load(bicycle.FreeCargoSlot()));
            Assert.AreEqual(2, upgrades.CargoCount);
            Assert.IsFalse(upgrades.CanLoadVolume(.1f));
            Assert.IsTrue(firstBox.Unload(Vector3.zero));
            Assert.AreEqual(1, upgrades.CargoCount);
            Assert.AreEqual(1f, upgrades.OccupiedVolume);
        }
        finally
        {
            Call(upgrades, "OnDisable");
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void VehicleScreenShowsCurrentNextShortfallAndMaximum()
    {
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        var screen = Child("Screen").AddComponent<UpgradeVehicle>();
        var current = Child("Current");
        var next = Child("Next");
        var max = Child("Max");
        var price = Child("Price").AddComponent<Text>();
        var shortage = Child("Shortage").AddComponent<Text>();
        var button = Child("Button").AddComponent<Button>();
        var capacity = Child("Capacity").AddComponent<Text>();
        var slots = Child("Slots").AddComponent<Text>();
        SetField(screen, "currentLevelHolder", current);
        SetField(screen, "nextLevelHolder", next);
        SetField(screen, "maxLevelHolder", max);
        SetField(screen, "price", price);
        SetField(screen, "notEnoughMoney", shortage);
        SetField(screen, "upgradeButton", button);
        SetField(screen, "currentLevelCapacity", capacity);
        SetField(screen, "currentLevelSlots", slots);
        SaveSystem.Data.cash = 40;
        screen.SetTarget(upgrades);
        Assert.AreEqual("Capacity: <b>1 kg</b>", capacity.text);
        Assert.AreEqual("Slots: <b>1</b>", slots.text);
        Assert.AreEqual("You need $60 more.", shortage.text);
        Assert.IsFalse(button.interactable);
        settings.vehicles[0].levels[0].price = 0;
        screen.Refresh();
        Assert.AreEqual("Free", price.text);
        Assert.IsTrue(button.interactable);
        Assert.IsTrue(SaveSystem.Data.TryUpgradeVehicle("test", 0, 0, 1));
        screen.Refresh();
        Assert.AreEqual("MAX", price.text);
        Assert.IsFalse(current.activeSelf);
        Assert.IsFalse(next.activeSelf);
        Assert.IsTrue(max.activeSelf);
        Assert.IsFalse(button.interactable);
    }

    [Test]
    public void VehicleMenuConsumesOpeningERejectsUnaffordablePurchaseAndClosesOnEscape()
    {
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        var menu = Child("Menu");
        menu.SetActive(false);
        var manager = menu.AddComponent<UpgradeManager>();
        var panel = new GameObject("Vehicle panel");
        panel.transform.SetParent(menu.transform);
        var screen = panel.AddComponent<UpgradeVehicle>();
        SetField(manager, "upgradeVehicleScreen", screen);
        var closedFrame = typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic);
        int previousFrame = (int)closedFrame.GetValue(null);
        var previousLock = Cursor.lockState;
        bool previousVisible = Cursor.visible;
        SaveSystem.Data.cash = 0;
        try
        {
            Assert.IsTrue(manager.OpenVehicle(upgrades));
            Call(manager, "HandleInput", false, true);
            Assert.AreEqual(0, SaveSystem.Data.GetVehicleUpgradeLevel("test"));
            SetField(manager, "openedFrame", Time.frameCount - 1);
            Call(manager, "HandleInput", false, true);
            Assert.AreEqual(0, SaveSystem.Data.GetVehicleUpgradeLevel("test"));
            Assert.IsTrue(manager.IsOpen);
            Call(manager, "HandleInput", true, false);
            Assert.IsFalse(manager.IsOpen);
            Assert.IsFalse(screen.isActiveAndEnabled);
        }
        finally
        {
            manager.Close();
            closedFrame.SetValue(null, previousFrame);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }

    [Test]
    public void DefaultProfilesAreValidAndIncludeEachExistingVehicleFamily()
    {
        var shared = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        Assert.IsNotNull(shared);
        foreach (var id in new[] { "bicycle", "motoroller", "trike", "pickup", "lawnmower" })
            Assert.IsTrue(shared.Find(id)?.IsConfigured ?? false, id);
        foreach (var id in new[] { "wheelbarrow.0", "wheelbarrow.1", "wheelbarrow.2" })
            Assert.IsNull(shared.Find(id), id);
    }

    [TestCase("slot")]
    [TestCase("bag")]
    [TestCase("rack")]
    public void LoadedVehicleHidesUpgradePromptAndBlocksMenuAndPurchaseUntilUnloaded(string cargoLocation)
    {
        root.SetActive(false);
        var bicycle = root.AddComponent<BicycleController>();
        bicycle.motorDriven = true;
        bicycle.steering = Child("Steering").transform;
        bicycle.frontWheel = Child("Front wheel").transform;
        bicycle.rearWheel = Child("Rear wheel").transform;
        var slot = Child("Cargo slot").transform;
        bicycle.cargoSlots = new[] { slot };
        if (cargoLocation == "bag") bicycle.exclusiveBagSlot = slot;
        var upgrades = root.AddComponent<VehicleUpgrade>();
        SetField(upgrades, "settings", settings);
        SetField(upgrades, "vehicleId", "test");
        root.SetActive(true);
        if (cargoLocation == "rack")
        {
            var rack = root.AddComponent<VehicleCargoRack>();
            rack.surface = slot;
            slot = rack.FreeSlot(false);
        }
        var menu = Child("Upgrade menu");
        menu.SetActive(false);
        var manager = menu.AddComponent<UpgradeManager>();
        var screenObject = new GameObject("Vehicle screen");
        screenObject.transform.SetParent(menu.transform);
        var screen = screenObject.AddComponent<UpgradeVehicle>();
        var button = Child("Upgrade button").AddComponent<Button>();
        SetField(screen, "upgradeButton", button);
        SetField(manager, "upgradeVehicleScreen", screen);
        var station = root.AddComponent<VehicleUpgradeInteractable>();
        SetField(station, "upgradeManager", manager);
        var interactor = Child("Interactor").AddComponent<PlayerInteractor>();
        var closedFrame = typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic);
        int previousFrame = (int)closedFrame.GetValue(null);
        var previousLock = Cursor.lockState;
        bool previousVisible = Cursor.visible;
        var cargo = new GameObject("Loaded cargo");
        cargo.transform.SetParent(slot);
        try
        {
            closedFrame.SetValue(null, -1);
            Assert.IsTrue(upgrades.HasCargo);
            Assert.IsFalse(station.TryGetPrompt(interactor, out _));
            Assert.IsFalse(station.TryInteract(interactor));
            Assert.IsFalse(manager.OpenVehicle(upgrades));
            Assert.IsFalse(upgrades.OpenMenu());
            Assert.IsFalse(upgrades.TryUpgrade());
            screen.SetTarget(upgrades);
            Assert.IsFalse(button.interactable);
            Assert.AreEqual(100, SaveSystem.Money);
            Assert.AreEqual(0, SaveSystem.Data.GetVehicleUpgradeLevel("test"));

            Object.DestroyImmediate(cargo);
            Assert.IsFalse(upgrades.HasCargo);
            Assert.IsTrue(station.TryGetPrompt(interactor, out _));
            Assert.IsTrue(manager.OpenVehicle(upgrades));
            Assert.IsTrue(button.interactable);
        }
        finally
        {
            manager.Close();
            Object.DestroyImmediate(cargo);
            closedFrame.SetValue(null, previousFrame);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }

    [Test]
    public void WheelbarrowHasFixedCapacityAndDoesNotCreateUpgradeComponents()
    {
        var barrow = root.AddComponent<WheelbarrowCargo>();
        Call(barrow, "Awake");
        Assert.IsNull(root.GetComponent<VehicleUpgrade>());
        SetField(barrow, "cargoVolumeCubicMeters", .7f);
        SaveSystem.Data.wheelbarrowUpgradeLevels = new[] { 2, 2, 2 };
        Assert.AreEqual(.7f, barrow.CapacityVolume);

        var station = root.AddComponent<VehicleUpgradeInteractable>();
        var interactor = root.AddComponent<PlayerInteractor>();
        Assert.IsFalse(station.TryGetPrompt(interactor, out _));
        Assert.IsFalse(station.TryInteract(interactor));
        Assert.IsNull(root.GetComponent<VehicleUpgrade>());
    }
    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform);
        return child;
    }
    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
