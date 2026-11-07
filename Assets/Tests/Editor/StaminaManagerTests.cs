using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class StaminaManagerTests
{
    private GameObject root, barRoot;
    private StaminaManager stamina;
    private PlayerData previousData;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData();
        root = new GameObject("Stamina hunger test");
        stamina = root.AddComponent<StaminaManager>();
        typeof(StaminaManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stamina, null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(barRoot);
        SaveSystem.Data = previousData;
    }

    [Test]
    [TestCase(10f, 98f)]
    [TestCase(50f, 90f)]
    [TestCase(100f, 80f)]
    public void CarryDrainScalesWithGrainWeight(float kilograms, float remaining)
    {
        stamina.RestoreSave(100f, 0f);
        stamina.TickBagCarry(kilograms, 1f);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(remaining).Within(.001f));
        Assert.IsTrue(stamina.IsWorking);
    }

    [Test]
    public void CarryingExhaustsWithoutRegeneratingAndEmptyHandsRecover()
    {
        stamina.RestoreSave(5f, 0f);
        stamina.TickBagCarry(50f, 1f);
        Assert.IsTrue(stamina.IsExhausted);
        stamina.TickBagCarry(50f, 3f);
        Assert.AreEqual(0f, stamina.CurrentStamina);
        stamina.TickBagCarry(0f, 3f);
        Assert.Greater(stamina.CurrentStamina, 0f);
    }

    [Test]
    public void BagCarryRespectsZeroTimeAndInfiniteStamina()
    {
        stamina.RestoreSave(50f, 0f);
        stamina.TickBagCarry(100f, 0f);
        Assert.AreEqual(50f, stamina.CurrentStamina);
        SaveSystem.Data.infiniteStaminaUntilUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
        stamina.TickBagCarry(100f, 100f);
        Assert.AreEqual(stamina.CurrentMaxStamina, stamina.CurrentStamina);
        Assert.IsFalse(stamina.IsExhausted);
    }

    [Test]
    public void HungerReducesCapacityAndCurrentStaminaWithoutChangingBaseMaximum()
    {
        stamina.SetHungerPercent(10f);
        Assert.AreEqual(100f, stamina.MaxStamina);
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(90f).Within(.001f));
        Assert.That(stamina.CurrentStamina, Is.EqualTo(90f).Within(.001f));
        Assert.AreEqual(1f, stamina.NormalizedStamina);
        stamina.Tick(true, 1f);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(80f).Within(.001f));
        stamina.Tick(false, 100f);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(90f).Within(.001f));
    }

    [Test]
    public void JumpConsumesStaminaOnlyWhenAffordableAndDelaysRecovery()
    {
        stamina.RestoreSave(10f, 0f);
        Assert.IsTrue(stamina.TryConsumeJumpStamina());
        Assert.AreEqual(0f, stamina.CurrentStamina);
        Assert.IsFalse(stamina.TryConsumeJumpStamina());
        stamina.Tick(false, 1f);
        Assert.AreEqual(0f, stamina.CurrentStamina);
        stamina.RestoreSave(9f, 0f);
        Assert.IsFalse(stamina.TryConsumeJumpStamina());
        Assert.AreEqual(9f, stamina.CurrentStamina);
    }

    [Test]
    public void InfiniteStaminaAllowsJumpWithoutSpendingStamina()
    {
        stamina.RestoreSave(0f, 0f);
        SaveSystem.Data.infiniteStaminaUntilUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
        Assert.IsTrue(stamina.TryConsumeJumpStamina());
        Assert.AreEqual(0f, stamina.CurrentStamina);
    }

    [Test]
    public void HungerGrowsAtConfiguredRateAndRejectsInvalidTime()
    {
        SetField("hungerIncreasePerMinute", 1f);
        stamina.TickHunger(600f);
        Assert.That(stamina.HungryPercent, Is.EqualTo(10f).Within(.001f));
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(90f).Within(.001f));
        stamina.TickHunger(float.NaN);
        stamina.TickHunger(float.PositiveInfinity);
        stamina.TickHunger(-1f);
        Assert.That(stamina.HungryPercent, Is.EqualTo(10f).Within(.001f));
    }

    [Test]
    public void ReducingHungerRestoresCapacityAndAllowsStaminaToRecover()
    {
        stamina.SetHungerPercent(50f);
        stamina.SetHungerPercent(10f);
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(90f).Within(.001f));
        Assert.That(stamina.CurrentStamina, Is.EqualTo(50f).Within(.001f));
        stamina.Tick(false, 100f);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(90f).Within(.001f));
    }

    [Test]
    public void HungerStopsAtDefaultMaximumAndRetainsThirtyPercentCapacity()
    {
        stamina.TickHunger(60000f);
        Assert.AreEqual(70f, stamina.HungryPercent);
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(stamina.MaxStamina * .3f).Within(.001f));
        stamina.TickHunger(60f);
        Assert.AreEqual(70f, stamina.HungryPercent);
    }

    [Test]
    public void RestoredHungerAndStaminaRespectConfiguredMaximum()
    {
        SetField("maxHungryPercent", 60f);
        stamina.RestoreSave(100f, 95f);
        Assert.AreEqual(60f, stamina.SavedHunger);
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(40f).Within(.001f));
        Assert.That(stamina.CurrentStamina, Is.EqualTo(40f).Within(.001f));
    }

    [Test]
    public void CompleteHungerHasZeroCapacityWithoutInvalidFillValues()
    {
        SetField("maxHungryPercent", 100f);
        stamina.SetHungerPercent(200f);
        Assert.AreEqual(100f, stamina.HungryPercent);
        Assert.AreEqual(0f, stamina.CurrentMaxStamina);
        Assert.AreEqual(0f, stamina.CurrentStamina);
        Assert.AreEqual(0f, stamina.NormalizedStamina);
        Assert.IsTrue(stamina.IsExhausted);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BackgroundWidthUsesOriginalSizeAndFillFollowsIt(bool stretched)
    {
        barRoot = new GameObject("Bar parent", typeof(RectTransform));
        var parent = barRoot.GetComponent<RectTransform>();
        parent.sizeDelta = new Vector2(200, 20);
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);
        var rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = stretched ? Vector2.one : Vector2.zero;
        rect.sizeDelta = stretched ? Vector2.zero : new Vector2(200, 20);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(rect, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        SetField("staminaBarBackground", background.GetComponent<Image>());
        SetField("staminaBarFill", fill.GetComponent<Image>());

        stamina.SetHungerPercent(10f);
        stamina.SetHungerPercent(10f);
        Assert.That(rect.rect.width, Is.EqualTo(180f).Within(.001f));
        Assert.That(fillRect.rect.width, Is.EqualTo(180f).Within(.001f));
        Assert.AreEqual(1f, fill.GetComponent<Image>().fillAmount);
        if (stretched)
        {
            parent.sizeDelta = new Vector2(400, 20);
            stamina.SetHungerPercent(10f);
            Assert.That(rect.rect.width, Is.EqualTo(360f).Within(.001f));
        }
        stamina.SetHungerPercent(0f);
        Assert.That(rect.rect.width, Is.EqualTo(stretched ? 400f : 200f).Within(.001f));
    }

    [Test]
    public void InfiniteStaminaRefillsOnlyToHungerAdjustedCapacity()
    {
        SaveSystem.Data.infiniteStaminaUntilUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;
        stamina.SetHungerPercent(10f);
        stamina.Tick(true, 10f);
        Assert.That(stamina.CurrentStamina, Is.EqualTo(90f).Within(.001f));
        Assert.AreEqual(1f, stamina.NormalizedStamina);
    }

    [Test]
    public void EatingImmediatelyRestoresCapacityAndBackgroundWidth()
    {
        barRoot = new GameObject("Food stamina bar", typeof(RectTransform), typeof(Image));
        var background = barRoot.GetComponent<RectTransform>();
        background.sizeDelta = new Vector2(200, 20);
        SetField("staminaBarBackground", barRoot.GetComponent<Image>());
        stamina.SetHungerPercent(30f);
        Assert.That(background.rect.width, Is.EqualTo(140f).Within(.001f));
        Assert.IsTrue(stamina.ConsumeFood(20f));
        Assert.That(stamina.HungryPercent, Is.EqualTo(10f).Within(.001f));
        Assert.That(stamina.CurrentMaxStamina, Is.EqualTo(90f).Within(.001f));
        Assert.That(background.rect.width, Is.EqualTo(180f).Within(.001f));
        Assert.That(stamina.CurrentStamina, Is.EqualTo(70f).Within(.001f));
    }

    [Test]
    public void FoodCannotReduceHungerBelowZeroOrBeUsedWhenAlreadyFull()
    {
        stamina.SetHungerPercent(10f);
        Assert.IsTrue(stamina.ConsumeFood(20f));
        Assert.AreEqual(0f, stamina.HungryPercent);
        Assert.AreEqual(100f, stamina.CurrentMaxStamina);
        Assert.IsFalse(stamina.ConsumeFood(20f));
    }

    [TestCase(-10f)]
    [TestCase(0f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidFoodValueDoesNotChangeHunger(float amount)
    {
        stamina.SetHungerPercent(30f);
        Assert.IsFalse(stamina.ConsumeFood(amount));
        Assert.AreEqual(30f, stamina.HungryPercent);
    }

    [Test]
    public void ShaderFillTracksStaminaWithoutChangingTheSharedMaterial()
    {
        barRoot = new GameObject("Shader stamina bar", typeof(RectTransform), typeof(Image));
        var image = barRoot.GetComponent<Image>();
        var shared = new Material(Shader.Find("UI/Image Margin Fade"));
        shared.SetFloat("_FillAmount", .25f);
        image.material = shared;
        SetField("staminaBarFill", image);
        try
        {
            stamina.Tick(true, 1f);
            var runtime = image.material;
            Assert.AreNotSame(shared, runtime);
            Assert.That(runtime.GetFloat("_FillAmount"), Is.EqualTo(stamina.NormalizedStamina).Within(.001f));
            Assert.That(runtime.GetFloat("_UseFill"), Is.EqualTo(1f));
            Assert.That(shared.GetFloat("_FillAmount"), Is.EqualTo(.25f));
            SetField("maxHungryPercent", 100f);
            stamina.SetHungerPercent(100f);
            Assert.That(runtime.GetFloat("_FillAmount"), Is.Zero);
            Object.DestroyImmediate(root);
            Assert.AreSame(shared, image.material);
            Assert.IsTrue(runtime == null);
        }
        finally { Object.DestroyImmediate(shared); }
    }

    private void SetField(string name, object value) =>
        typeof(StaminaManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stamina, value);
}
