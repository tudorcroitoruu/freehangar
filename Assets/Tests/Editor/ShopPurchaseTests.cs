using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ShopPurchaseTests
{
    [Test]
    public void ThreeDeliverySlotsFillIndependentlyAndReuseVacatedSpace()
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Multiple delivery slots");
        root.transform.SetPositionAndRotation(new Vector3(20000, 300, 20000), Quaternion.Euler(0, 35, 0));
        var manager = root.AddComponent<InventoryManager>();
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prefab.transform.position = root.transform.position + Vector3.right * 20;
        try
        {
            SaveSystem.Data = new PlayerData { cash = 500 };
            var shop = root.AddComponent<ShopProduct>();
            ConfigureShop(shop, manager, prefab);
            SetShopField(shop, "additionalSpawnOffsets", new[] { Vector3.forward * 1.8f, Vector3.back * 1.8f });
            for (int i = 0; i < 3; i++) Assert.IsTrue(shop.TryInteract(null));
            Assert.AreEqual(3, manager.SpawnedProducts.Count);
            Assert.AreEqual(200, SaveSystem.Money);
            Assert.IsFalse(shop.TryInteract(null), "All three occupied slots must block another purchase.");
            Assert.AreEqual(200, SaveSystem.Money);
            var releasedPosition = manager.SpawnedProducts[1].transform.position;
            manager.SpawnedProducts[1].transform.position += Vector3.up * 10;
            Assert.IsTrue(shop.TryInteract(null));
            Assert.That(Vector3.Distance(releasedPosition, manager.SpawnedProducts[3].transform.position), Is.LessThan(.001f));
            Assert.AreEqual(100, SaveSystem.Money);
        }
        finally
        {
            DestroyDeliveredProducts(manager);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
            SaveSystem.Data = previousData;
        }
    }

    [Test]
    public void OccupiedSpawnAreaBlocksPaymentAndHolderReturnsAfterCargoLeaves()
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Shop spawn clearance test");
        root.transform.SetPositionAndRotation(new Vector3(20000, 300, 20000), Quaternion.Euler(0, 35, 0));
        var manager = root.AddComponent<InventoryManager>();
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prefab.transform.position = root.transform.position + Vector3.right * 20;
        var holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        holder.transform.SetParent(root.transform, false);
        holder.transform.localPosition = Vector3.up * .55f;
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.transform.SetParent(root.transform, false);
        blocker.transform.localPosition = Vector3.right * 10;
        try
        {
            SaveSystem.Data = new PlayerData { cash = 300 };
            var shop = root.AddComponent<ShopProduct>();
            ConfigureShop(shop, manager, prefab);
            SetShopField(shop, "objectSpawnHolder", holder);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var refresh = typeof(ShopProduct).GetMethod("RefreshSpawnHolder", flags);
            refresh.Invoke(shop, null);
            Assert.IsTrue(holder.activeSelf, "The marker's own collider must not block delivery.");

            blocker.transform.localPosition = Vector3.up * .55f;
            refresh.Invoke(shop, null);
            Assert.IsFalse(holder.activeSelf);
            Assert.IsTrue(shop.TryGetPrompt(null, out var blockedPrompt));
            Assert.IsTrue(blockedPrompt.ShowKey, "The buy action must remain reachable to show the occupied-area warning.");
            Assert.IsFalse(shop.TryInteract(null));
            Assert.AreEqual(300, SaveSystem.Money);
            Assert.AreEqual(0, manager.SpawnedProducts.Count);

            blocker.transform.localPosition = Vector3.right * 10;
            refresh.Invoke(shop, null);
            Assert.IsTrue(holder.activeSelf);
            Assert.IsTrue(shop.TryInteract(null));
            Assert.AreEqual(200, SaveSystem.Money);
            Assert.IsFalse(holder.activeSelf);
            refresh.Invoke(shop, null);
            Assert.IsFalse(holder.activeSelf, "Delivered cargo must keep the marker hidden.");
            Assert.IsFalse(shop.TryInteract(null), "A second purchase cannot overlap the first delivery.");
            Assert.AreEqual(200, SaveSystem.Money);
            Assert.AreEqual(1, manager.SpawnedProducts.Count);

            manager.SpawnedProducts[0].transform.position += Vector3.forward * 10;
            refresh.Invoke(shop, null);
            Assert.IsTrue(holder.activeSelf);
            Assert.IsTrue(shop.TryGetPrompt(null, out var clearPrompt));
            Assert.IsTrue(clearPrompt.ShowKey);
        }
        finally
        {
            DestroyDeliveredProducts(manager);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
            SaveSystem.Data = previousData;
        }
    }

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
            root.AddComponent<InventoryManager>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(InventoryProduct).GetField("unlimitedCount", flags).SetValue(product, true);
            typeof(InventoryProduct).GetField("productID", flags).SetValue(product, "freeBag");
            typeof(InventoryProduct).GetField("productName", flags).SetValue(product, "Free Bag");
            typeof(InventoryProduct).GetField("objectPrefab", flags).SetValue(product, prefab);
            typeof(InventoryProduct).GetField("deliveryMode", flags).SetValue(product, InventoryProduct.DeliveryMode.AtSpawnPoint);
            typeof(InventoryProduct).GetField("spawnPoint", flags).SetValue(product, root.transform);
            typeof(InventoryProduct).GetField("visual", flags).SetValue(product, new[] { visual });
            visual.SetActive(false);
            typeof(InventoryProduct).GetMethod("RefreshOwnedVisuals", flags).Invoke(product, null);
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
        product.gameObject.AddComponent<InventoryManager>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShopDeliversImmediatelyWithoutAddingPickupStock(bool toHands)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Direct shop delivery test");
        var prefab = new GameObject("Shop delivery prefab");
        var actor = new GameObject("Shop customer");
        var manager = root.AddComponent<InventoryManager>();
        PlayerItemCarry carrier = null;
        try
        {
            SaveSystem.Data = new PlayerData { cash = 100 };
            SaveSystem.Data.TryBuyProduct("crate", 0);
            var player = actor.AddComponent<PlayerController>();
            var camera = new GameObject("Camera");
            camera.transform.SetParent(actor.transform);
            player.characterCamera = camera.AddComponent<Camera>();
            carrier = actor.AddComponent<PlayerItemCarry>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerItemCarry).GetMethod("Awake", flags).Invoke(carrier, null);
            var interactor = actor.AddComponent<PlayerInteractor>();
            typeof(PlayerInteractor).GetMethod("Awake", flags).Invoke(interactor, null);
            var shop = root.AddComponent<ShopProduct>();
            ConfigureShop(shop, manager, prefab);
            SetShopField(shop, "deliveryMode", toHands ? ShopProduct.DeliveryMode.Hands : ShopProduct.DeliveryMode.AtSpawnPoint);
            root.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 60, 0));

            Assert.IsTrue(shop.TryGetPrompt(interactor, out var prompt));
            Assert.IsTrue(prompt.ShowKey);
            StringAssert.DoesNotContain("<color=", prompt.Action);
            Assert.IsTrue(shop.TryInteract(interactor));
            Assert.AreEqual(0, SaveSystem.Money);
            Assert.AreEqual(1, SaveSystem.Data.GetOwnedProductCount("crate"));
            Assert.AreEqual(1, manager.GetActiveCount("crate"));
            Assert.AreEqual(1, manager.SpawnedProducts.Count);
            Assert.AreEqual("crate", manager.SpawnedProducts[0].ProductID);
            var instance = manager.SpawnedProducts[0].gameObject;
            if (toHands) Assert.AreSame(instance, carrier.CarriedObject);
            else
            {
                Assert.IsFalse(carrier.IsCarrying);
                Assert.AreEqual(root.transform.position, instance.transform.position);
                Assert.That(Quaternion.Angle(prefab.transform.rotation, instance.transform.rotation), Is.LessThan(.01f));
            }
        }
        finally
        {
            if (carrier != null && carrier.IsCarrying) carrier.CancelTake(carrier.CarriedObject);
            DestroyDeliveredProducts(manager);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
            SaveSystem.Data = previousData;
        }
    }

    [TestCase("money")]
    [TestCase("prefab")]
    [TestCase("point")]
    [TestCase("limit")]
    [TestCase("hands")]
    public void BlockedShopPurchaseDoesNotChargeOrSpawn(string reason)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Blocked shop test");
        var prefab = new GameObject("Shop delivery prefab");
        var manager = root.AddComponent<InventoryManager>();
        try
        {
            SaveSystem.Data = new PlayerData { cash = reason == "money" ? 99 : 100 };
            var shop = root.AddComponent<ShopProduct>();
            ConfigureShop(shop, manager, prefab);
            if (reason == "prefab") SetShopField(shop, "objectPrefab", null);
            if (reason == "point") SetShopField(shop, "spawnPoint", null);
            if (reason == "hands") SetShopField(shop, "deliveryMode", ShopProduct.DeliveryMode.Hands);
            if (reason == "limit")
            {
                SetShopField(shop, "maxActiveInScene", 1);
                manager.RegisterInstance(new GameObject("Already delivered crate"), "crate", 1);
            }
            int cash = SaveSystem.Money;
            int count = manager.GetActiveCount("crate");
            Assert.IsTrue(shop.TryGetPrompt(null, out var prompt));
            Assert.IsTrue(prompt.ShowKey);
            Assert.IsFalse(prompt.RequiresFreeHands);
            if (reason == "money") StringAssert.Contains("Price: <color=#D92D2D>$100</color>", prompt.SecondaryDescription);
            Assert.IsFalse(shop.TryInteract(null));
            Assert.AreEqual(cash, SaveSystem.Money);
            Assert.AreEqual(0, SaveSystem.Data.GetOwnedProductCount("crate"));
            Assert.AreEqual(count, manager.GetActiveCount("crate"));
        }
        finally
        {
            DestroyDeliveredProducts(manager);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
            SaveSystem.Data = previousData;
        }
    }

    [Test]
    public void FailedDeliveryDoesNotCharge()
    {
        var previousData = SaveSystem.Data;
        try
        {
            SaveSystem.Data = new PlayerData { cash = 100 };
            Assert.IsFalse(SaveSystem.TryBuyProduct("crate", 100, () => false));
            Assert.AreEqual(100, SaveSystem.Money);
            Assert.IsEmpty(SaveSystem.Data.ownedProducts);
        }
        finally { SaveSystem.Data = previousData; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProductsResolveManagerEvenWhenTheOldDepotIsDisabled(bool inactiveDepot)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Delivery manager discovery test");
        var depot = new GameObject("Disabled depot");
        depot.transform.SetParent(root.transform);
        var manager = depot.AddComponent<InventoryManager>();
        manager.enabled = false;
        if (inactiveDepot) depot.SetActive(false);
        var prefab = new GameObject("Delivered product");
        var expectedManager = inactiveDepot ? Object.FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include) : manager;
        bool wasActive = expectedManager.gameObject.activeSelf;
        string id = "discovery." + System.Guid.NewGuid().ToString("N");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        string[] keys = { "/save", "/save_Settings", "levelsCompleted" };
        var previousPrefs = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++) previousPrefs[i] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
        try
        {
            SaveSystem.Data = new PlayerData { cash = 200 };
            var shopObject = new GameObject("Shop product");
            shopObject.transform.SetParent(inactiveDepot ? root.transform : depot.transform);
            var shop = shopObject.AddComponent<ShopProduct>();
            ConfigureShop(shop, null, prefab);
            SetShopField(shop, "productID", id);
            Assert.IsTrue(shop.TryInteract(null));
            Assert.AreSame(expectedManager, typeof(ShopProduct).GetField("inventoryManager", flags).GetValue(shop));
            Assert.AreEqual(100, SaveSystem.Money);
            Assert.AreEqual(0, SaveSystem.Data.GetOwnedProductCount(id));

            var inventoryObject = new GameObject("Free inventory product");
            inventoryObject.transform.SetParent(shopObject.transform.parent);
            var inventory = inventoryObject.AddComponent<InventoryProduct>();
            typeof(InventoryProduct).GetField("productID", flags).SetValue(inventory, id);
            typeof(InventoryProduct).GetField("unlimitedCount", flags).SetValue(inventory, true);
            typeof(InventoryProduct).GetField("objectPrefab", flags).SetValue(inventory, prefab);
            typeof(InventoryProduct).GetField("deliveryMode", flags).SetValue(inventory, InventoryProduct.DeliveryMode.AtSpawnPoint);
            typeof(InventoryProduct).GetField("spawnPoint", flags).SetValue(inventory, root.transform);
            Assert.IsTrue(inventory.TryGetPrompt(null, out var prompt));
            Assert.IsTrue(prompt.ShowKey);
            Assert.IsTrue(inventory.TryInteract(null));
            Assert.AreSame(expectedManager, typeof(InventoryProduct).GetField("inventoryManager", flags).GetValue(inventory));
            Assert.AreEqual(2, expectedManager.GetActiveCount(id));
            Assert.AreEqual(wasActive, expectedManager.gameObject.activeSelf);
            Assert.AreEqual(!inactiveDepot, depot.activeSelf);
        }
        finally
        {
            for (int i = expectedManager.SpawnedProducts.Count - 1; i >= 0; i--)
            {
                var item = expectedManager.SpawnedProducts[i];
                if (item == null || item.ProductID != id) continue;
                expectedManager.UnregisterInstance(item);
                Object.DestroyImmediate(item.gameObject);
            }
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(prefab);
            SaveSystem.Data = previousData;
            for (int i = 0; i < keys.Length; i++)
                if (previousPrefs[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previousPrefs[i]);
            PlayerPrefs.Save();
        }
    }

    private static void ConfigureShop(ShopProduct shop, InventoryManager manager, GameObject prefab)
    {
        SetShopField(shop, "productID", "crate");
        SetShopField(shop, "productPrice", 100);
        SetShopField(shop, "objectPrefab", prefab);
        SetShopField(shop, "inventoryManager", manager);
        SetShopField(shop, "deliveryMode", ShopProduct.DeliveryMode.AtSpawnPoint);
        SetShopField(shop, "spawnPoint", shop.transform);
    }

    private static void SetShopField(ShopProduct shop, string field, object value) =>
        typeof(ShopProduct).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(shop, value);

    private static void DestroyDeliveredProducts(InventoryManager manager)
    {
        while (manager.SpawnedProducts.Count > 0)
        {
            var item = manager.SpawnedProducts[0];
            manager.UnregisterInstance(item);
            Object.DestroyImmediate(item.gameObject);
        }
    }
}
