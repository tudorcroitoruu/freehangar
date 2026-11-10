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
        goal.goalType = GoalType.BagsFilled;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(GoalType.GrainMoved, 10, 2), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 10, 1), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(goal.goalType, 10), true));
        Assert.AreEqual(0, saved.currentAmount);
    }

    [TestCase(0f, 0f)]
    [TestCase(49f, 50f)]
    [TestCase(98f, 100f)]
    [TestCase(100f, 100f)]
    public void DisplayProgressScalesToTheDisplayTarget(float actual, float displayed)
    {
        goal.goalAmount = 98f;
        goal.useDisplayGoalAmount = true;
        goal.displayGoalAmount = 100f;
        Assert.AreEqual(displayed, goal.GetDisplayProgress(actual), .001f);
    }

    [Test]
    public void UnconfiguredDisplayTargetUsesActualUnits()
    {
        Assert.AreEqual(100f, goal.DisplayGoalAmount);
        Assert.AreEqual(49f, goal.GetDisplayProgress(49f), .001f);
    }

    [Test]
    public void DisabledDisplayOverrideUsesActualUnitsEvenWithDisplayAmountConfigured()
    {
        goal.goalAmount = 98f;
        goal.displayGoalAmount = 100f;
        goal.useDisplayGoalAmount = false;
        Assert.AreEqual(98f, goal.DisplayGoalAmount);
        Assert.AreEqual(49f, goal.GetDisplayProgress(49f), .001f);
    }

    [TestCase(GoalType.GrainCollected)]
    [TestCase(GoalType.GrainLoaded)]
    [TestCase(GoalType.GrainSold)]
    [TestCase(GoalType.GrainMoved)]
    public void GlobalGrainQuantitiesIgnoreHangarAndKeepGrainFilter(GoalType type)
    {
        goal.goalType = type;
        goal.filterByGrainType = true;
        goal.grainType = GrainType.Wheat;
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(type, 12.5f, 1, GrainType.Wheat), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(type, 20, 5, GrainType.Wheat), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(type, 7.5f, 0, GrainType.Wheat), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(type, 30, 2, GrainType.Corn), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(type, 30, 2), true));
        Assert.AreEqual(40, saved.currentAmount);
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

    [TestCase(GoalType.HangarKeyCollected)]
    [TestCase(GoalType.HangarUnlocked)]
    public void HangarMilestonesRequireStateReportsAndMatchingHangar(GoalType type)
    {
        goal.goalType = type;
        goal.progressMode = GoalProgressMode.CurrentValue;
        goal.goalAmount = 1;
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(type, 1, 2), true));
        Assert.IsFalse(GoalChecker.TryApply(goal, saved, new GoalAction(type, 1, 3, isValue: true), true));
        Assert.IsTrue(GoalChecker.TryApply(goal, saved, new GoalAction(type, 1, 2, isValue: true), true));
    }

    [Test]
    public void SavedHangarStateKeepsKeyCollectionSeparateFromOpening()
    {
        var data = new PlayerData { foundSecrets = new[] { "farm.hangar.key.2" } };
        goal.goalType = GoalType.HangarKeyCollected;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float key));
        Assert.AreEqual(1, key);
        goal.goalType = GoalType.HangarUnlocked;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float closed));
        Assert.AreEqual(0, closed);
        data.TryOpenHangar(2);
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float opened));
        Assert.AreEqual(1, opened);
    }

    [Test]
    public void ClearanceStateRestoresTheSelectedHangarPercentage()
    {
        var data = new PlayerData();
        data.TryRecordHangarClearance(1, 100);
        data.TryRecordHangarClearance(2, 75);
        goal.goalType = GoalType.HangarCleared;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float value));
        Assert.AreEqual(75, value);
    }

    [TestCase(GoalUpgradeType.LargerSack)]
    [TestCase(GoalUpgradeType.SmallSack)]
    [TestCase(GoalUpgradeType.LargerTruck)]
    public void PurchasedUpgradesRemainCompleteWithoutPickupStock(GoalUpgradeType type)
    {
        var data = new PlayerData();
        data.purchasedGoalUpgrades.Add(type);
        goal.goalType = GoalType.UpgradePurchased;
        goal.upgradeType = type;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float value));
        Assert.AreEqual(1, value);
        Assert.AreEqual(0, data.GetOwnedProductCount("largeBag"));
    }

    [Test]
    public void PreviouslyPurchasedSmallBagCountsAfterPickupAndReload()
    {
        var data = new PlayerData { cash = 100 };
        Assert.IsTrue(data.TryBuyProduct("smallBag", 8));
        Assert.IsTrue(data.TryTakeProduct("smallBag"));
        data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        goal.goalType = GoalType.UpgradePurchased;
        goal.upgradeType = GoalUpgradeType.SmallSack;
        Assert.AreEqual(0, data.GetOwnedProductCount("smallBag"));
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float value));
        Assert.AreEqual(1, value);
    }

    [Test]
    public void LegacySmallBagInWorldCountsWithoutShopStock()
    {
        var data = new PlayerData { world = new WorldSaveData() };
        data.world.bags.Add(new BagSave { prefab = "SmallBag" });
        goal.goalType = GoalType.UpgradePurchased;
        goal.upgradeType = GoalUpgradeType.SmallSack;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float value));
        Assert.AreEqual(1, value);
    }

    [Test]
    public void FreeBagDoesNotSatisfySmallBagGoal()
    {
        var data = new PlayerData { world = new WorldSaveData() };
        data.world.bags.Add(new BagSave { prefab = "FreeBag" });
        goal.goalType = GoalType.UpgradePurchased;
        goal.upgradeType = GoalUpgradeType.SmallSack;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float value));
        Assert.AreEqual(0, value);
    }

    [Test]
    public void LegacyVehicleAndShovelPurchasesRestoreUpgradeState()
    {
        var data = new PlayerData { shovelUpgradeLevel = 1 };
        data.unlockedVehicles.Add("motoroller");
        goal.goalType = GoalType.UpgradePurchased;
        goal.upgradeType = GoalUpgradeType.Shovel;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float shovel));
        Assert.AreEqual(1, shovel);
        goal.upgradeType = GoalUpgradeType.LargerTruck;
        Assert.IsTrue(GoalChecker.TryGetSavedValue(goal, data, out float vehicle));
        Assert.AreEqual(1, vehicle);
    }
}

public class GoalsManagerTests
{
    [Test]
    public void CheatCompletesOnlySelectedHangarGrainGoalsAndRewardsOnce()
    {
        var previousData = SaveSystem.Data;
        bool previousLoaded = SaveSystem.savesLoaded;
        try
        {
            SaveSystem.savesLoaded = false;
            var data = SaveSystem.Data = new PlayerData { cash = 0 };
            var first = Goal("blocker", 1, GoalProgressMode.SinceActivation);
            var load = Goal("h2_load_1000kg", 1000, GoalProgressMode.SinceActivation, 20);
            load.hangarId = 0;
            var clear = new GoalSettings { goalId = "h2_clear", hangarId = 2,
                goalType = GoalType.HangarCleared, goalAmount = 100, rewardAmount = 30 };
            var key = new GoalSettings { goalId = "h2_collect_key", hangarId = 2,
                goalType = GoalType.HangarKeyCollected, goalAmount = 1 };
            var other = Goal("h3_load_4500kg", 4500, GoalProgressMode.SinceActivation, 40);
            Create(first, load, clear, key, other);
            manager.Initialize(data);
            manager.CompleteHangarGrainGoals(2);
            manager.CompleteHangarGrainGoals(2);
            Assert.That(data.cash, Is.EqualTo(50));
            Assert.IsTrue(data.goals.Single(g => g.goalId == load.goalId).completed);
            Assert.IsTrue(data.goals.Single(g => g.goalId == clear.goalId).rewardClaimed);
            Assert.IsFalse(data.goals.Single(g => g.goalId == key.goalId).completed);
            Assert.IsFalse(data.goals.Single(g => g.goalId == other.goalId).completed);
            manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 1));
            Assert.That(manager.CurrentGoal, Is.SameAs(key), "Completed future goals must be skipped.");
            Assert.That(data.cash, Is.EqualTo(50));
        }
        finally
        {
            SaveSystem.Data = previousData;
            SaveSystem.savesLoaded = previousLoaded;
        }
    }

    private GameObject owner;
    private GoalsManager manager;

    private void Create(params GoalSettings[] goals)
    {
        owner = new GameObject("Goal checks") { hideFlags = HideFlags.HideAndDontSave };
        owner.SetActive(false);
        manager = owner.AddComponent<GoalsManager>();
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 0f);
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
    public void CompletedGoalShowsCheckmarkUntilDelayEndsAndRewardsOnlyOnce()
    {
        Create(Goal("first", 10, GoalProgressMode.SinceActivation, 5),
            Goal("second", 10, GoalProgressMode.SinceActivation, 7));
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 2f);
        var elementObject = new GameObject("Goal element");
        elementObject.transform.SetParent(owner.transform);
        var element = elementObject.AddComponent<GoalListElement>();
        var checker = new GameObject("Checker");
        checker.transform.SetParent(owner.transform);
        typeof(GoalListElement).GetField("checkerObj", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(element, checker);
        typeof(GoalsManager).GetField("currentElement", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, element);
        int completions = 0;
        manager.GoalCompleted += _ => completions++;
        var data = new PlayerData { cash = 0 };
        manager.Initialize(data);
        Assert.IsFalse(checker.activeSelf);

        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
        Assert.IsTrue(checker.activeSelf);
        Assert.IsTrue(data.goals[0].completed);
        Assert.AreEqual(5, data.cash);
        AdvanceTransition(1f);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        Assert.AreEqual(1, completions);
        Assert.AreEqual(5, data.cash);
        AdvanceTransition(.9f);
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
        AdvanceTransition(.2f);
        Assert.AreEqual("second", manager.CurrentGoal.goalId);
        Assert.AreEqual(0, manager.CurrentProgress);
        Assert.IsFalse(checker.activeSelf);

        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        Assert.IsFalse(manager.AllGoalsCompleted);
        AdvanceTransition(2f);
        Assert.IsTrue(manager.AllGoalsCompleted);
        Assert.AreEqual(12, data.cash);
        Assert.AreEqual(2, completions);
    }

    [Test]
    public void ReadyLifetimeGoalsEachWaitBeforeAdvancingAndReloadDoesNotRewardTwice()
    {
        Create(Goal("first", 10, GoalProgressMode.LifetimeTotal, 5),
            Goal("second", 20, GoalProgressMode.LifetimeTotal, 7));
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 2f);
        var data = new PlayerData { cash = 0 };
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 100, 1));
        Assert.AreEqual("first", manager.CurrentGoal.goalId);
        Assert.AreEqual(5, data.cash);
        AdvanceTransition(2f);
        Assert.AreEqual("second", manager.CurrentGoal.goalId);
        Assert.AreEqual(12, data.cash);
        Assert.IsFalse(manager.AllGoalsCompleted);
        manager.Initialize(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data)));
        Assert.IsTrue(manager.AllGoalsCompleted);
        AdvanceTransition(2f);
        Assert.IsTrue(manager.AllGoalsCompleted);
    }

    private void AdvanceTransition(float seconds) => typeof(GoalsManager)
        .GetMethod("AdvanceGoalTransition", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(manager, new object[] { seconds });

    [TestCase(false)]
    [TestCase(true)]
    public void FinalDoorCompletesEarlierGoalsAndAdvancesToLightWithoutDuplicateRewards(bool earlierGoalPending)
    {
        var door = new GoalSettings { goalId = GoalChecker.LaunchRoomDoorGoalId, goalType = GoalType.Custom,
            hangarId = 5, goalAmount = 1, progressMode = GoalProgressMode.LifetimeTotal, rewardAmount = 7 };
        var light = new GoalSettings { goalId = GoalChecker.LaunchRoomLightGoalId, goalType = GoalType.Custom,
            hangarId = 5, goalAmount = 1, progressMode = GoalProgressMode.LifetimeTotal, rewardAmount = 11 };
        Create(Goal("earlier", 10, GoalProgressMode.SinceActivation, 5), door, light);
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 2f);
        var data = new PlayerData();
        manager.Initialize(data);
        if (earlierGoalPending) manager.ProcessAction(new GoalAction(GoalType.GrainLoaded, 10, 1));
        manager.ProcessAction(new GoalAction(GoalType.Custom, 1, 5, goalId: door.goalId));
        Assert.AreEqual(door.goalId, manager.CurrentGoal.goalId);
        Assert.IsTrue(data.goals.Single(g => g.goalId == "earlier").completed);
        Assert.IsTrue(data.goals.Single(g => g.goalId == door.goalId).completed);
        Assert.IsFalse(data.goals.Single(g => g.goalId == light.goalId).completed);
        Assert.AreEqual(12, data.cash);
        AdvanceTransition(1f);
        manager.ProcessAction(new GoalAction(GoalType.Custom, 1, 5, goalId: door.goalId));
        AdvanceTransition(1.1f);
        Assert.AreEqual(light.goalId, manager.CurrentGoal.goalId);
        manager.Initialize(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data)));
        Assert.AreEqual(light.goalId, manager.CurrentGoal.goalId);
        Assert.AreEqual(12, data.cash);
        manager.ProcessAction(new GoalAction(GoalType.Custom, 1, 5, goalId: light.goalId));
        AdvanceTransition(2f);
        Assert.IsTrue(manager.AllGoalsCompleted);
        Assert.AreEqual(23, data.cash);
    }

    [Test]
    public void LightInteractionDuringDoorCompletionIsSavedBeforeCinematic()
    {
        var door = new GoalSettings { goalId = GoalChecker.LaunchRoomDoorGoalId, goalType = GoalType.Custom,
            hangarId = 5, goalAmount = 1, progressMode = GoalProgressMode.LifetimeTotal };
        var light = new GoalSettings { goalId = GoalChecker.LaunchRoomLightGoalId, goalType = GoalType.Custom,
            hangarId = 5, goalAmount = 1, progressMode = GoalProgressMode.LifetimeTotal };
        Create(door, light);
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 2f);
        var data = new PlayerData();
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.Custom, 1, 5, goalId: door.goalId));
        manager.ProcessAction(new GoalAction(GoalType.Custom, 1, 5, goalId: light.goalId));
        Assert.IsTrue(data.goals.All(g => g.completed && g.rewardClaimed));
        manager.Initialize(JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data)));
        Assert.IsTrue(manager.AllGoalsCompleted);
    }

    [Test]
    public void HangarCompletesAtActualTargetAndShowsFullDisplayProgressAfterReload()
    {
        var clearance = new GoalSettings { goalId = "clear", goalType = GoalType.HangarCleared,
            hangarId = 1, goalAmount = 98f, useDisplayGoalAmount = true, displayGoalAmount = 100f,
            progressMode = GoalProgressMode.CurrentValue, rewardAmount = 5 };
        Create(clearance);
        var data = new PlayerData { cash = 0 };
        manager.Initialize(data);
        manager.ProcessAction(new GoalAction(GoalType.HangarCleared, 97f, 1, isValue: true));
        Assert.IsFalse(data.goals[0].completed);
        manager.ProcessAction(new GoalAction(GoalType.HangarCleared, 98f, 1, isValue: true));
        Assert.IsTrue(data.goals[0].completed);
        Assert.AreEqual(98f, data.goals[0].currentAmount);
        Assert.AreEqual(100f, clearance.GetDisplayProgress(data.goals[0].currentAmount));
        var reloaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        manager.Initialize(reloaded);
        Assert.IsTrue(manager.AllGoalsCompleted);
        Assert.AreEqual(5, reloaded.cash);
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

