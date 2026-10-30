using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HangarShopProgressTests
{
    [Test]
    public void FirstProductUnlocksAtHalfOfFirstHangarAndStaysUnlocked()
    {
        var data = new PlayerData();
        Assert.IsFalse(data.TryRecordHangarClearance(2, 100));
        Assert.IsFalse(data.TryRecordHangarClearance(1, 49.999f));
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
}
