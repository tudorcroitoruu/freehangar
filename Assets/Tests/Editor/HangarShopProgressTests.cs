using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HangarShopProgressTests
{
    [Test]
    public void FinalDoorAnimationStartsBeforeLaunchClockExists()
    {
        var root = new GameObject("Final door animation regression");
        root.SetActive(false);
        try
        {
            var sequence = root.AddComponent<HangarLaunchSequence>();
            sequence.roomDoor = new GameObject("Door hinge").transform;
            sequence.roomDoor.SetParent(root.transform, false);
            var rest = Quaternion.Euler(0, 35, 0);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(HangarLaunchSequence).GetField("doorClosed", flags).SetValue(sequence, rest);
            Assert.IsNull(typeof(HangarLaunchSequence).GetField("saveClock", flags).GetValue(sequence));
            var animation = (System.Collections.IEnumerator)typeof(HangarLaunchSequence).GetMethod("OpenRoom", flags).Invoke(sequence, null);
            Assert.IsTrue(animation.MoveNext(), "Opening the door must not access the uninitialized launch clock.");
            Assert.Less(Quaternion.Angle(rest, sequence.roomDoor.localRotation), .01f);
            sequence.RestoreDoor(true);
            Assert.Less(Quaternion.Angle(rest * Quaternion.Euler(0, -100, 0), sequence.roomDoor.localRotation), .01f);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void FinalDoorNeedsOnlyLocalClearanceAndRechecksNewGrain()
    {
        var root = new GameObject("Final doorway test");
        root.SetActive(false);
        var pileObject = new GameObject("Remaining hangar grain");
        bool bypass = GameManager.Instance != null && GameManager.Instance.bypassFinalRoomRestrictions;
        try
        {
            if (GameManager.Instance != null) GameManager.Instance.bypassFinalRoomRestrictions = false;
            pileObject.transform.position = new Vector3(25000, 100, 25000);
            var pile = pileObject.AddComponent<GrainPile>();
            var sequence = root.AddComponent<HangarLaunchSequence>();
            sequence.hangar = root.AddComponent<HangarKeyDoor>();
            sequence.grain = pile;
            sequence.clearancePoint = new GameObject("Door clearance").transform;
            sequence.clearancePoint.SetParent(root.transform);
            sequence.clearancePoint.position = pileObject.transform.position + Vector3.right * 100;
            Assert.IsFalse(sequence.CanOpen, "Hangar must first be unlocked.");
            typeof(HangarKeyDoor).GetProperty("IsUnlocked").SetValue(sequence.hangar, true);
            Assert.Greater(pile.Kilograms, 5);
            Assert.IsTrue(sequence.CanOpen, "Grain elsewhere must not lock the final room.");
            pileObject.transform.position += Vector3.up * 10;
            Assert.IsTrue(sequence.CanOpen, "An unrelated elevated pile base must not block the door.");
            pileObject.transform.position -= Vector3.up * 10;
            sequence.clearancePoint.position = pileObject.transform.position;
            Assert.IsFalse(sequence.CanOpen, "Clearing once must not bypass newly blocking grain.");
            var field = (GrainHeightField)typeof(GrainPile).GetField("field", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pile);
            var heights = (float[])field.Heights.Clone();
            System.Array.Clear(field.Heights, 0, field.Heights.Length);
            sequence.clearancePoint.position -= Vector3.up;
            Assert.IsTrue(sequence.CanOpen, "An empty pile above the clearance marker is not grain obstruction.");
            System.Array.Copy(heights, field.Heights, heights.Length);
            sequence.clearancePoint.position += Vector3.up;
            sequence.clearancePoint.position += Vector3.right * 100;
            Assert.IsTrue(sequence.CanOpen);
        }
        finally
        {
            if (GameManager.Instance != null) GameManager.Instance.bypassFinalRoomRestrictions = bypass;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(pileObject);
        }
    }

    [Test]
    public void SecondBagUnlocksWithMotorollerAndStaysUnlockedAfterReload()
    {
        var root = new GameObject("Motoroller bag unlock");
        try
        {
            var product = root.AddComponent<ShopProduct>();
            SetUnlock(product, ShopProduct.UnlockCondition.VehicleUnlocked, 1);
            var data = new PlayerData();
            Assert.IsFalse(product.IsUnlocked(data));
            data.TryRecordHangarClearance(1, 100);
            Assert.IsFalse(product.IsUnlocked(data), "Hangar clearance alone must not unlock bag 2.");
            data.unlockedVehicles.Add("motoroller");
            Assert.IsTrue(product.IsUnlocked(data));
            var restored = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
            Assert.IsTrue(product.IsUnlocked(restored));
            Assert.IsFalse(product.IsUnlocked(new PlayerData()));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test]
    public void FirstProductUnlocksAtHalfOfFirstHangarAndStaysUnlocked()
    {
        var data = new PlayerData();
        Assert.IsTrue(data.TryRecordHangarClearance(2, 100));
        Assert.IsTrue(data.TryRecordHangarClearance(1, 49.999f));
        Assert.IsFalse(data.TryRecordHangarClearance(1, float.NaN));
        Assert.IsFalse(data.TryRecordHangarClearance(1, float.PositiveInfinity));
        Assert.IsFalse(data.firstHangarHalfCleared);
        Assert.IsTrue(data.TryRecordHangarClearance(1, 50));
        Assert.IsFalse(data.TryRecordHangarClearance(1, 50));
        Assert.IsFalse(data.TryRecordHangarClearance(1, 0));
        Assert.IsTrue(data.firstHangarHalfCleared);
    }

    [Test]
    public void LegacySaveImportsOpenedDoorsButNotCollectedKeys()
    {
        var data = JsonUtility.FromJson<PlayerData>("{\"foundSecrets\":[\"farm.hangar.open.2\",\"farm.hangar.key.3\",\"farm.hangar.open.5\"]}");
        data.MigrateHangarProgress();
        Assert.IsTrue(data.IsHangarOpen(1));
        Assert.IsTrue(data.IsHangarOpen(2));
        Assert.IsFalse(data.IsHangarOpen(3));
        Assert.IsFalse(data.IsHangarOpen(4));
        Assert.IsTrue(data.IsHangarOpen(5));
        CollectionAssert.AreEquivalent(new[] { 1, 2, 5 }, data.openedHangars);
        Assert.IsFalse(data.MigrateHangarProgress());
    }

    [Test]
    public void OpenHangarsAndHalfClearedMilestoneSurviveSerialization()
    {
        var data = new PlayerData { firstHangarHalfCleared = true, openedHangars = null };
        Assert.IsTrue(data.TryOpenHangar(3));
        Assert.IsFalse(data.TryOpenHangar(3));
        Assert.IsFalse(data.TryOpenHangar(0));
        Assert.IsFalse(data.TryOpenHangar(6));
        var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.IsTrue(loaded.firstHangarHalfCleared);
        Assert.IsTrue(loaded.IsHangarOpen(1));
        Assert.IsTrue(loaded.IsHangarOpen(3));
        Assert.IsFalse(loaded.IsHangarOpen(2));
    }

    [Test]
    public void ShopUsesExactMilestonesAndRefreshesAfterReload()
    {
        var previous = SaveSystem.Data;
        bool wasLoaded = SaveSystem.savesLoaded;
        var root = new GameObject("Hangar shop test");
        try
        {
            SaveSystem.savesLoaded = true;
            SaveSystem.Data = new PlayerData();
            var shop = root.AddComponent<ShopManager>();
            var products = new ShopProduct[5];
            for (int i = 0; i < products.Length; i++)
            {
                var product = new GameObject("Product " + i);
                product.transform.SetParent(root.transform);
                products[i] = product.AddComponent<ShopProduct>();
                SetUnlock(products[i], i == 0 ? ShopProduct.UnlockCondition.HangarCleared : ShopProduct.UnlockCondition.HangarOpened, i + 1);
            }
            typeof(ShopManager).GetField("_shopProducts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(shop, products);
            shop.RefreshProductAvailability();
            AssertStates(products, false, false, false, false, false);
            SaveSystem.Data.firstHangarHalfCleared = true;
            shop.RefreshProductAvailability();
            AssertStates(products, true, false, false, false, false);
            for (int hangar = 2; hangar <= 5; hangar++)
            {
                SaveSystem.Data.TryOpenHangar(hangar);
                shop.RefreshProductAvailability();
                for (int i = 0; i < 5; i++) Assert.AreEqual(i < hangar, products[i].gameObject.activeSelf);
            }
            SaveSystem.Data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(SaveSystem.Data));
            shop.RefreshProductAvailability();
            AssertStates(products, true, true, true, true, true);
            SaveSystem.Data = new PlayerData { openedHangars = new List<int> { 1, 4 } };
            shop.RefreshProductAvailability();
            AssertStates(products, false, false, false, true, false);
            SaveSystem.savesLoaded = false;
            shop.RefreshProductAvailability();
            AssertStates(products, false, false, false, false, false);
        }
        finally
        {
            Object.DestroyImmediate(root);
            SaveSystem.Data = previous;
            SaveSystem.savesLoaded = wasLoaded;
        }
    }

    [TestCase("smallBag", "Motoroller", 0)]
    [TestCase("largeBag", "Hangar 2", 2)]
    [TestCase("bigBag", "Hangar 5", 5)]
    public void ShelfBagStaysVisibleAndExplainsItsUnlock(string id, string requirement, int hangar)
    {
        var previous = SaveSystem.Data;
        bool wasLoaded = SaveSystem.savesLoaded;
        var shelf = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Gameplay/ContainersShop.prefab"));
        try
        {
            SaveSystem.savesLoaded = true;
            SaveSystem.Data = new PlayerData { cash = 2000 };
            var bag = System.Array.Find(shelf.GetComponentsInChildren<ShopProduct>(true), p => p.SaveProductId == id);
            var manager = shelf.AddComponent<ShopManager>();
            typeof(ShopManager).GetField("_shopProducts", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, new[] { bag });
            bag.gameObject.SetActive(false);
            manager.RefreshProductAvailability();
            Assert.IsTrue(bag.gameObject.activeSelf);
            Assert.IsTrue(bag.TryGetPrompt(null, out var locked));
            Assert.IsFalse(locked.ShowKey);
            StringAssert.Contains("Locked", locked.Action);
            StringAssert.Contains(requirement, bag.UnlockHint());
            Assert.IsFalse(bag.TryInteract(null));
            Assert.AreEqual(2000, SaveSystem.Data.cash);

            if (hangar == 0) SaveSystem.Data.unlockedVehicles.Add("motoroller");
            else SaveSystem.Data.TryOpenHangar(hangar);
            Assert.IsTrue(bag.TryGetPrompt(null, out var unlocked));
            Assert.IsTrue(unlocked.ShowKey);
            StringAssert.StartsWith("Buy ", unlocked.Action);
        }
        finally
        {
            Object.DestroyImmediate(shelf);
            SaveSystem.Data = previous;
            SaveSystem.savesLoaded = wasLoaded;
        }
    }

    [Test]
    public void ClearingMeasuresRemainingMassRatherThanRepeatedlyMovedGrain()
    {
        var root = new GameObject("Hangar clearance test");
        try
        {
            var pile = root.AddComponent<GrainPile>();
            double initial = pile.InitialKilograms;
            Assert.Greater(initial, 0);
            Assert.AreEqual(0, pile.ClearedPercent, .001f);
            float removed = pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, (float)(initial * .5));
            Assert.AreEqual(50, pile.ClearedPercent, .05f);
            var cargo = new GrainCargo();
            cargo.Add(GrainType.Corn, removed);
            Assert.IsTrue(pile.Deposit(Vector3.zero, cargo));
            Assert.AreEqual(0, pile.ClearedPercent, .05f);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AssertStates(ShopProduct[] products, params bool[] expected)
    {
        for (int i = 0; i < products.Length; i++) Assert.AreEqual(expected[i], products[i].gameObject.activeSelf, "Product " + i);
    }

    private static void SetUnlock(ShopProduct product, ShopProduct.UnlockCondition condition, int hangar)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ShopProduct).GetField("unlockCondition", flags).SetValue(product, condition);
        typeof(ShopProduct).GetField("requiredHangar", flags).SetValue(product, hangar);
    }

    [Test]
    public void ProductUnlocksAtConfiguredPercentOfTheSelectedHangarAndStaysUnlockedAfterReload()
    {
        var root = new GameObject("75 percent unlock");
        try
        {
            var product = root.AddComponent<ShopProduct>();
            SetUnlock(product, ShopProduct.UnlockCondition.HangarCleared, 3);
            typeof(ShopProduct).GetField("requiredClearedPercent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(product, 75f);
            var data = new PlayerData();
            data.TryOpenHangar(3);
            Assert.IsTrue(data.TryRecordHangarClearance(2, 100));
            Assert.IsFalse(product.IsUnlocked(data), "Clearing another hangar must not satisfy this condition.");
            Assert.IsTrue(data.TryRecordHangarClearance(3, 74.999f));
            Assert.IsFalse(product.IsUnlocked(data));
            Assert.IsTrue(data.TryRecordHangarClearance(3, 75));
            Assert.IsTrue(product.IsUnlocked(data));
            Assert.IsFalse(data.TryRecordHangarClearance(3, 25));
            Assert.AreEqual(75f, data.GetHangarClearedPercent(3));
            var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
            Assert.IsTrue(product.IsUnlocked(loaded));
            Assert.AreEqual(75f, loaded.GetHangarClearedPercent(3));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ZeroPercentStillRequiresOpeningTheSelectedHangar()
    {
        var root = new GameObject("Zero percent unlock");
        try
        {
            var product = root.AddComponent<ShopProduct>();
            SetUnlock(product, ShopProduct.UnlockCondition.HangarCleared, 4);
            typeof(ShopProduct).GetField("requiredClearedPercent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(product, 0f);
            var data = new PlayerData { foundSecrets = new[] { "farm.hangar.key.4" } };
            Assert.IsFalse(product.IsUnlocked(data));
            data.TryOpenHangar(4);
            Assert.IsTrue(product.IsUnlocked(data));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ClearanceSupportsAllHangarsAndMigratesTheOldHalfClearedMilestone()
    {
        var data = JsonUtility.FromJson<PlayerData>("{\"firstHangarHalfCleared\":true}");
        Assert.AreEqual(50f, data.GetHangarClearedPercent(1));
        Assert.IsFalse(data.TryRecordHangarClearance(1, 25));
        for (int hangar = 1; hangar <= 5; hangar++)
        {
            Assert.IsTrue(data.TryRecordHangarClearance(hangar, 75));
            Assert.AreEqual(75f, data.GetHangarClearedPercent(hangar));
            Assert.IsFalse(data.TryRecordHangarClearance(hangar, 101));
            Assert.IsFalse(data.TryRecordHangarClearance(hangar, float.NaN));
        }
        Assert.IsFalse(data.TryRecordHangarClearance(0, 75));
        Assert.IsFalse(data.TryRecordHangarClearance(6, 75));
    }

    [Test]
    public void MoreThanFiveProductsAndDuplicateIdsUseTheirOwnConditionsRegardlessOfArrayOrder()
    {
        var previous = SaveSystem.Data;
        bool wasLoaded = SaveSystem.savesLoaded;
        var root = new GameObject("Independent product unlock test");
        try
        {
            SaveSystem.savesLoaded = true;
            SaveSystem.Data = new PlayerData { openedHangars = new List<int> { 1, 4 } };
            var shop = root.AddComponent<ShopManager>();
            var products = new ShopProduct[7];
            for (int i = 0; i < products.Length; i++)
            {
                var item = new GameObject("Product copy " + i); item.transform.SetParent(root.transform);
                products[i] = item.AddComponent<ShopProduct>();
                typeof(ShopProduct).GetField("productID", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(products[i], "sameProduct");
            }
            SetUnlock(products[0], ShopProduct.UnlockCondition.HangarOpened, 5);
            SetUnlock(products[1], ShopProduct.UnlockCondition.Always, 2);
            SetUnlock(products[2], ShopProduct.UnlockCondition.HangarCleared, 1);
            typeof(ShopProduct).GetField("requiredClearedPercent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(products[2], 75f);
            SetUnlock(products[3], ShopProduct.UnlockCondition.HangarOpened, 2);
            SetUnlock(products[4], ShopProduct.UnlockCondition.HangarOpened, 4);
            SetUnlock(products[5], ShopProduct.UnlockCondition.Always, 2);
            SetUnlock(products[6], ShopProduct.UnlockCondition.HangarOpened, 3);
            var field = typeof(ShopManager).GetField("_shopProducts", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(shop, new[] { products[6], products[5], null, products[4], products[3], products[2], products[1], products[0] });
            typeof(ShopManager).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shop, null);
            AssertStates(products, false, true, false, false, true, true, false);
            Assert.IsTrue(SaveSystem.TryOpenHangar(5));
            ProcessPendingAvailability(shop);
            AssertStates(products, true, true, false, false, true, true, false);
            Assert.IsTrue(SaveSystem.TryUnlockFirstHangarShopProduct(50));
            ProcessPendingAvailability(shop);
            AssertStates(products, true, true, false, false, true, true, false);
            Assert.IsTrue(SaveSystem.TryRecordHangarClearance(1, 75));
            ProcessPendingAvailability(shop);
            AssertStates(products, true, true, true, false, true, true, false);
            field.SetValue(shop, products);
            shop.RefreshProductAvailability();
            AssertStates(products, true, true, true, false, true, true, false);
        }
        finally
        {
            var manager = root.GetComponent<ShopManager>();
            if (manager != null) typeof(ShopManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Object.DestroyImmediate(root);
            SaveSystem.Data = previous;
            SaveSystem.savesLoaded = wasLoaded;
        }
    }
    private static void ProcessPendingAvailability(ShopManager shop)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ShopManager).GetField("nextAvailabilityCheck", flags).SetValue(shop, 0f);
        typeof(ShopManager).GetMethod("Update", flags).Invoke(shop, null);
    }
}
