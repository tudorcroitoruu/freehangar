using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ConsumableFoodTests
{
    private GameObject actor, foodObject;
    private StaminaManager stamina;
    private PlayerInteractor interactor;
    private ConsumableFood food;
    private PlayerData previousData;

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData { cash = 20 };
        actor = new GameObject("Food buyer test");
        stamina = actor.AddComponent<StaminaManager>();
        var player = actor.AddComponent<PlayerController>();
        SetField(player, "staminaManager", stamina);
        interactor = actor.AddComponent<PlayerInteractor>();
        foodObject = new GameObject("Food test");
        food = foodObject.AddComponent<ConsumableFood>();
        SetField(food, "foodName", "Bread");
        SetField(food, "foodPrice", 25);
        stamina.SetHungerPercent(30f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(foodObject);
        Object.DestroyImmediate(actor);
        SaveSystem.Data = previousData;
    }

    [Test]
    public void InsufficientMoneyShowsPriceAndDoesNotConsumeFood()
    {
        Assert.IsTrue(food.TryGetPrompt(interactor, out var prompt));
        StringAssert.Contains("Buy & eat Bread\n$ 25", prompt.Action);
        StringAssert.Contains("Not enough money", prompt.Action);
        Assert.IsFalse(prompt.ShowKey);
        Assert.IsFalse(food.TryInteract(interactor));
        Assert.AreEqual(20, SaveSystem.Money);
        Assert.AreEqual(30f, stamina.HungryPercent);
        Assert.IsTrue(foodObject.activeSelf);
    }

    [Test]
    public void NoHungerDoesNotChargeMoneyOrConsumeFood()
    {
        SaveSystem.Data.cash = 100;
        stamina.SetHungerPercent(0f);
        food.TryGetPrompt(interactor, out var prompt);
        Assert.IsFalse(prompt.ShowKey);
        StringAssert.Contains("Not hungry", prompt.Action);
        Assert.IsFalse(food.TryInteract(interactor));
        Assert.AreEqual(100, SaveSystem.Money);
        Assert.IsTrue(foodObject.activeSelf);
    }

    [Test]
    public void ExactMoneyEnablesPurchasePrompt()
    {
        SaveSystem.Data.cash = 25;
        Assert.IsTrue(food.TryGetPrompt(interactor, out var prompt));
        Assert.IsTrue(prompt.ShowKey);
        Assert.AreEqual("Buy & eat Bread\n$ 25", prompt.Action);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
