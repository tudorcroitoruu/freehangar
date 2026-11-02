using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class MoneyChangePopupTests
{
    private GameObject root;
    private GameplayCanvas canvas;
    private CanvasGroup popup;
    private Text balance, change;
    private RectTransform arrow;
    private Image arrowImage;
    private readonly Color addColor = Color.green, removeColor = Color.red;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Money popup test");
        root.SetActive(false);
        canvas = root.AddComponent<GameplayCanvas>();
        balance = Child("Balance", typeof(Text)).GetComponent<Text>();
        change = Child("Change", typeof(Text)).GetComponent<Text>();
        popup = Child("Popup", typeof(CanvasGroup)).GetComponent<CanvasGroup>();
        arrowImage = Child("Arrow", typeof(Image)).GetComponent<Image>();
        arrow = arrowImage.rectTransform;
        Set("moneyText", balance);
        Set("moneyChangeValue", change);
        Set("moneyChangeCanvas", popup);
        Set("arrow", arrow);
        Set("arrowImage", arrowImage);
        Set("addMoneyColor", addColor);
        Set("removeMoneyColor", removeColor);
        Set("moneyChangeTransferDelay", 0f);
        Call("ResetMoneyChange");
        Call("RefreshMoney", 100);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void InitialBalanceDoesNotShowAChange()
    {
        Assert.AreEqual("$100", balance.text);
        Assert.AreEqual("", change.text);
        Assert.AreEqual(0f, popup.alpha);
    }

    [Test]
    public void AdditionFadesInThenTransfersTheSameAmountIntoBalance()
    {
        Call("RefreshMoney", 200);
        Assert.AreEqual("+$100", change.text);
        Assert.AreEqual(addColor, change.color);
        Assert.AreEqual(addColor, arrowImage.color);
        Assert.Less(Quaternion.Angle(Quaternion.identity, arrow.localRotation), .01f);
        Call("AnimateMoney", .1f);
        Assert.That(popup.alpha, Is.EqualTo(.5f).Within(.001f));
        Assert.AreEqual("$100", balance.text);
        Call("AnimateMoney", .1f);
        Call("AnimateMoney", .2f);
        Assert.AreEqual("$150", balance.text);
        Assert.AreEqual("+$50", change.text);
        Call("AnimateMoney", .2f);
        Assert.AreEqual("$200", balance.text);
        Assert.AreEqual("+$0", change.text);
        Call("AnimateMoney", 1f);
        Assert.AreEqual(0f, popup.alpha);
        Assert.IsFalse(popup.interactable);
        Assert.IsFalse(popup.blocksRaycasts);
    }

    [Test]
    public void SpendingDecreasesBalanceAndRemainingCostTogether()
    {
        Call("RefreshMoney", 40);
        Assert.AreEqual("-$60", change.text);
        Assert.AreEqual(removeColor, change.color);
        Assert.AreEqual(removeColor, arrowImage.color);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 0, 180), arrow.localRotation), .01f);
        Call("AnimateMoney", .2f);
        Call("AnimateMoney", .2f);
        Assert.AreEqual("$70", balance.text);
        Assert.AreEqual("-$30", change.text);
        Call("AnimateMoney", .2f);
        Assert.AreEqual("$40", balance.text);
        Assert.AreEqual("-$0", change.text);
    }

    [Test]
    public void NewTransactionsPreserveDisplayedBalanceAndUpdateRemainingDifference()
    {
        Call("RefreshMoney", 200);
        Call("AnimateMoney", .2f);
        Call("AnimateMoney", .2f);
        Call("RefreshMoney", 250);
        Assert.AreEqual("$150", balance.text);
        Assert.AreEqual("+$100", change.text);
        Call("RefreshMoney", 120);
        Assert.AreEqual("$150", balance.text);
        Assert.AreEqual("-$30", change.text);
        Assert.AreEqual(removeColor, change.color);
        Call("AnimateMoney", .4f);
        Assert.AreEqual("$120", balance.text);
        Assert.AreEqual("-$0", change.text);
    }

    [Test]
    public void ResetClearsAnUnfinishedPopup()
    {
        Call("RefreshMoney", 200);
        Call("AnimateMoney", .1f);
        Call("ResetMoneyChange");
        Assert.AreEqual(0f, popup.alpha);
        Assert.AreEqual("", change.text);
    }

    [Test]
    public void TransferWaitsAfterFadeInAndNewUpdatesDoNotRestartTheDelay()
    {
        Set("moneyChangeTransferDelay", .3f);
        Call("RefreshMoney", 200);
        Call("AnimateMoney", .2f);
        Assert.AreEqual(1f, popup.alpha);
        Assert.AreEqual("$100", balance.text);
        Call("AnimateMoney", .15f);
        Call("RefreshMoney", 300);
        Call("AnimateMoney", .15f);
        Assert.AreEqual("$100", balance.text);
        Assert.AreEqual("+$200", change.text);
        Call("AnimateMoney", .2f);
        Assert.AreEqual("$200", balance.text);
        Assert.AreEqual("+$100", change.text);
    }

    [TestCase("sale", "+$50", 150)]
    [TestCase("cashCache", "+$50", 150)]
    [TestCase("cashChest", "+$50", 150)]
    [TestCase("purchase", "-$40", 60)]
    [TestCase("failedDelivery", "", 100)]
    [TestCase("unaffordable", "", 100)]
    public void GameplayTransactionsReachThePopupThroughMoneyChanged(string transaction, string expectedChange, int expectedCash)
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
        try
        {
            SaveSystem.Data = new PlayerData { cash = 100 };
            Call("OnEnable");
            if (transaction == "sale") Assert.IsTrue(canvas.AddMoney(50));
            else if (transaction == "cashCache" || transaction == "cashChest")
            {
                var find = Child("Cash discovery", typeof(BuriedFind)).GetComponent<BuriedFind>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(BuriedFind).GetField("kind", flags).SetValue(find,
                    transaction == "cashCache" ? BuriedFind.Kind.CashCache : BuriedFind.Kind.CashChest);
                typeof(BuriedFind).GetField("findId", flags).SetValue(find, "money-popup-test-" + transaction);
                typeof(BuriedFind).GetField("cashReward", flags).SetValue(find, 50);
                typeof(BuriedFind).GetField("variedChestRewards", flags).SetValue(find, false);
                var commit = typeof(BuriedFind).GetMethod("CommitReward", flags);
                Assert.IsTrue((bool)commit.Invoke(find, null));
                Assert.IsFalse((bool)commit.Invoke(find, null), "The same discovery must not grant money twice.");
            }
            else
            {
                bool succeeds = transaction == "purchase";
                int price = transaction == "unaffordable" ? 200 : 40;
                Assert.AreEqual(succeeds, SaveSystem.TryBuyProduct("money-popup-product", price,
                    () => transaction != "failedDelivery"));
            }
            Assert.AreEqual(expectedCash, SaveSystem.Money);
            Assert.AreEqual(expectedChange, change.text);
            Assert.AreEqual("$100", balance.text, "The balance should animate, rather than jump at commit.");
            Call("AnimateMoney", 1f);
            Assert.AreEqual("$" + EconomyNumberFormat.Accounting(expectedCash), balance.text);
        }
        finally
        {
            Call("OnDisable");
            SaveSystem.Data = previousData;
            for (int i = 0; i < keys.Length; i++)
                if (existed[i]) PlayerPrefs.SetString(keys[i], values[i]);
                else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
        }
    }

    private GameObject Child(string name, System.Type component)
    {
        var child = new GameObject(name, typeof(RectTransform), component);
        child.transform.SetParent(root.transform);
        return child;
    }

    private void Set(string field, object value) => typeof(GameplayCanvas)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(canvas, value);
    private void Call(string method, params object[] args) => typeof(GameplayCanvas)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, args);
}
