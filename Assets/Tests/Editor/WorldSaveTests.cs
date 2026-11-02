using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class WorldSaveTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void SavedLooseAndLoadedBagsRegisterWithInventoryForExistingAndRecreatedObjects(bool recreate)
    {
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool separateScene = !string.IsNullOrEmpty(originalScene.path);
        var originalRoots = new System.Collections.Generic.HashSet<GameObject>(originalScene.GetRootGameObjects());
        var scene = separateScene
            ? UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Additive)
            : originalScene;
        var originalData = SaveSystem.Data;
        bool wasLoaded = SaveSystem.savesLoaded;
        var template = new GameObject("Saved bag template");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(template, originalScene);
        template.SetActive(false);
        template.AddComponent<GrainContainer>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject Create(string name)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        try
        {
            SaveSystem.Data = new PlayerData();
            SaveSystem.savesLoaded = false;
            var manager = Create("Inventory").AddComponent<InventoryManager>();
            var shop = Create("Free bag shop").AddComponent<ShopProduct>();
            typeof(ShopProduct).GetField("productID", flags).SetValue(shop, "freeBags");
            typeof(ShopProduct).GetField("objectPrefab", flags).SetValue(shop, template);
            typeof(ShopProduct).GetField("maxActiveInScene", flags).SetValue(shop, 16);
            var barrow = Create("Barrow").AddComponent<WheelbarrowCargo>();
            var slot = Create("Bag slot").transform;
            slot.SetParent(barrow.transform, false);
            var loose = Create("Loose bag").AddComponent<GrainContainer>();
            var loaded = Create("Loaded bag").AddComponent<GrainContainer>();
            loaded.transform.SetParent(slot, false);
            var inactive = Create("Inactive bag").AddComponent<GrainContainer>();
            inactive.gameObject.SetActive(false);
            var adapter = Create("Save adapter").AddComponent<WorldSaveRuntime>();
            typeof(WorldSaveRuntime).GetMethod("Awake", flags).Invoke(adapter, null);
            var saved = adapter.Capture();
            string templateId = WorldSaveRuntime.Identity(template);
            saved.bags.RemoveAll(bag => bag.id == templateId);
            saved.authoredContainers.Remove(templateId);
            Assert.AreEqual(2, saved.bags.Count);
            Assert.AreEqual(1, saved.wheelbarrows[0].bags.Count);
            if (recreate)
            {
                Object.DestroyImmediate(loose.gameObject);
                Object.DestroyImmediate(loaded.gameObject);
                Object.DestroyImmediate(inactive.gameObject);
            }
            var restore = typeof(WorldSaveRuntime).GetMethod("Restore", flags);
            restore.Invoke(adapter, new object[] { saved });
            Assert.AreEqual(3, manager.SpawnedProducts.Count);
            Assert.AreEqual(2, manager.GetActiveCount("freeBags"));
            Assert.AreEqual(0, manager.GetActiveCount("FreeBag"));
            foreach (var item in manager.SpawnedProducts)
            {
                Assert.AreEqual("freeBags", item.ProductID);
                Assert.AreEqual(16, typeof(InventoryItemInstance).GetProperty("MaxActiveInScene", flags).GetValue(item));
            }
            restore.Invoke(adapter, new object[] { saved });
            Assert.AreEqual(3, manager.SpawnedProducts.Count, "Repeated restore must not duplicate inventory registrations.");
            Object.DestroyImmediate(manager.SpawnedProducts[0].gameObject);
            Assert.AreEqual(2, manager.SpawnedProducts.Count, "Destroyed restored bags must unregister normally.");
        }
        finally
        {
            if (separateScene) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            else foreach (var go in scene.GetRootGameObjects())
                if (!originalRoots.Contains(go)) Object.DestroyImmediate(go);
            Object.DestroyImmediate(template);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            SaveSystem.Data = originalData;
            SaveSystem.savesLoaded = wasLoaded;
        }
    }

    [Test]
    public void RestoringSavedInventoryDoesNotApplyTheNewDeliveryLimit()
    {
        var root = new GameObject("Restored inventory limit");
        try
        {
            var manager = root.AddComponent<InventoryManager>();
            for (int i = 0; i < 2; i++)
            {
                var bag = new GameObject("Saved bag");
                bag.transform.SetParent(root.transform);
                Assert.IsNotNull(typeof(InventoryManager).GetMethod("RegisterRestoredInstance", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { bag, "freeBags", 1 }));
            }
            Assert.AreEqual(2, manager.GetActiveCount("freeBags"));
            Assert.IsFalse(manager.CanSpawn("freeBags", 1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PartiallyBrokenBarrierRestoresHitProgress()
    {
        var go = new GameObject("Saved barrier");
        try
        {
            var barrier = go.AddComponent<BreakableGrainBarrier>();
            typeof(BreakableGrainBarrier).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(barrier, null);
            barrier.RestoreSave(2, true);
            Assert.AreEqual(2, barrier.SavedHits); Assert.AreEqual(1, barrier.HitsRemaining);
            Assert.IsTrue(barrier.IsExposed);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void WorldRoundTripRestoresVehicleCargoAndDoesNotRespawnSoldBag()
    {
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool separateScene = !string.IsNullOrEmpty(originalScene.path);
        var originalRoots = new System.Collections.Generic.HashSet<GameObject>(originalScene.GetRootGameObjects());
        var scene = separateScene
            ? UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Additive)
            : originalScene;
        var originalData = SaveSystem.Data; bool loaded = SaveSystem.savesLoaded;
        SaveSystem.Data = new PlayerData(); SaveSystem.savesLoaded = false;
        try
        {
            var vehicle = new GameObject("Barrow"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(vehicle, scene);
            vehicle.transform.position = new Vector3(10, 2, -5);
            vehicle.transform.rotation = Quaternion.Euler(0, 45, 0); vehicle.AddComponent<WheelbarrowCargo>();
            var slot = new GameObject("Bag slot 1"); slot.transform.SetParent(vehicle.transform, false); slot.transform.localPosition = Vector3.up;
            var bag = new GameObject("Loaded sack"); bag.transform.SetParent(slot.transform, false);
            var cargo = bag.AddComponent<GrainContainer>(); cargo.RestoreSave(new ContainerSave { kilograms = 20, grain = 3 });
            var sold = new GameObject("Sold sack"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sold, scene); sold.AddComponent<GrainContainer>();
            var host = new GameObject("Save adapter"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene); var adapter = host.AddComponent<WorldSaveRuntime>();
            typeof(WorldSaveRuntime).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
            string soldId = WorldSaveRuntime.Identity(sold);
            var snapshot = adapter.Capture(); snapshot.bags.RemoveAll(x => x.id == soldId);
            Assert.AreEqual(1, snapshot.wheelbarrows.Count);
            Assert.AreEqual(1, snapshot.wheelbarrows[0].bags.Count);
            snapshot = SaveSystem.DeserializePlayerData(JsonUtility.ToJson(new PlayerData { world = snapshot })).world;
            var position = vehicle.transform.position; var rotation = vehicle.transform.rotation; var bagPosition = bag.transform.position;
            vehicle.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bag.transform.SetParent(null); Object.DestroyImmediate(slot);
            cargo.SetDemonstrationFill(1);
            typeof(WorldSaveRuntime).GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, new object[] { snapshot });
            Assert.AreEqual(position, vehicle.transform.position);
            Assert.Less(Quaternion.Angle(rotation, vehicle.transform.rotation), .001f);
            Assert.Less(Vector3.Distance(bagPosition, bag.transform.position), .001f);
            Assert.AreEqual(20, cargo.Kilograms); Assert.AreEqual((GrainType)3, cargo.AcceptedGrain);
            Assert.AreEqual(vehicle.transform, bag.transform.parent.parent);
            Assert.AreEqual(1, vehicle.GetComponent<WheelbarrowCargo>().BagCount);
            Assert.IsTrue(sold == null, "A paid/sold authored sack must remain deleted.");
        }
        finally
        {
            if (separateScene) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            else foreach (var root in scene.GetRootGameObjects())
                if (!originalRoots.Contains(root)) Object.DestroyImmediate(root);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            SaveSystem.Data = originalData; SaveSystem.savesLoaded = loaded;
        }
    }

    [Test]
    public void NativePlayerDataRoundTripsNestedVehicleCargo()
    {
        var world = new WorldSaveData { scene = "Gameplay", playerPosition = new Vector3(12,3,-9),
            playerRotation = Quaternion.Euler(0,123,0), introPlaying = true, introTime = 12.3f, carriedBag = "bag-1" };
        world.bags.Add(new BagSave { id = "bag-1", parent = "vehicle-1", slot = "/Rack#0/Slot#2",
            container = new ContainerSave { kilograms = 34, grain = 3, closure = .7f, closing = true } });
        world.vehicles.Add(new VehicleSave { id = "vehicle-1" });
        world.NestCargo();
        string json = JsonUtility.ToJson(new PlayerData { world = world, cash = 123 });
        Assert.IsFalse(json.Contains("worldSnapshot"));
        var profile = SaveSystem.DeserializePlayerData(json);
        Assert.AreEqual(123, profile.cash);
        var copy = profile.world;
        Assert.AreEqual(0, copy.bags.Count);
        Assert.AreEqual(1, copy.vehicles[0].bags.Count);
        Assert.AreEqual(world.playerPosition, copy.playerPosition);
        Assert.Less(Quaternion.Angle(world.playerRotation, copy.playerRotation), .001f);
        Assert.AreEqual(12.3f, copy.introTime); Assert.AreEqual("bag-1", copy.carriedBag);
        Assert.AreEqual("/Rack#0/Slot#2", copy.vehicles[0].bags[0].slot);
        Assert.AreEqual(34, copy.vehicles[0].bags[0].container.kilograms);
    }

    [Test]
    public void LegacyCompressedWorldMigratesToNativeNestedLists()
    {
        const string legacyWorld = "{\"version\":1,\"scene\":\"Gameplay\",\"objects\":[{\"id\":\"scene:Wheelbarrow\",\"kind\":\"vehicle\"},{\"id\":\"bag\",\"kind\":\"container\",\"parent\":\"scene:Wheelbarrow\",\"container\":{\"kilograms\":7,\"grain\":2}}]}";
        string encoded = Convert.ToBase64String(SaveSystem.CompressString(legacyWorld));
        var data = SaveSystem.DeserializePlayerData("{\"cash\":45,\"worldSnapshot\":\"" + encoded + "\"}");
        Assert.AreEqual(45, data.cash);
        Assert.AreEqual(2, data.world.version);
        Assert.AreEqual(1, data.world.wheelbarrows.Count);
        Assert.AreEqual(7, data.world.wheelbarrows[0].bags[0].container.kilograms);
        Assert.AreEqual(0, data.world.bags.Count);
        Assert.IsFalse(JsonUtility.ToJson(data).Contains("worldSnapshot"));
        Assert.Throws<FormatException>(() => SaveSystem.DeserializePlayerData("{\"worldSnapshot\":\"broken!\"}"));
    }

    [Test]
    public void GrainGridAndCropStrataRoundTripWithoutRegrowingMaterial()
    {
        var go = new GameObject("Save grain test"); go.SetActive(false);
        try
        {
            var pile = go.AddComponent<GrainPile>();
            typeof(GrainPile).GetField("useSpatialLayout", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pile, true);
            pile.Initialize(); go.SetActive(true);
            pile.Scoop(go.transform.position, 2, .3f, 20, new GrainCargo());
            var saved = pile.CaptureSave(); double mass = pile.Kilograms;
            float cleared = pile.ClearedPercent;
            pile.Scoop(go.transform.position, 2, .3f, 20, new GrainCargo());
            pile.RestoreSave(saved);
            Assert.That(pile.Kilograms, Is.EqualTo(mass).Within(.001));
            Assert.That(pile.ClearedPercent, Is.EqualTo(cleared).Within(.0001));
            CollectionAssert.AreEqual(saved.heights, pile.CaptureSave().heights);
            Assert.AreEqual(saved.columns.Count, pile.CaptureSave().columns.Count);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ContainerRestoresCropMassAndIdentityWithoutAwardingProgress()
    {
        var go = new GameObject("Saved bag");
        try
        {
            var bag = go.AddComponent<GrainContainer>();
            bag.RestoreSave(new ContainerSave { kilograms = 17.5f, grain = 2, achievementId = "already-counted" });
            Assert.AreEqual(17.5f, bag.Kilograms); Assert.AreEqual((GrainType)2, bag.AcceptedGrain);
            Assert.AreEqual("already-counted", bag.AchievementIdentity); Assert.IsFalse(bag.IsCarried);
            var copy = JsonUtility.FromJson<ContainerSave>(JsonUtility.ToJson(bag.CaptureSave()));
            bag.SetDemonstrationFill(0); bag.RestoreSave(copy);
            Assert.AreEqual(17.5f, bag.Kilograms);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void CutsceneSeekReconstructsCompletedShotsAndStopsAtSavedFraction()
    {
        var clock = new SavedCutsceneClock(3);
        float first = -1, second = -1;
        IEnumerator Sequence()
        {
            yield return clock.Animate(2, t => first = t);
            yield return clock.Animate(4, t => second = t);
        }
        var playback = clock.Run(Sequence());
        Assert.IsTrue(playback.MoveNext());
        Assert.AreEqual(1, first); Assert.AreEqual(.25f, second);
        Assert.AreEqual(3, clock.Elapsed); Assert.IsFalse(clock.IsSeeking);
        (playback as IDisposable)?.Dispose();
    }

    [Test]
    public void WorldIdentitySurvivesReparentAndVehicleReplacement()
    {
        var first = new GameObject("Vehicle"); var second = new GameObject("Upgrade");
        try
        {
            string id = WorldSaveRuntime.Identity(first, "authored-vehicle");
            first.transform.SetParent(second.transform);
            Assert.AreEqual(id, WorldSaveRuntime.Identity(first));
            WorldSaveIdentity.Transfer(first, second);
            Assert.AreEqual(id, WorldSaveRuntime.Identity(second));
        }
        finally { Object.DestroyImmediate(second); }
    }
}
