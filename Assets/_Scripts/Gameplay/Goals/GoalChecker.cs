using System;
using UnityEngine;

/// <summary>Successful gameplay actions report here; GoalsManager owns progression and saving.</summary>
public static class GoalChecker
{
    public static event Action<GoalAction> ActionReported;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => ActionReported = null;

    /// <summary>Report an actual increment: kg transferred, money from a sale, or one successful action.</summary>
    public static void ReportProgress(GoalType type, float amount = 1f, int hangarId = 0,
        GrainType? grainType = null, GoalUpgradeType upgradeType = GoalUpgradeType.None, string goalId = null)
    {
        Publish(new GoalAction(type, amount, hangarId, grainType, upgradeType, false, goalId));
    }

    /// <summary>Report a state: sack kg, cleared percentage (0-100), or 1/0 for true/false.</summary>
    public static void ReportValue(GoalType type, float value, int hangarId = 0,
        GrainType? grainType = null, GoalUpgradeType upgradeType = GoalUpgradeType.None, string goalId = null)
    {
        Publish(new GoalAction(type, value, hangarId, grainType, upgradeType, true, goalId));
    }

    private static void Publish(GoalAction action)
    {
        if (Application.isPlaying && IsValid(action)) ActionReported?.Invoke(action);
    }

    public static bool IsValid(GoalAction action) =>
        !float.IsNaN(action.amount) && !float.IsInfinity(action.amount) &&
        action.hangarId >= 0 && (action.isValue ? action.amount >= 0 : action.amount > 0);

    /// <summary>Future cumulative/state goals remember progress; inactive SinceActivation goals do not.</summary>
    public static bool TryApply(GoalSettings goal, GoalSettingsSave saved, GoalAction action, bool isCurrentGoal)
    {
        if (goal == null || saved == null || saved.completed || !IsValid(action) ||
            goal.goalType != action.type ||
            (goal.hangarId > 0 && goal.hangarId != action.hangarId) ||
            (goal.filterByGrainType && (!action.grainType.HasValue || goal.grainType != action.grainType.Value)) ||
            (goal.goalType == GoalType.UpgradePurchased && goal.upgradeType != action.upgradeType) ||
            (goal.goalType == GoalType.Custom && goal.goalId != action.goalId) ||
            (goal.progressMode == GoalProgressMode.SinceActivation && !isCurrentGoal))
            return false;

        bool expectsValue = goal.progressMode == GoalProgressMode.CurrentValue || IsStateType(goal.goalType);
        if (action.isValue != expectsValue) return false;
        // A sack's fill is one sack's kg, never a sum of partially filled sacks.
        // Lifetime state objectives retain the highest observed value; CurrentValue can decrease.
        float next = action.isValue
            ? goal.progressMode == GoalProgressMode.CurrentValue ? action.amount : Mathf.Max(saved.currentAmount, action.amount)
            : saved.currentAmount + action.amount;
        next = Mathf.Clamp(next, 0f, goal.goalAmount);
        if (saved.currentAmount == next) return false;
        saved.currentAmount = next;
        return true;
    }

    private static bool IsStateType(GoalType type) =>
        type == GoalType.SackFilled || type == GoalType.HangarCleared ||
        type == GoalType.ShovelEquipped || type == GoalType.UpgradePurchased ||
        type == GoalType.HangarKeyCollected || type == GoalType.HangarUnlocked ||
        type == GoalType.BuyerReached;
}

public readonly struct GoalAction
{
    public readonly GoalType type;
    public readonly float amount;
    public readonly int hangarId;
    public readonly GrainType? grainType;
    public readonly GoalUpgradeType upgradeType;
    public readonly bool isValue;
    public readonly string goalId;

    public GoalAction(GoalType type, float amount, int hangarId = 0, GrainType? grainType = null,
        GoalUpgradeType upgradeType = GoalUpgradeType.None, bool isValue = false, string goalId = null)
    {
        this.type = type;
        this.amount = amount;
        this.hangarId = hangarId;
        this.grainType = grainType;
        this.upgradeType = upgradeType;
        this.isValue = isValue;
        this.goalId = goalId;
    }
}

