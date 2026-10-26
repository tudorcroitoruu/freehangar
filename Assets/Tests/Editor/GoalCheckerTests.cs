using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GoalCheckerTests
{
    private GoalSettings goal;
    private GoalSettingsSave saved;

    [SetUp]
    public void SetUp()
    {
        goal = new GoalSettings { goalId = "load", goalType = GoalType.GrainLoaded, hangarId = 2,
            goalAmount = 100, progressMode = GoalProgressMode.LifetimeTotal };
        saved = new GoalSettingsSave { goalId = goal.goalId };
    }

    [Test]
    public void WrongActionOrHangarDoesNotCount()
    {
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainMoved, 10, 2), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainLoaded, 10, 1), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainLoaded, 10), true));
        Assert.AreEqual(0, saved.currentAmount);
    }

    [Test]
    public void MixedGrainsContributeWhenFilterIsOff()
    {
        GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainLoaded, 30, 2, GrainType.Wheat), true);
        GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainLoaded, 40, 2, GrainType.Corn), true);
        Assert.AreEqual(70, saved.currentAmount);
    }

    [Test]
    public void SpecificFilterRejectsOtherAndUnknownGrains()
    {
        goal.filterByGrainType = true;
        goal.grainType = GrainType.Wheat;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 30, 2, GrainType.Corn), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 30, 2), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 30, 2, GrainType.Wheat), true));
    }

    [Test]
    public void AnyHangarCountsKnownAndUnknownSources()
    {
        goal.hangarId = 0;
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 30), true);
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 40, 5), true);
        Assert.AreEqual(70, saved.currentAmount);
    }

    [TestCase(-1f)]
    [TestCase(0f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidIncrementsDoNotCount(float amount)
    {
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, amount, 2), true));
        Assert.AreEqual(0, saved.currentAmount);
    }

    [Test]
    public void ProgressIsCappedAndCompletedGoalsCannotChange()
    {
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 1000, 2), true);
        Assert.AreEqual(100, saved.currentAmount);
        saved.completed = true;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 20, 2), true));
    }

    [Test]
    public void FutureSinceActivationGoalIgnoresEarlierActions()
    {
        goal.progressMode = GoalProgressMode.SinceActivation;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 20, 2), false));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 20, 2), true));
        Assert.AreEqual(20, saved.currentAmount);
    }

    [Test]
    public void FutureLifetimeGoalRemembersEarlierActions()
    {
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 20, 2), false));
        Assert.AreEqual(20, saved.currentAmount);
    }

    [Test]
    public void SackSnapshotsDoNotSumDifferentPartiallyFilledSacks()
    {
        goal.goalType = GoalType.SackFilled;
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 60, 2, isValue: true), false);
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 50, 2, isValue: true), false);
        Assert.AreEqual(60, saved.currentAmount);
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 40, 2), true));
    }

    [Test]
    public void CurrentStateCanDecreaseBeforeActivationAndRejectsDeltas()
    {
        goal.goalType = GoalType.HangarCleared;
        goal.progressMode = GoalProgressMode.CurrentValue;
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 75, 2, isValue: true), false);
        GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 20, 2, isValue: true), false);
        Assert.AreEqual(20, saved.currentAmount);
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 80, 2), true));
    }

    [Test]
    public void UpgradeMustMatch()
    {
        goal.goalType = GoalType.UpgradePurchased;
        goal.progressMode = GoalProgressMode.CurrentValue;
        goal.upgradeType = GoalUpgradeType.Wheelbarrow;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 1, 2,
            upgradeType: GoalUpgradeType.HandCart, isValue: true), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 1, 2,
            upgradeType: GoalUpgradeType.Wheelbarrow, isValue: true), true));
    }

    [Test]
    public void CustomActionMustIdentifyItsGoal()
    {
        goal.goalType = GoalType.Custom;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 1, 2), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 1, 2, goalId: goal.goalId), true));
    }
}

public class GoalsManagerTests
{
    private GameObject owner;
    private GoalsManager manager;

    private void Create(params GoalSettings[] goals)
    {
        owner = new GameObject("Goal checks") { hideFlags = HideFlags.HideAndDontSave };
        owner.SetActive(false);
        manager = owner.AddComponent<GoalsManager>();
        typeof(GoalsManager).GetField("allGoalSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, goals);
    }

    private static GoalSettings Goal(string id, float target, GoalProgressMode mode, int reward = 0) =>
        new GoalSettings { goalId = id, goalType = GoalType.GrainLoaded, hangarId = 1,
            goalAmount = target, progressMode = mode, rewardAmount = reward };

    [TearDown]
    public void TearDown()
    {
        if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
    }

    [Test]
    public void OneActionDoesNotSpillIntoNextSinceActivationGoal()
    {
        Create(Goal("first", 10, GoalProgressMode.SinceActivation, 5),
            Goal("second", 10, GoalProgressMode.SinceActivation, 5));
        var data = new PlayerData { cash = 0 };
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 100, 1));
        Assert.AreEqual("second", manager.CurrentGoal.goalId);
        Assert.AreEqual(0, manager.CurrentProgress);
        Assert.AreEqual(5, data.cash);
    }

    [Test]
    public void LifetimeMilestonesAdvanceInOrderAndRewardsAreNotGrantedTwiceAfterReload()
    {
        Create(Goal("first", 10, GoalProgressMode.LifetimeTotal, 5),
            Goal("second", 20, GoalProgressMode.LifetimeTotal, 7));
        var data = new PlayerData { cash = 0 };
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 100, 1));
        Assert.IsTrue(manager.AllGoalsCompleted);
        Assert.AreEqual(12, data.cash);
        Assert.IsTrue(data.goals.All(g => g.completed && g.rewardClaimed));
        var reloaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        manager.Initialize(reloaded);
        Assert.AreEqual(12, reloaded.cash);
        Assert.IsTrue(manager.AllGoalsCompleted);
    }

    [Test]
    public void PartialProgressSurvivesJsonRoundTrip()
    {
        Create(Goal("first", 100, GoalProgressMode.SinceActivation));
        var data = new PlayerData();
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 35, 1));
        manager.Initialize(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data)));
        Assert.AreEqual(35, manager.CurrentProgress);
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
    }

    [Test]
    public void ActionsBeforeSaveLoadAreBuffered()
    {
        Create(Goal("first", 100, GoalProgressMode.LifetimeTotal));
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 35, 1));
        manager.Initialize(new PlayerData());
        Assert.AreEqual(35, manager.CurrentProgress);
    }

    [Test]
    public void FutureStateDoesNotSkipEarlierGoal()
    {
        var upgrade = new GoalSettings { goalId = "cart", goalType = GoalType.UpgradePurchased,
            upgradeType = GoalUpgradeType.HandCart, goalAmount = 1, progressMode = GoalProgressMode.CurrentValue };
        Create(Goal("first", 10, GoalProgressMode.SinceActivation), upgrade);
        manager.Initialize(new PlayerData());
        manager.ProcessAction(new GoalAction(GoalType.UpgradePurchased, 1,
            upgradeType: GoalUpgradeType.HandCart, isValue: true));
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        Assert.IsTrue(manager.AllGoalsCompleted);
    }

    [Test]
    public void GoalRewardsDoNotCountAsGrainSaleIncome()
    {
        var income = new GoalSettings { goalId = "income", goalType = GoalType.MoneyEarned,
            goalAmount = 5, progressMode = GoalProgressMode.LifetimeTotal };
        Create(Goal("first", 10, GoalProgressMode.LifetimeTotal, 50), income);
        manager.Initialize(new PlayerData { cash = 0 });
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        Assert.AreEqual("income", manager.CurrentGoal.goalId);
        Assert.AreEqual(0, manager.CurrentProgress);
    }

    [Test]
    public void DuplicateIdsAreRejected()
    {
        Create(Goal("same", 10, GoalProgressMode.LifetimeTotal),
            Goal("same", 20, GoalProgressMode.LifetimeTotal));
        Assert.Throws<InvalidOperationException>(() => manager.Initialize(new PlayerData()));
    }

    [Test]
    public void MissingOldSaveArrayIsInitialized()
    {
        Create(Goal("first", 10, GoalProgressMode.LifetimeTotal));
        var data = new PlayerData { goals = null };
        manager.Initialize(data);
        Assert.AreEqual(1, data.goals.Length);
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
    }
}

