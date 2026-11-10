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
        var title = Child("Requirements title").AddComponent<Text>();
        var button = Child("Upgrade").AddComponent<Button>();
        SetField(screen, "currentLevelMovementSpeed", currentSpeed);
        SetField(screen, "nextLevelMovementSpeed", nextSpeed);
        SetField(screen, "currentLevelSlots", currentSlots);
        SetField(screen, "notEnoughMoney", message);
        SetField(screen, "notEnoughMoneyTitle", title);
        SetField(screen, "upgradeButton", button);
        settings.vehicles[1].requiresKey = true;
        screen.SetTarget(target);
        Assert.AreEqual("Move speed: <b>25 km/h</b>", currentSpeed.text);
        StringAssert.Contains("43 km/h", nextSpeed.text);
        Assert.AreEqual("Slots: <b>1</b>", currentSlots.text);
        StringAssert.Contains("key required", title.text);
        Assert.IsFalse(message.gameObject.activeSelf);
        Assert.IsFalse(button.interactable);
        SaveSystem.Data.foundSecrets = new[] { "vehicle.key.second" };
        screen.Refresh();
        Assert.IsTrue(button.interactable);
    }
    [Test]
    [TestCase(100, false, "Vehicle key required.", "")]
    [TestCase(99, false, "Requirements not met.", "Not enough money and vehicle key missing.")]
    [TestCase(99, true, "Not enough money", "$ 1")]
    [TestCase(100, true, "", "")]
    public void ScreenExplainsWhichPurchaseRequirementsAreMissing(int cash, bool hasKey, string expectedTitle, string expectedDetails)
    {
        var target=root.AddComponent<VehicleUpgrade>();
        SetField(target,"settings",settings);SetField(target,"vehicleId","first");
        var screen=Child("Requirement screen").AddComponent<UpgradeVehicle>();
        var message=Child("Requirement message").AddComponent<Text>();
        var title=Child("Requirement title").AddComponent<Text>();
        var popup=Child("Requirement popup");
        var button=Child("Requirement button").AddComponent<Button>();
        SetField(screen,"notEnoughMoney",message);SetField(screen,"notEnoughMoneyTitle",title);SetField(screen,"notEnoughMoneyPopup",popup);SetField(screen,"upgradeButton",button);
        settings.vehicles[1].requiresKey=true;
        SaveSystem.Data.cash=cash;
        SaveSystem.Data.foundSecrets=hasKey?new[]{settings.vehicles[1].RequiredKeyId}:new string[0];
        screen.SetTarget(target);
        Assert.That(title.text,Is.EqualTo(expectedTitle));
        Assert.That(message.text,Is.EqualTo(expectedDetails));
        Assert.That(message.gameObject.activeSelf,Is.EqualTo(cash<100));
        Assert.That(popup.activeSelf,Is.EqualTo(expectedTitle.Length>0));
        Assert.That(button.interactable,Is.EqualTo(cash>=100&&hasKey));
        SaveSystem.Data.cash=0;screen.Refresh();Assert.IsTrue(message.gameObject.activeSelf);
        SaveSystem.Data.cash=100;screen.Refresh();Assert.IsFalse(message.gameObject.activeSelf);
    }

    [Test]
    public void ScreenShowsTheNextVehiclesKeyAndClearsItWhenNoKeyIsRequiredOrMaxed()
    {
        var texture=new Texture2D(1,1);
        var currentKey=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f);
        var nextKey=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f);
        var lastKey=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f);
        try
        {
            var target=root.AddComponent<VehicleUpgrade>();SetField(target,"settings",settings);SetField(target,"vehicleId","first");
            var screen=Child("Key upgrade screen").AddComponent<UpgradeVehicle>();
            var keyParent=Child("Key parent");
            var image=Child("Key image").AddComponent<Image>();image.transform.SetParent(keyParent.transform);
            var button=Child("Key purchase button").AddComponent<Button>();
            SetField(screen,"keyParent",keyParent);SetField(screen,"keyImage",image);SetField(screen,"upgradeButton",button);
            settings.vehicles[0].keySprite=currentKey;
            settings.vehicles[1].requiresKey=true;settings.vehicles[1].keySprite=nextKey;
            screen.SetTarget(target);
            Assert.IsTrue(keyParent.activeSelf);Assert.IsTrue(image.enabled);Assert.AreSame(nextKey,image.sprite);Assert.IsFalse(button.interactable);
            SaveSystem.Data.foundSecrets=new[]{settings.vehicles[1].RequiredKeyId};
            SaveSystem.Data.cash=99;screen.Refresh();Assert.IsFalse(button.interactable);
            SaveSystem.Data.cash=100;screen.Refresh();Assert.IsTrue(button.interactable);Assert.IsTrue(keyParent.activeSelf);
            settings.vehicles[1].requiresKey=false;screen.Refresh();
            Assert.IsFalse(keyParent.activeSelf);Assert.IsNull(image.sprite);Assert.IsFalse(image.enabled);
            SaveSystem.Data.unlockedVehicles.Add("second");SetField(target,"vehicleId","second");
            settings.vehicles[2].requiresKey=true;settings.vehicles[2].keySprite=lastKey;
            screen.SetTarget(target);Assert.IsTrue(keyParent.activeSelf);Assert.AreSame(lastKey,image.sprite);
            settings.vehicles[2].keySprite=null;screen.Refresh();Assert.IsTrue(keyParent.activeSelf);Assert.IsNull(image.sprite);Assert.IsFalse(image.enabled);
            SetField(target,"vehicleId","last");screen.SetTarget(target);
            Assert.IsFalse(keyParent.activeSelf);Assert.IsNull(image.sprite);
            screen.SetTarget(null);Assert.IsFalse(keyParent.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(currentKey);Object.DestroyImmediate(nextKey);Object.DestroyImmediate(lastKey);Object.DestroyImmediate(texture);
        }
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

    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    [TestCase(false, false, true)]
    [TestCase(false, false, false)]
    public void MotorollerUpgradeFindsOutsideGroundBeforePurchase(bool cleared, bool groundAvailable, bool inside)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var cache = (System.Collections.Generic.List<Transform>)typeof(HangarVehicleAccess).GetField("hangars", flags).GetValue(null);
        var previousHangars = cache.ToArray();
        var scan = typeof(HangarVehicleAccess).GetField("nextScan", flags);
        var previousScan = scan.GetValue(null);
        var hangar = new GameObject("01_Modular_Hangar");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        VehicleUpgrade replacement = null;
        try
        {
            hangar.transform.position = new Vector3(10000, 0, 10000);
            cache.Clear(); cache.Add(hangar.transform); scan.SetValue(null, float.PositiveInfinity);
            if (cleared) SaveSystem.Data.TryRecordHangarClearance(1, 98);
            ground.transform.position = hangar.transform.position + Vector3.down * .5f;
            ground.transform.localScale = new Vector3(100, 1, 100);
            ground.SetActive(groundAvailable);
            root.transform.position = hangar.transform.position + (inside ? Vector3.zero : Vector3.right * 40);
            settings.vehicles[0].id = "motoroller";
            settings.vehicles[1].prefabResourcePath = "Vehicles/3_Trike_Driveable";
            var target = root.AddComponent<VehicleUpgrade>();
            SetField(target, "settings", settings); SetField(target, "vehicleId", "motoroller");
            var args = new object[] { null };
            bool created = (bool)typeof(VehicleUpgrade).GetMethod("TryCreateReplacement", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
            replacement = (VehicleUpgrade)args[0];
            Assert.AreEqual(!inside || groundAvailable, created);
            Assert.AreEqual(100, SaveSystem.Data.cash);
            if (!created) { Assert.IsNull(replacement); return; }
            if (inside)
            {
                foreach (var mesh in replacement.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mesh.sharedMesh == null) continue;
                    var local = mesh.sharedMesh.bounds;
                    var bounds = new Bounds(mesh.transform.TransformPoint(local.center), Vector3.zero);
                    for (int corner = 0; corner < 8; corner++)
                        bounds.Encapsulate(mesh.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                    Assert.IsFalse(HangarVehicleAccess.BlocksPlacement(bounds, null, true), mesh.name);
                }
                Assert.Greater(Vector3.Distance(root.transform.position, replacement.transform.position), 10);
            }
            else Assert.AreEqual(root.transform.position, replacement.transform.position);
        }
        finally
        {
            if (replacement != null) Object.DestroyImmediate(replacement.gameObject);
            Object.DestroyImmediate(ground); Object.DestroyImmediate(hangar);
            cache.Clear(); cache.AddRange(previousHangars); scan.SetValue(null, previousScan);
        }
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
    [TestCase(.52f, 0f, 1f, false)] // Two physics ticks of render lead at 13 m/s.
    [TestCase(1.3f, 0f, 1f, false)] // A slow render frame must not fake a collision.
    [TestCase(.52f, .4f, 1f, true)] // A genuinely stretched joint still stops towing.
    [TestCase(.52f, .4f, -1f, false)] // Reversing toward a stuck trailer remains possible.
    public void LawnmowerTowLimitUsesPhysicsAnchors(float renderLead, float jointGap,
        float direction, bool blocked)
    {
        root.SetActive(false);
        root.transform.position = new Vector3(10000, 10000, 10000);
        var mower = root.AddComponent<LawnmowerController>();
        var trailer = new GameObject("Trailer");
        trailer.transform.SetParent(root.transform, false);
        var trailerBody = trailer.AddComponent<Rigidbody>();
        trailerBody.isKinematic = true;
        var joint = trailer.AddComponent<ConfigurableJoint>();
        var hitch = new GameObject("Hitch");
        hitch.transform.SetParent(root.transform, false);
        var hitchBody = hitch.AddComponent<Rigidbody>();
        hitchBody.isKinematic = true;
        hitchBody.position = trailerBody.position + Vector3.forward * jointGap;
        joint.connectedBody = hitchBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = joint.connectedAnchor = Vector3.zero;
        var target = new GameObject("Render hitch target");
        target.transform.SetParent(root.transform, false);
        target.transform.position = hitchBody.position + Vector3.forward * renderLead;
        mower.trailerJoint = joint;
        mower.hitchBody = hitchBody;
        mower.hitchTarget = target.transform;

        // Register the bodies before reading their physics poses. Inactive bodies
        // retain native poses independently of the staged transform hierarchy.
        root.SetActive(true);
        trailerBody.position = trailer.transform.position;
        hitchBody.position = trailerBody.position + Vector3.forward * jointGap;
        Physics.SyncTransforms();

        Vector3 motion = Vector3.forward * (direction * .2f);
        Assert.That(mower.ConstrainTowMotion(motion), Is.EqualTo(blocked ? Vector3.zero : motion));
    }

    [Test]
    public void RestoredLawnmowerReconnectsSiblingTrailerAndCargoAtSavedPosition()
    {
        root.SetActive(false);
        var prefab = Resources.Load<GameObject>("Vehicles/4_Lawnmower_With_Trailer");
        var assembly = Object.Instantiate(prefab, root.transform);
        var mower = assembly.GetComponentInChildren<LawnmowerController>(true);
        var trailer = mower.trailerJoint.GetComponent<Rigidbody>();
        Assert.IsFalse(trailer.transform.IsChildOf(mower.transform));
        mower.transform.SetPositionAndRotation(new Vector3(700, 50, -500), Quaternion.Euler(0, 110, 0));
        Assert.Greater(Vector3.Distance(mower.hitchTarget.position, trailer.position), 100f);

        mower.ReconnectTrailer();

        root.SetActive(true);
        Physics.SyncTransforms();

        Vector3 trailerAnchor = trailer.position + trailer.rotation *
            Vector3.Scale(mower.trailerJoint.anchor, trailer.transform.lossyScale);
        Assert.Less(Vector3.Distance(trailerAnchor, mower.hitchTarget.position), .001f);
        Assert.Less(Vector3.Distance(mower.hitchBody.position, mower.hitchTarget.position), .001f);
        Assert.That(trailer.linearVelocity.sqrMagnitude, Is.LessThan(.000001f));
        Assert.That(trailer.angularVelocity.sqrMagnitude, Is.LessThan(.000001f));
        SaveSystem.Data.unlockedVehicles.Add("lawnmower");
        var rig = mower.GetComponent<BicycleController>();
        var slot = rig.FreeBagCargoSlot();
        Assert.IsNotNull(slot);
        Assert.AreSame(rig, VehicleCargoOwner.Resolve(slot));
        Assert.Less(Vector3.Distance(slot.position, mower.transform.position), 8f);
    }

    [TestCase(0f)]
    [TestCase(.35f)]
    public void RestoredTrailerSettlesOnItsWheelsBeforeParking(float hitchLift)
    {
        var simulationMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            root.SetActive(false);
            root.transform.position = new Vector3(10000, 10000, 10000);
            var floor = new GameObject("Trailer ground");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = Vector3.down * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
            var assembly = Object.Instantiate(Resources.Load<GameObject>("Vehicles/4_Lawnmower_With_Trailer"), root.transform);
            var mower = assembly.GetComponentInChildren<LawnmowerController>(true);
            var rig = mower.GetComponent<BicycleController>();
            typeof(LawnmowerController).GetField("movement", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mower, rig);
            root.SetActive(true);
            Physics.SyncTransforms();
            mower.ReconnectTrailer();
            var trailer = mower.trailerJoint.GetComponent<Rigidbody>();
            Assert.IsFalse(trailer.isKinematic, "Restoration must let the suspension settle before locking the trailer.");
            mower.hitchTarget.position += Vector3.up * hitchLift;
            mower.hitchTarget.rotation = Quaternion.Euler(hitchLift > 0f ? 8f : 0f, 0, 0);
            var physics = Physics.defaultPhysicsScene;
            for (int i = 0; i < 500; i++)
            {
                mower.UpdateHitch();
                physics.Simulate(Time.fixedDeltaTime);
            }
            foreach (var wheel in mower.trailerWheels)
                Assert.IsTrue(wheel.GetGroundHit(out _), "Both trailer tires must rest on the ground.");
            Assert.IsTrue(trailer.isKinematic, "The settled trailer must lock while unattended.");
            float pitch = Mathf.DeltaAngle(0, trailer.rotation.eulerAngles.x);
            Assert.That(Mathf.Abs(pitch), Is.LessThan(15f), "A modest hitch adjustment must not tip the trailer into the air.");
            mower.hitchTarget.position += Vector3.up * .05f;
            mower.UpdateHitch();
            Assert.IsFalse(trailer.isKinematic, "A later ground correction must release the parking lock to settle again.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Physics.simulationMode = simulationMode;
        }
    }

    [Test]
    public void LoadedCargoCannotPushItsTrailerAndRegainsCollisionWhenUnloaded()
    {
        root.SetActive(false);
        var mower = root.AddComponent<LawnmowerController>();
        var trailer = new GameObject("Dynamic trailer");
        var bag = new GameObject("Kinematic loaded bag");
        var ground = new GameObject("External ground");
        try
        {
            trailer.AddComponent<Rigidbody>();
            var hull = trailer.AddComponent<BoxCollider>();
            mower.trailerJoint = trailer.AddComponent<ConfigurableJoint>();
            bag.transform.SetParent(trailer.transform);
            bag.AddComponent<Rigidbody>().isKinematic = true;
            var bagCollider = bag.AddComponent<BoxCollider>();
            var groundCollider = ground.AddComponent<BoxCollider>();
            Assert.IsFalse(Physics.GetIgnoreCollision(bagCollider, hull));
            mower.RefreshTrailerCollisions();
            Assert.IsTrue(Physics.GetIgnoreCollision(bagCollider, hull));
            Assert.IsFalse(Physics.GetIgnoreCollision(hull, groundCollider));
            Assert.IsFalse(Physics.GetIgnoreCollision(bagCollider, groundCollider));
            // Loading reenables colliders; the next physics step must restore filtering.
            Physics.IgnoreCollision(bagCollider, hull, false);
            mower.RefreshTrailerCollisions();
            Assert.IsTrue(Physics.GetIgnoreCollision(bagCollider, hull));
            bag.transform.SetParent(null);
            mower.RefreshTrailerCollisions();
            Assert.IsFalse(Physics.GetIgnoreCollision(bagCollider, hull));
        }
        finally
        {
            Object.DestroyImmediate(bag);
            Object.DestroyImmediate(trailer);
            Object.DestroyImmediate(ground);
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
