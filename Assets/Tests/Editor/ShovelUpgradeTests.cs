using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class ShovelUpgradeTests
{
    private ShovelUpgradeSettings settings;
    private PlayerData previousData;
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData { cash = 100 };
        settings = ScriptableObject.CreateInstance<ShovelUpgradeSettings>();
        settings.shovels = new[]
        {
            new ShovelUpgradeSettings.ShovelType(),
            new ShovelUpgradeSettings.ShovelType { capacityKg = 10.5f, weightKg = .8f }
        };
        root = new GameObject("Shovel upgrade test");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
        SaveSystem.Data = previousData;
    }

    [TestCase(0, 0, 0, 6.5f, 1f)]
    [TestCase(1, 0, 1, 7.5f, .95f)]
    [TestCase(3, 0, 3, 9.5f, .85f)]
    [TestCase(4, 1, 0, 10.5f, .8f)]
    [TestCase(7, 1, 3, 13.5f, .65f)]
    [TestCase(8, 1, 4, 14.5f, .6f)]
    public void FourPurchasesAdvanceShovelTypeAndFinalTypeKeepsFourDots(
        int level, int type, int dots, float capacity, float weight)
    {
        Assert.IsTrue(settings.IsConfigured);
        Assert.AreEqual(type, settings.TypeIndex(level));
        Assert.AreEqual(dots, settings.FilledDots(level));
        Assert.That(settings.Capacity(level), Is.EqualTo(capacity).Within(.001f));
        Assert.That(settings.Weight(level), Is.EqualTo(weight).Within(.001f));
        Assert.AreEqual(level == 8 ? 0 : settings.shovels[type].prices[dots], settings.Price(level));
    }

    [Test]
    public void SharedRuntimeSettingsAssetHasACompleteUpgradeConfiguration()
    {
        var shared = Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
        Assert.IsNotNull(shared);
        Assert.IsTrue(shared.IsConfigured);
    }

    [Test]
    public void ZeroPriceShowsFreeAndAllowsUpgradeWithNoMoney()
    {
        settings.shovels[0].prices[0] = 0;
        SaveSystem.Data.cash = 0;
        Assert.IsTrue(settings.IsConfigured);
        Assert.AreEqual(0, settings.Price(0));
        var screen = root.AddComponent<UpgradeShovel>();
        SetField(screen, "settings", settings);
        var price = Child("Price").AddComponent<Text>();
        var button = Child("Upgrade").AddComponent<Button>();
        var popup = Child("Shortage");
        SetField(screen, "price", price);
        SetField(screen, "upgradeButton", button);
        SetField(screen, "notEnoughMoneyPopup", popup);
        screen.Refresh();
        Assert.AreEqual("Free", price.text);
        Assert.IsTrue(button.interactable);
        Assert.IsFalse(popup.activeSelf);
        Assert.IsTrue(SaveSystem.Data.TryUpgradeShovel(0, 0, settings.MaxUpgrades));
        Assert.AreEqual(1, SaveSystem.Data.shovelUpgradeLevel);
        Assert.AreEqual(0, SaveSystem.Money);
        SaveSystem.Data.shovelUpgradeLevel = settings.MaxUpgrades;
        Assert.IsFalse(screen.TryUpgrade());
        Assert.AreEqual(settings.MaxUpgrades, SaveSystem.Data.shovelUpgradeLevel);
    }

    [Test]
    public void PurchaseChargesExactlyOnceAndRejectsStaleOrUnaffordableRequests()
    {
        var data = SaveSystem.Data;
        Assert.IsTrue(data.TryUpgradeShovel(100, 0, settings.MaxUpgrades));
        Assert.AreEqual(0, data.cash);
        Assert.AreEqual(1, data.shovelUpgradeLevel);
        data.cash = 500;
        Assert.IsFalse(data.TryUpgradeShovel(100, 0, settings.MaxUpgrades));
        Assert.IsFalse(data.TryUpgradeShovel(501, 1, settings.MaxUpgrades));
        Assert.IsFalse(data.TryUpgradeShovel(-1, 1, settings.MaxUpgrades));
        Assert.AreEqual(500, data.cash);
        Assert.AreEqual(1, data.shovelUpgradeLevel);
        data.shovelUpgradeLevel = settings.MaxUpgrades;
        Assert.IsFalse(data.TryUpgradeShovel(100, settings.MaxUpgrades, settings.MaxUpgrades));
    }

    [Test]
    public void UpgradeProgressSurvivesSaveSerializationAndOldSavesStartAtZero()
    {
        var saved = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(new PlayerData { shovelUpgradeLevel = 5 }));
        Assert.AreEqual(5, saved.shovelUpgradeLevel);
        Assert.AreEqual(0, JsonUtility.FromJson<PlayerData>("{\"cash\":100}").shovelUpgradeLevel);
    }

    [Test]
    public void ToolReadsSavedCapacityAndWeightEvenWithoutAnOpenUpgradeScreen()
    {
        var shovel = root.AddComponent<ShovelTool>();
        SetField(shovel, "upgradeSettings", settings);
        shovel.Cargo.Add(GrainType.Corn, 2f);
        SaveSystem.Data.shovelUpgradeLevel = 5;
        Assert.That(shovel.CapacityKg, Is.EqualTo(11.5f).Within(.001f));
        Assert.That(shovel.WeightKg, Is.EqualTo(.75f).Within(.001f));
        Assert.AreEqual(2f, shovel.Kilograms);
    }

    [TestCase(.5f, 95f)]
    [TestCase(1f, 90f)]
    [TestCase(2f, 80f)]
    public void ShovelWeightScalesStaminaConsumption(float weight, float remaining)
    {
        var stamina = root.AddComponent<StaminaManager>();
        typeof(StaminaManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stamina, null);
        stamina.ConsumeShovelStamina(weight);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(remaining).Within(.001f));
    }

    [Test]
    public void ScreenTracksMissingMoneyDotsTypeAndMaximumWithoutChangingSharedMaterial()
    {
        var screen = root.AddComponent<UpgradeShovel>();
        SetField(screen, "settings", settings);
        var button = Child("Button").AddComponent<Button>();
        var canvas = Child("Button Canvas").AddComponent<CanvasGroup>();
        var image = Child("Button Image").AddComponent<Image>();
        var shared = new Material(Shader.Find("UI/Image Margin Fade"));
        image.material = shared;
        var shortage = Child("Shortage").AddComponent<Text>();
        var popup = Child("Popup");
        var label = Child("Level").AddComponent<Text>();
        var capacity = Child("Capacity").AddComponent<Text>();
        var dots = new[] { Child("Dot 1"), Child("Dot 2"), Child("Dot 3"), Child("Dot 4") };
        SetField(screen, "upgradeButton", button);
        SetField(screen, "upgradeButtonCanvas", canvas);
        SetField(screen, "upgradeButtonImage", image);
        SetField(screen, "notEnoughMoney", shortage);
        SetField(screen, "notEnoughMoneyPopup", popup);
        SetField(screen, "currentLevelText", label);
        SetField(screen, "scopeModification", capacity);
        SetField(screen, "upgradeDots", dots);
        try
        {
            SaveSystem.Data.cash = 40;
            screen.Refresh();
            Assert.AreEqual("You need $60 more.", shortage.text);
            Assert.IsTrue(popup.activeSelf);
            Assert.IsFalse(button.interactable);
            Assert.AreEqual(.5f, canvas.alpha);
            Assert.AreEqual(.5f, image.material.GetFloat("_Saturation"));
            Assert.AreEqual(1f, shared.GetFloat("_Saturation"));
            StringAssert.Contains("6.5  ➜  <color=#", capacity.text);

            SaveSystem.Data.cash = 100;
            screen.Refresh();
            Assert.IsTrue(button.interactable);
            Assert.IsFalse(popup.activeSelf);
            Assert.AreEqual(1f, canvas.alpha);
            Assert.AreEqual(1f, image.material.GetFloat("_Saturation"));

            SaveSystem.Data.shovelUpgradeLevel = 3;
            screen.Refresh();
            Assert.IsTrue(dots[2].activeSelf);
            Assert.IsFalse(dots[3].activeSelf);
            SaveSystem.Data.shovelUpgradeLevel = 4;
            screen.Refresh();
            Assert.AreEqual("Level 2", label.text);
            foreach (var dot in dots) Assert.IsFalse(dot.activeSelf);
            SaveSystem.Data.shovelUpgradeLevel = settings.MaxUpgrades;
            screen.Refresh();
            foreach (var dot in dots) Assert.IsTrue(dot.activeSelf);
            Assert.IsFalse(button.interactable);
            Assert.IsFalse(popup.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(shared);
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
