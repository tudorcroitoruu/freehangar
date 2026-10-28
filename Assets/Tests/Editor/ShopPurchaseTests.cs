using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ShopPurchaseTests
{
    [Test]
    public void PurchaseWithExactMoneyAddsOneProduct()
    {
        var data = new PlayerData { cash = 100 };
        Assert.IsTrue(data.TryBuyProduct("crate", 100));
        Assert.AreEqual(0, data.cash);
        Assert.AreEqual(1, data.ownedProducts.Count);
        Assert.AreEqual("crate", data.ownedProducts[0].productID);
        Assert.AreEqual(1, data.ownedProducts[0].ownCount);
    }

    [Test]
    public void RepeatPurchaseIncrementsExistingEntryAndOtherProductsRemainSeparate()
    {
        var data = new PlayerData { cash = 500 };
        Assert.IsTrue(data.TryBuyProduct("crate", 100));
        Assert.IsTrue(data.TryBuyProduct("bag", 50));
        Assert.IsTrue(data.TryBuyProduct("crate", 100));
        Assert.AreEqual(250, data.cash);
        Assert.AreEqual(2, data.ownedProducts.Count);
        Assert.AreEqual(2, data.ownedProducts.Find(product => product.productID == "crate").ownCount);
        Assert.AreEqual(1, data.ownedProducts.Find(product => product.productID == "bag").ownCount);
    }

    [Test]
    public void InsufficientMoneyDoesNotChangeCashOrOwnership()
    {
        var data = new PlayerData { cash = 100 };
        Assert.IsTrue(data.TryBuyProduct("crate", 50));
        Assert.IsFalse(data.TryBuyProduct("crate", 51));
        Assert.IsFalse(data.TryBuyProduct("bag", 51));
        Assert.AreEqual(50, data.cash);
        Assert.AreEqual(1, data.ownedProducts.Count);
        Assert.AreEqual(1, data.ownedProducts[0].ownCount);
    }

    [TestCase(null, 10)]
    [TestCase("", 10)]
    [TestCase("   ", 10)]
    [TestCase("crate", -10)]
    public void InvalidProductOrPriceDoesNotChangeData(string id, int price)
    {
        var data = new PlayerData { cash = 100 };
        Assert.IsFalse(data.TryBuyProduct(id, price));
        Assert.AreEqual(100, data.cash);
        Assert.IsEmpty(data.ownedProducts);
    }

    [Test]
    public void OlderSaveWithoutProductListCanPurchase()
    {
        var data = new PlayerData { cash = 100, ownedProducts = null };
        Assert.IsTrue(data.TryBuyProduct("crate", 20));
        Assert.AreEqual(80, data.cash);
        Assert.AreEqual(1, data.ownedProducts[0].ownCount);
    }

    [Test]
    public void ProductCountCannotOverflow()
    {
        var data = new PlayerData { cash = 100, ownedProducts = new List<OwnedProductData>
        {
            new OwnedProductData { productID = "crate", ownCount = int.MaxValue }
        } };
        Assert.IsFalse(data.TryBuyProduct("crate", 10));
        Assert.AreEqual(100, data.cash);
        Assert.AreEqual(int.MaxValue, data.ownedProducts[0].ownCount);
    }

    [Test]
    public void CashAndOwnershipSurviveJsonRoundTrip()
    {
        var data = new PlayerData { cash = 100 };
        data.TryBuyProduct("crate", 20);
        var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.AreEqual(80, loaded.cash);
        Assert.AreEqual("crate", loaded.ownedProducts[0].productID);
        Assert.AreEqual(1, loaded.ownedProducts[0].ownCount);
    }

    [Test]
    public void TakingLastProductKeepsZeroCountAndCannotTakeAgain()
    {
        var data = new PlayerData { cash = 100 };
        data.TryBuyProduct("crate", 20);
        Assert.IsTrue(data.TryTakeProduct("crate"));
        Assert.AreEqual(0, data.GetOwnedProductCount("crate"));
        Assert.AreEqual(0, data.ownedProducts[0].ownCount);
        Assert.AreEqual(80, data.cash);
        Assert.IsFalse(data.TryTakeProduct("crate"));
        Assert.AreEqual(0, data.ownedProducts[0].ownCount);
        Assert.IsTrue(data.TryBuyProduct("crate", 20));
        Assert.AreEqual(1, data.ownedProducts.Count);
        Assert.AreEqual(1, data.GetOwnedProductCount("crate"));
    }

    [Test]
    public void TakingOneProductLeavesOtherProductsUnchanged()
    {
        var data = new PlayerData { cash = 100 };
        data.TryBuyProduct("crate", 20);
        data.TryBuyProduct("crate", 20);
        data.TryBuyProduct("bag", 10);
        Assert.IsTrue(data.TryTakeProduct("crate"));
        Assert.AreEqual(1, data.GetOwnedProductCount("crate"));
        Assert.AreEqual(1, data.GetOwnedProductCount("bag"));
        Assert.AreEqual(50, data.cash);
    }

    [Test]
    public void MissingInventoryAndUnknownProductsCannotBeTaken()
    {
        var data = new PlayerData { cash = 100, ownedProducts = null };
        Assert.IsFalse(data.TryTakeProduct("crate"));
        Assert.IsFalse(data.TryTakeProduct(null));
        Assert.AreEqual(0, data.GetOwnedProductCount("crate"));
        Assert.AreEqual(0, data.GetOwnedProductCount(null));
        Assert.AreEqual(100, data.cash);
    }

    [TestCase(0, "Crate\nOwn: 0", false)]
    [TestCase(2, "Take 1 Crate\nLeft: 2", true)]
    public void InventoryPromptReflectsCurrentStock(int count, string text, bool showKey)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Inventory product test");
        var prefab = new GameObject("Product prefab test");
        try
        {
            SaveSystem.Data = new PlayerData { ownedProducts = new List<OwnedProductData>
            {
                new OwnedProductData { productID = "crate", ownCount = count }
            } };
            var product = root.AddComponent<InventoryProduct>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(InventoryProduct).GetField("productID", flags).SetValue(product, "crate");
            typeof(InventoryProduct).GetField("productName", flags).SetValue(product, "Crate");
            typeof(InventoryProduct).GetField("objectPrefab", flags).SetValue(product, prefab);
            typeof(InventoryProduct).GetField("deliveryMode", flags).SetValue(product, InventoryProduct.DeliveryMode.AtSpawnPoint);
            typeof(InventoryProduct).GetField("spawnPoint", flags).SetValue(product, root.transform);
            BindInventoryManager(product);
            Assert.IsTrue(product.TryGetPrompt(null, out var prompt));
            Assert.AreEqual(text, prompt.Action);
            Assert.AreEqual(showKey, prompt.ShowKey);
            if (count == 0) Assert.IsFalse(product.TryInteract(null));
        }
        finally
        {
            SaveSystem.Data = previousData;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(prefab);
        }
    }

    [TestCase(InventoryProduct.DeliveryMode.Hands)]
    [TestCase(InventoryProduct.DeliveryMode.AtSpawnPoint)]
    public void MissingCarrierOrSpawnPointDoesNotConsumeStock(InventoryProduct.DeliveryMode mode)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Undeliverable inventory product test");
        var prefab = new GameObject("Product prefab test");
        try
        {
            SaveSystem.Data = new PlayerData { ownedProducts = new List<OwnedProductData>
            {
                new OwnedProductData { productID = "crate", ownCount = 1 }
            } };
            var product = root.AddComponent<InventoryProduct>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(InventoryProduct).GetField("productID", flags).SetValue(product, "crate");
            typeof(InventoryProduct).GetField("objectPrefab", flags).SetValue(product, prefab);
            typeof(InventoryProduct).GetField("deliveryMode", flags).SetValue(product, mode);
            BindInventoryManager(product);
            Assert.IsFalse(product.TryInteract(null));
            Assert.AreEqual(1, SaveSystem.Data.GetOwnedProductCount("crate"));
            product.TryGetPrompt(null, out var prompt);
            Assert.IsFalse(prompt.ShowKey);
        }
        finally
        {
            SaveSystem.Data = previousData;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void UnlimitedProductIsVisibleAndCanBeTakenRepeatedlyWithoutSave()
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Unlimited inventory test");
        var visual = new GameObject("Inventory visual");
        visual.transform.SetParent(root.transform);
        var prefab = new GameObject("Unlimited prefab test " + System.Guid.NewGuid().ToString("N"));
        var spawned = new List<GameObject>();
        try
        {
            SaveSystem.Data = null;
            var product = root.AddComponent<InventoryProduct>();
            var manager = root.AddComponent<InventoryManager>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(InventoryProduct).GetField("unlimitedCount", flags).SetValue(product, true);
            typeof(InventoryProduct).GetField("productID", flags).SetValue(product, "freeBag");
            typeof(InventoryProduct).GetField("productName", flags).SetValue(product, "Free Bag");
            typeof(InventoryProduct).GetField("objectPrefab", flags).SetValue(product, prefab);
            typeof(InventoryProduct).GetField("deliveryMode", flags).SetValue(product, InventoryProduct.DeliveryMode.AtSpawnPoint);
            typeof(InventoryProduct).GetField("spawnPoint", flags).SetValue(product, root.transform);
            typeof(InventoryProduct).GetField("visual", flags).SetValue(product, new[] { visual });
            typeof(InventoryManager).GetField("allContainers", flags).SetValue(manager, new[] { product });
            visual.SetActive(false);
            manager.RefreshVisuals();
            Assert.IsTrue(visual.activeSelf);
            Assert.IsTrue(product.TryGetPrompt(null, out var prompt));
            Assert.AreEqual("Take 1 Free Bag", prompt.Action);
            Assert.IsTrue(prompt.ShowKey);
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(product.TryInteract(null));
                var instance = GameObject.Find(prefab.name + "(Clone)");
                Assert.IsNotNull(instance);
                spawned.Add(instance);
                instance.name += i;
            }
            Assert.IsNull(SaveSystem.Data);
        }
        finally
        {
            SaveSystem.Data = previousData;
            foreach (var instance in spawned) Object.DestroyImmediate(instance);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(prefab);
        }
    }

    private static void BindInventoryManager(InventoryProduct product)
    {
        var manager = product.gameObject.AddComponent<InventoryManager>();
        typeof(InventoryManager).GetField("allContainers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(manager, new[] { product });
        manager.RefreshVisuals();
    }
}
