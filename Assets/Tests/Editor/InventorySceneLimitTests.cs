using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InventorySceneLimitTests
{
    private GameObject root, prefab;
    private InventoryManager manager;
    private InventoryProduct product;
    private PlayerData previousData;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = null;
        root = new GameObject("Inventory limit test");
        prefab = new GameObject("Inventory limit prefab");
        manager = root.AddComponent<InventoryManager>();
        product = root.AddComponent<InventoryProduct>();
        SetField(product, "productID", "freeBag");
        SetField(product, "productName", "Free Bag");
        SetField(product, "unlimitedCount", true);
        SetField(product, "objectPrefab", prefab);
        SetField(product, "deliveryMode", InventoryProduct.DeliveryMode.AtSpawnPoint);
        SetField(product, "spawnPoint", root.transform);
    }

    [TearDown]
    public void TearDown()
    {
        while (manager.SpawnedProducts.Count > 0)
        {
            var item = manager.SpawnedProducts[0];
            manager.UnregisterInstance(item);
            if (item != null) Object.DestroyImmediate(item.gameObject);
        }
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(prefab);
        SaveSystem.Data = previousData;
    }

    [TestCase(1)]
    [TestCase(10)]
    public void UnlimitedStockStillHonorsActiveSceneLimit(int limit)
    {
        SetField(product, "maxActiveInScene", limit);
        for (int i = 0; i < limit; i++) Assert.IsTrue(product.TryInteract(null));
        Assert.AreEqual(limit, manager.GetActiveCount("freeBag"));
        Assert.IsFalse(product.TryInteract(null));
        Assert.AreEqual(limit, manager.SpawnedProducts.Count);
        product.TryGetPrompt(null, out var prompt);
        Assert.IsFalse(prompt.ShowKey);
        Assert.AreEqual("Take 1 Free Bag", prompt.Action);
        Assert.IsFalse(prompt.HasAvailableInput);
        Assert.IsFalse(prompt.RequiresFreeHands);
        Assert.IsNull(SaveSystem.Data);
    }

    [Test]
    public void InactiveInstanceStaysTrackedAndCannotReactivateOverTheLimit()
    {
        SetField(product, "maxActiveInScene", 1);
        Assert.IsTrue(product.TryInteract(null));
        var first = manager.SpawnedProducts[0];
        first.gameObject.SetActive(false);
        Assert.AreEqual(0, manager.GetActiveCount("freeBag"));
        Assert.AreEqual(1, manager.SpawnedProducts.Count);
        Assert.IsTrue(product.TryInteract(null));
        Assert.AreEqual(2, manager.SpawnedProducts.Count);
        first.gameObject.SetActive(true);
        // Edit Mode does not automatically invoke the runtime OnEnable callback.
        typeof(InventoryItemInstance).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first, null);
        Assert.IsFalse(first.gameObject.activeSelf);
        Assert.AreEqual(1, manager.GetActiveCount("freeBag"));
    }

    [Test]
    public void DestroyedInstanceFreesASlotAndNewInstanceGetsNewIdentity()
    {
        SetField(product, "maxActiveInScene", 1);
        Assert.IsTrue(product.TryInteract(null));
        var first = manager.SpawnedProducts[0];
        string firstID = first.InstanceID;
        Assert.AreEqual("freeBag", first.ProductID);
        Assert.IsNotEmpty(firstID);
        Object.DestroyImmediate(first.gameObject);
        Assert.AreEqual(0, manager.GetActiveCount("freeBag"));
        Assert.IsTrue(product.TryInteract(null));
        Assert.AreNotEqual(firstID, manager.SpawnedProducts[0].InstanceID);
    }

    [Test]
    public void ZeroLimitAllowsMoreThanTenInstancesAndTracksEachOne()
    {
        SetField(product, "maxActiveInScene", 0);
        for (int i = 0; i < 11; i++) Assert.IsTrue(product.TryInteract(null));
        Assert.AreEqual(11, manager.SpawnedProducts.Count);
        Assert.AreEqual(11, manager.GetActiveCount("freeBag"));
    }

    [Test]
    public void TwoInventoryEntriesWithSameProductIDShareTheSceneCount()
    {
        SetField(product, "maxActiveInScene", 1);
        var secondRoot = new GameObject("Second inventory point");
        secondRoot.transform.SetParent(root.transform);
        var second = secondRoot.AddComponent<InventoryProduct>();
        SetField(second, "productID", "freeBag");
        SetField(second, "unlimitedCount", true);
        SetField(second, "maxActiveInScene", 1);
        SetField(second, "objectPrefab", prefab);
        SetField(second, "deliveryMode", InventoryProduct.DeliveryMode.AtSpawnPoint);
        SetField(second, "spawnPoint", secondRoot.transform);
        Assert.IsTrue(product.TryInteract(null));
        Assert.IsFalse(second.TryInteract(null));
        Assert.AreEqual(1, manager.SpawnedProducts.Count);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
