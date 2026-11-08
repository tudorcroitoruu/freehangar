using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class PlayerPerksTests
{
    [TestCase("farm.key.iron", "farm.key.iron.bonus.2", true)]
    [TestCase("farm.key.iron.bonus.4", "farm.key.iron", true)]
    [TestCase("farm.key.iron.bonus.2", "farm.key.iron.bonus.4", true)]
    [TestCase("farm.key.copper.bonus.1", "farm.key.copper.bonus.3", true)]
    [TestCase("farm.key.copper", "farm.key.iron", false)]
    [TestCase("farm.hangar.key.3", "farm.key.iron", false)]
    [TestCase(null, "farm.key.iron", false)]
    public void ChestKeysAreUniversalWithinTheirMaterial(string owned, string required, bool expected)
    {
        var data = new PlayerData { foundSecrets = new[] { owned } };
        Assert.AreEqual(expected, BuriedFind.HasChestKey(data, required));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void VoucherPaysForOnlyOneSuccessfulToolUpgrade(bool shovel)
    {
        var data = new PlayerData { cash = 500 };
        Assert.IsTrue(PlayerPerks.Grant(data, ChestReward.FreeUpgrade, 0, 0, 0));
        Assert.IsFalse(shovel ? data.TryUpgradeShovel(-1, 0, 4) : data.TryUpgradeStamina(-1, 0, 4));
        Assert.AreEqual(1, data.freeUpgradeVouchers);
        Assert.IsTrue(shovel ? data.TryUpgradeShovel(100, 0, 4) : data.TryUpgradeStamina(100, 0, 4));
        Assert.AreEqual(500, data.cash);
        Assert.AreEqual(0, data.freeUpgradeVouchers);
        Assert.IsFalse(shovel ? data.TryUpgradeShovel(100, 0, 4) : data.TryUpgradeStamina(100, 0, 4));
        Assert.IsTrue(shovel ? data.TryUpgradeShovel(200, 1, 4) : data.TryUpgradeStamina(200, 1, 4));
        Assert.AreEqual(300, data.cash);
        var restored = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.AreEqual(0, restored.freeUpgradeVouchers);
        Assert.AreEqual(2, shovel ? restored.shovelUpgradeLevel : restored.staminaUpgradeLevel);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void VoucherMakesUpgradeScreenFreeWithoutRequiringCash(bool shovel)
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Voucher price test");
        ScriptableObject settings = null;
        try
        {
            SaveSystem.Data = new PlayerData { freeUpgradeVouchers = 1 };
            MonoBehaviour screen;
            if (shovel)
            {
                var configuration = ScriptableObject.CreateInstance<ShovelUpgradeSettings>();
                configuration.shovels = new[] { new ShovelUpgradeSettings.ShovelType() };
                settings = configuration;
                screen = root.AddComponent<UpgradeShovel>();
            }
            else
            {
                settings = ScriptableObject.CreateInstance<StaminaUpgradeSettings>();
                screen = root.AddComponent<UpgradeStamina>();
            }
            var label = new GameObject("Price", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var buttonRoot = new GameObject("Upgrade", typeof(RectTransform), typeof(Button));
            buttonRoot.transform.SetParent(root.transform, false);
            var button = buttonRoot.GetComponent<Button>();
            SetPrivate(screen, "settings", settings);
            SetPrivate(screen, "price", label.GetComponent<Text>());
            SetPrivate(screen, "upgradeButton", button);
            screen.GetType().GetMethod("Refresh").Invoke(screen, null);
            Assert.AreEqual("Free", label.GetComponent<Text>().text);
            Assert.IsTrue(button.interactable);
            SaveSystem.Data.freeUpgradeVouchers = 0;
            screen.GetType().GetMethod("Refresh").Invoke(screen, null);
            StringAssert.StartsWith("$ ", label.GetComponent<Text>().text);
            Assert.IsFalse(button.interactable);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(settings);
            SaveSystem.Data = previousData;
        }
    }

    [Test]
    public void UpgradePurchaseBlocksReentryAndRapidPurchasesAcrossScreens()
    {
        var previousData = SaveSystem.Data;
        string[] keys = { "/save", "/save_Settings", "levelsCompleted" };
        var values = new string[keys.Length];
        var existed = new bool[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            existed[i] = PlayerPrefs.HasKey(keys[i]);
            values[i] = PlayerPrefs.GetString(keys[i]);
        }
        bool reentered = false;
        System.Action<int> onMoneyChanged = _ => reentered |= SaveSystem.TryPurchaseStaminaUpgrade(100, 0, 4);
        try
        {
            SaveSystem.Data = new PlayerData { cash = 500, freeUpgradeVouchers = 1 };
            SaveSystem.MoneyChanged += onMoneyChanged;
            Assert.IsTrue(SaveSystem.TryPurchaseShovelUpgrade(100, 0, 4));
            Assert.IsFalse(reentered);
            Assert.IsTrue(SaveSystem.UpgradePurchaseLocked);
            Assert.IsFalse(SaveSystem.TryPurchaseShovelUpgrade(200, 1, 4));
            Assert.IsFalse(SaveSystem.TryPurchaseStaminaUpgrade(100, 0, 4));
            Assert.AreEqual(1, SaveSystem.Data.shovelUpgradeLevel);
            Assert.AreEqual(0, SaveSystem.Data.staminaUpgradeLevel);
            Assert.AreEqual(0, SaveSystem.Data.freeUpgradeVouchers);
            Assert.AreEqual(500, SaveSystem.Money);
        }
        finally
        {
            SaveSystem.MoneyChanged -= onMoneyChanged;
            SaveSystem.Data = previousData;
            for (int i = 0; i < keys.Length; i++)
                if (existed[i]) PlayerPrefs.SetString(keys[i], values[i]);
                else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
        }
    }

    [TestCase(.1f, ChestReward.Cash)]
    [TestCase(.5f, ChestReward.FreeUpgrade)]
    [TestCase(.65f, ChestReward.NoAttacks)]
    [TestCase(.75f, ChestReward.Mobility)]
    [TestCase(.85f, ChestReward.InfiniteStamina)]
    [TestCase(.95f, ChestReward.DoubleSellCash)]
    public void PoolIncludesEveryReward(float roll, ChestReward expected)
    {
        Assert.That(PlayerPerks.Roll(roll), Is.EqualTo(expected));
    }
    [Test] public void CashAndVoucherRewardsChangeOnlyTheirOwnBalance()
    {
        var data = new PlayerData { cash = 20 };
        Assert.IsTrue(PlayerPerks.Grant(data, ChestReward.Cash, 500, 300, 1000));
        Assert.That(data.cash, Is.EqualTo(520));
        Assert.IsTrue(PlayerPerks.Grant(data, ChestReward.FreeUpgrade, 500, 300, 1000));
        Assert.That(data.freeUpgradeVouchers, Is.EqualTo(1));
        Assert.That(data.cash, Is.EqualTo(520));
    }
    [TestCase(ChestReward.NoAttacks)]
    [TestCase(ChestReward.Mobility)]
    [TestCase(ChestReward.InfiniteStamina)]
    [TestCase(ChestReward.DoubleSellCash)]
    public void TimedRewardsPersistAndExtendWithoutChangingStrength(ChestReward reward)
    {
        var data = new PlayerData();
        Assert.IsTrue(PlayerPerks.Grant(data, reward, 500, 300, 1000));
        Assert.That(Expiry(data, reward), Is.EqualTo(1300));
        data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.That(Expiry(data, reward), Is.EqualTo(1300));
        PlayerPerks.Grant(data, reward, 500, 300, 1050);
        Assert.That(Expiry(data, reward), Is.EqualTo(1600));
        Assert.IsTrue(PlayerPerks.Active(1600, 1599));
        Assert.IsFalse(PlayerPerks.Active(1600, 1600));
    }
    [Test] public void DoubleSalesPayExactlyTwiceTheDisplayedRoundedBaseValue()
    {
        Assert.That(PlayerPerks.SellCash(1.6, 2), Is.EqualTo(2 * PlayerPerks.SellCash(1.6, 1)));
        Assert.That(PlayerPerks.SellCash(int.MaxValue, 2), Is.EqualTo(int.MaxValue));
    }
    [Test] public void LegacySavesStartWithoutPerks()
    {
        var data = JsonUtility.FromJson<PlayerData>("{\"cash\":100}");
        Assert.That(data.noAttacksUntilUtc, Is.Zero);
        Assert.That(data.mobilityUntilUtc, Is.Zero);
        Assert.That(data.doubleSellCashUntilUtc, Is.Zero);
        Assert.IsFalse(PlayerPerks.Active(0, 1000));
    }
    private static long Expiry(PlayerData data, ChestReward reward)
    {
        switch (reward)
        {
            case ChestReward.NoAttacks: return data.noAttacksUntilUtc;
            case ChestReward.Mobility: return data.mobilityUntilUtc;
            case ChestReward.InfiniteStamina: return data.infiniteStaminaUntilUtc;
            default: return data.doubleSellCashUntilUtc;
        }
    }

    [Test]
    public void BoosterIndicatorUsesConfiguredIconAndPrivateOutlineCountdown()
    {
        var root = new GameObject("Booster UI test", typeof(RectTransform), typeof(Image));
        var iconRoot = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconRoot.transform.SetParent(root.transform, false);
        var shared = new Material(Shader.Find("UI/Image Margin Fade"));
        shared.SetFloat("_OutlineFillAmount", .4f);
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
        try
        {
            var background = root.GetComponent<Image>();
            background.material = shared;
            var icon = iconRoot.GetComponent<Image>();
            var handler = root.AddComponent<BoosterStatusHandler>();
            SetPrivate(handler, "boosterBg", background);
            SetPrivate(handler, "boosterIcon", icon);
            SetPrivate(handler, "settings", new[] { new BoosterStatusSettings
            {
                kind = BuriedFind.Kind.PerkBottle, boosterReward = ChestReward.Mobility,
                icon = sprite, outlineTimerColor = Color.cyan
            } });
            handler.Initialize(ChestReward.Mobility, PlayerPerks.Now + 300);
            var runtime = background.material;
            Assert.AreNotSame(shared, runtime);
            Assert.AreSame(sprite, icon.sprite);
            Assert.AreEqual(Color.cyan, runtime.GetColor("_OutlineColor"));
            Assert.That(runtime.GetFloat("_OutlineFillAmount"), Is.GreaterThan(.99f));
            Assert.AreEqual(4f, runtime.GetFloat("_OutlineFillDirection"));
            Assert.AreEqual(.4f, shared.GetFloat("_OutlineFillAmount"));

            handler.SetExpiry(PlayerPerks.Now - 1);
            InvokePrivate(handler, "UpdateTimer");
            Assert.AreEqual(0f, runtime.GetFloat("_OutlineFillAmount"));
            handler.SetExpiry(PlayerPerks.Now + 600);
            InvokePrivate(handler, "UpdateTimer");
            Assert.That(runtime.GetFloat("_OutlineFillAmount"), Is.GreaterThan(.99f));
            // Edit-mode components without Awake do not receive runtime teardown.
            InvokePrivate(handler, "OnDestroy");
            Object.DestroyImmediate(handler);
            Assert.AreSame(shared, background.material);
            Assert.IsTrue(runtime == null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(shared);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ToolHintsRestoreDiscoveriesAndSelectTheEquippedTool()
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Tool hints test");
        try
        {
            SaveSystem.Data = new PlayerData();
            var hud = root.AddComponent<GameplayCanvas>();
            var groupRoot = new GameObject("Hints", typeof(CanvasGroup));
            groupRoot.transform.SetParent(root.transform, false);
            var shovel = NewHint(groupRoot.transform, "Shovel");
            var broom = NewHint(groupRoot.transform, "Broom");
            var axe = NewHint(groupRoot.transform, "Axe");
            var hammer = NewHint(groupRoot.transform, "Hammer");
            SetPrivate(hud, "hintsParent", groupRoot.GetComponent<CanvasGroup>());
            SetPrivate(hud, "shovelEquipped", shovel);
            SetPrivate(hud, "broomEquipped", broom);
            SetPrivate(hud, "axeEquipped", axe);
            SetPrivate(hud, "hammerEquipped", hammer);
            SetPrivate(hud, "displayedTools", -1);
            InvokePrivate(hud, "RefreshCollectibleUI");
            Assert.IsTrue(shovel.gameObject.activeSelf);
            Assert.IsFalse(groupRoot.activeSelf);
            Assert.IsFalse(broom.gameObject.activeSelf);
            Assert.IsFalse(axe.gameObject.activeSelf);
            Assert.IsFalse(hammer.gameObject.activeSelf);
            Assert.IsTrue((bool)GetPrivate(shovel, "isSelected"));

            SaveSystem.Data.foundSecrets = new[] { PlayerCombatTools.AxeFindId };
            var tools = root.AddComponent<PlayerCombatTools>();
            SetPrivate(tools, "equipped", PlayerCombatTools.Tool.Axe);
            SetPrivate(hud, "playerTools", tools);
            InvokePrivate(hud, "RefreshCollectibleUI");
            Assert.IsTrue(axe.gameObject.activeSelf);
            Assert.IsTrue(groupRoot.activeSelf);
            Assert.IsFalse((bool)GetPrivate(shovel, "isSelected"));
            Assert.IsTrue((bool)GetPrivate(axe, "isSelected"));
            Assert.That(groupRoot.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); SaveSystem.Data = previousData; }
    }

    [Test]
    public void SavedBoostersRestoreOnceExtendWithoutDuplicatesAndDisappearOnExpiry()
    {
        var previousData = SaveSystem.Data;
        var root = new GameObject("Saved booster HUD test");
        try
        {
            SaveSystem.Data = new PlayerData { infiniteStaminaUntilUtc = PlayerPerks.Now + 300 };
            var hud = root.AddComponent<GameplayCanvas>();
            var parent = new GameObject("Boosters").transform;
            parent.SetParent(root.transform, false);
            var source = new GameObject("Booster prefab");
            source.transform.SetParent(root.transform, false);
            var prefab = source.AddComponent<BoosterStatusHandler>();
            SetPrivate(prefab, "settings", new[] { new BoosterStatusSettings
            {
                kind = BuriedFind.Kind.PerkBottle, boosterReward = ChestReward.InfiniteStamina
            } });
            SetPrivate(hud, "boostersParent", parent);
            SetPrivate(hud, "boosterHandlerPrefab", prefab);
            InvokePrivate(hud, "RefreshCollectibleUI");
            Assert.AreEqual(1, parent.childCount);
            var indicator = parent.GetChild(0);
            InvokePrivate(hud, "RefreshCollectibleUI");
            SaveSystem.Data.infiniteStaminaUntilUtc += 300;
            InvokePrivate(hud, "RefreshCollectibleUI");
            Assert.AreEqual(1, parent.childCount);
            Assert.AreSame(indicator, parent.GetChild(0));
            Assert.AreEqual(SaveSystem.Data.infiniteStaminaUntilUtc,
                GetPrivate(indicator.GetComponent<BoosterStatusHandler>(), "expiresAt"));
            SaveSystem.Data.infiniteStaminaUntilUtc = PlayerPerks.Now - 1;
            InvokePrivate(hud, "RefreshCollectibleUI");
            Assert.AreEqual(0, parent.childCount);
        }
        finally { Object.DestroyImmediate(root); SaveSystem.Data = previousData; }
    }

    private static SelectedUI NewHint(Transform parent, string name)
    {
        var hint = new GameObject(name);
        hint.transform.SetParent(parent, false);
        return hint.AddComponent<SelectedUI>();
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object GetPrivate(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void InvokePrivate(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
}
