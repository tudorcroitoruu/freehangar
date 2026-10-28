using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShovelUpgradeInteractionTests
{
    private GameObject root, actor, station;
    private UpgradeManager manager;
    private UpgradeShovel screen;
    private ShovelUpgradeSettings settings;
    private PlayerData previousData;
    private SettingsData previousSettings;
    private ActivityData previousActivity;
    private readonly string[] keys = { "/save", "/save_Settings", "levelsCompleted" };
    private string[] previousPrefs;
    private int previousClosedFrame;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        previousSettings = SaveSystem.settingsData;
        previousActivity = SaveSystem.activityData;
        SaveSystem.Data = new PlayerData { cash = 100 };
        SaveSystem.settingsData = new SettingsData();
        SaveSystem.activityData = new ActivityData();
        previousPrefs = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            previousPrefs[i] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
        previousClosedFrame = (int)typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, -1);
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        root = new GameObject("Upgrade menu");
        root.SetActive(false);
        manager = root.AddComponent<UpgradeManager>();
        var panel = new GameObject("Shovel panel");
        panel.transform.SetParent(root.transform);
        screen = panel.AddComponent<UpgradeShovel>();
        settings = ScriptableObject.CreateInstance<ShovelUpgradeSettings>();
        settings.shovels = new[] { new ShovelUpgradeSettings.ShovelType() };
        SetField(screen, "settings", settings);
        SetField(manager, "upgradeShovelScreen", screen);
        actor = new GameObject("Actor");
        actor.AddComponent<PlayerInteractor>();
        station = new GameObject("Workshop shovel");
        var interactable = station.AddComponent<ShovelUpgradeInteractable>();
        SetField(interactable, "upgradeManager", manager);
    }

    [TearDown]
    public void TearDown()
    {
        manager.Close();
        Object.DestroyImmediate(station);
        Object.DestroyImmediate(actor);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(settings);
        SaveSystem.Data = previousData;
        SaveSystem.settingsData = previousSettings;
        SaveSystem.activityData = previousActivity;
        for (int i = 0; i < keys.Length; i++)
            if (previousPrefs[i] == null) PlayerPrefs.DeleteKey(keys[i]);
            else PlayerPrefs.SetString(keys[i], previousPrefs[i]);
        PlayerPrefs.Save();
        typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousClosedFrame);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    [Test]
    public void DisplayLoadsSavedTypeAndSwitchesImmediatelyWhenPurchaseUnlocksNextShovel()
    {
        var interactable = station.GetComponent<ShovelUpgradeInteractable>();
        settings.shovels = new[]
        {
            new ShovelUpgradeSettings.ShovelType(),
            new ShovelUpgradeSettings.ShovelType { capacityKg = 10.5f, weightKg = .8f }
        };
        var first = new GameObject("First shovel");
        first.transform.SetParent(station.transform);
        var second = new GameObject("Second shovel");
        second.transform.SetParent(station.transform);
        SetField(interactable, "settings", settings);
        SetField(interactable, "shovelModels", new[] { first, second });
        SaveSystem.Data.shovelUpgradeLevel = 3;
        SaveSystem.Data.cash = 400;
        typeof(ShovelUpgradeInteractable).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactable, null);
        try
        {
            Assert.IsTrue(first.activeSelf);
            Assert.IsFalse(second.activeSelf);
            Assert.IsTrue(SaveSystem.TryPurchaseShovelUpgrade(400, 3, settings.MaxUpgrades));
            Assert.IsFalse(first.activeSelf);
            Assert.IsTrue(second.activeSelf);
            Assert.IsTrue(station.activeSelf);
            SaveSystem.Data.shovelUpgradeLevel = settings.MaxUpgrades;
            interactable.RefreshDisplay();
            Assert.IsFalse(first.activeSelf);
            Assert.IsTrue(second.activeSelf);
            SaveSystem.Data = new PlayerData();
            interactable.RefreshDisplay();
            Assert.IsTrue(first.activeSelf);
            Assert.IsFalse(second.activeSelf);
        }
        finally
        {
            typeof(ShovelUpgradeInteractable).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactable, null);
        }
    }

    [Test]
    public void WorkshopShovelOpensInactiveMenuWithoutPurchasingOnTheOpeningEPress()
    {
        var interactable = station.GetComponent<ShovelUpgradeInteractable>();
        var interactor = actor.GetComponent<PlayerInteractor>();
        Assert.IsTrue(interactable.TryGetPrompt(interactor, out var prompt));
        Assert.IsTrue(prompt.ShowKey);
        Assert.IsTrue(interactable.TryInteract(interactor));
        Assert.IsTrue(manager.IsOpen);
        Assert.IsTrue(screen.isActiveAndEnabled);
        Assert.IsTrue(UpgradeManager.BlocksGameplayInput);
        Assert.IsFalse(interactable.TryGetPrompt(interactor, out _));
        Press(false, true);
        Assert.AreEqual(100, SaveSystem.Money);
        Assert.AreEqual(0, SaveSystem.Data.shovelUpgradeLevel);
    }

    [Test]
    public void SubsequentEPressBuysOneUpgradeAndInsufficientMoneyDoesNothing()
    {
        Assert.IsTrue(manager.OpenShovel());
        SetField(manager, "openedFrame", Time.frameCount - 1);
        Press(false, true);
        Assert.AreEqual(1, SaveSystem.Data.shovelUpgradeLevel);
        Assert.AreEqual(0, SaveSystem.Money);
        Press(false, true);
        Assert.AreEqual(1, SaveSystem.Data.shovelUpgradeLevel);
        Assert.AreEqual(0, SaveSystem.Money);
        Assert.IsTrue(manager.IsOpen);
    }

    [Test]
    public void EscapeClosesMenuBeforeProcessingAnyPurchaseAndConsumesTheGameplayFrame()
    {
        Assert.IsTrue(manager.OpenShovel());
        SetField(manager, "openedFrame", Time.frameCount - 1);
        Press(true, true);
        Assert.IsFalse(manager.IsOpen);
        Assert.IsFalse(root.activeSelf);
        Assert.IsFalse(screen.isActiveAndEnabled);
        Assert.AreEqual(0, SaveSystem.Data.shovelUpgradeLevel);
        Assert.AreEqual(100, SaveSystem.Money);
        Assert.IsTrue(UpgradeManager.BlocksGameplayInput);
    }

    private void Press(bool escape, bool upgrade) =>
        typeof(UpgradeManager).GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { escape, upgrade });
    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
