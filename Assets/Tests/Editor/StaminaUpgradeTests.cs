using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class StaminaUpgradeTests
{
    private PlayerData previousData;
    private GameObject root;
    private StaminaUpgradeSettings settings;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData();
        root = new GameObject("Stamina upgrade test");
        settings = ScriptableObject.CreateInstance<StaminaUpgradeSettings>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
        SaveSystem.Data = previousData;
    }

    [Test]
    public void SavedUpgradeIncreasesActualCapacityAndStillAppliesHunger()
    {
        SaveSystem.Data.staminaUpgradeLevel = 2;
        var stamina = root.AddComponent<StaminaManager>();
        SetField(stamina, "upgradeSettings", settings);
        typeof(StaminaManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stamina, null);
        Assert.AreEqual(150f, stamina.MaxStamina);
        Assert.AreEqual(150f, stamina.CurrentStamina);
        stamina.SetHungerPercent(20f);
        Assert.AreEqual(120f, stamina.CurrentMaxStamina);
        Assert.AreEqual(120f, stamina.CurrentStamina);
        var data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(SaveSystem.Data));
        Assert.AreEqual(2, data.staminaUpgradeLevel);
        Assert.AreEqual(0, JsonUtility.FromJson<PlayerData>("{\"cash\":100}").staminaUpgradeLevel);
    }

    [Test]
    public void PurchasesRejectInsufficientMoneyStaleLevelsNegativePricesAndMaximum()
    {
        var data = SaveSystem.Data;
        data.cash = 100;
        Assert.IsFalse(data.TryUpgradeStamina(101, 0, 4));
        Assert.IsTrue(data.TryUpgradeStamina(100, 0, 4));
        Assert.AreEqual(0, data.cash);
        Assert.AreEqual(1, data.staminaUpgradeLevel);
        Assert.IsFalse(data.TryUpgradeStamina(0, 0, 4));
        Assert.IsFalse(data.TryUpgradeStamina(-1, 1, 4));
        Assert.IsTrue(data.TryUpgradeStamina(0, 1, 4));
        data.staminaUpgradeLevel = 4;
        Assert.IsFalse(data.TryUpgradeStamina(0, 4, 4));
    }

    [Test]
    public void UIShowsShortfallFreeAndMaximumAndRestoresAffordableButtonAppearance()
    {
        var screen = root.AddComponent<UpgradeStamina>();
        SetField(screen, "settings", settings);
        var price = Child("Price").AddComponent<Text>();
        var shortage = Child("Missing money").AddComponent<Text>();
        var comparison = Child("Stamina comparison").AddComponent<Text>();
        var popup = Child("Popup");
        var button = Child("Button").AddComponent<Button>();
        var canvas = Child("Canvas").AddComponent<CanvasGroup>();
        var image = Child("Button image").AddComponent<Image>();
        var shared = new Material(Shader.Find("UI/Image Margin Fade"));
        image.material = shared;
        SetField(screen, "price", price);
        SetField(screen, "notEnoughMoney", shortage);
        SetField(screen, "staminaModification", comparison);
        SetField(screen, "notEnoughMoneyPopup", popup);
        SetField(screen, "upgradeButton", button);
        SetField(screen, "upgradeButtonCanvas", canvas);
        SetField(screen, "upgradeButtonImage", image);
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
            StringAssert.Contains("100  \u279c  <color=#", comparison.text);
            settings.prices[0] = 0;
            SaveSystem.Data.cash = 0;
            screen.Refresh();
            Assert.AreEqual("Free", price.text);
            Assert.IsFalse(popup.activeSelf);
            Assert.IsTrue(button.interactable);
            Assert.AreEqual(1f, canvas.alpha);
            Assert.AreEqual(1f, image.material.GetFloat("_Saturation"));
            SaveSystem.Data.staminaUpgradeLevel = settings.MaxUpgrades;
            screen.Refresh();
            Assert.AreEqual("MAX", price.text);
            Assert.IsFalse(button.interactable);
            Assert.IsFalse(popup.activeSelf);
            Assert.IsFalse(screen.TryUpgrade());
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(shared);
        }
    }

    [Test]
    public void SharedSettingsAssetLoadsForRuntimeAndScreen()
    {
        var shared = Resources.Load<StaminaUpgradeSettings>(StaminaUpgradeSettings.ResourcePath);
        Assert.IsNotNull(shared);
        Assert.IsTrue(shared.IsConfigured);
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform);
        return child;
    }
    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
