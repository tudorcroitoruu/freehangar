using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum ProgressArea
{
    LevelStart,
    LevelRestart,
    NextLevel,
    Revive,
    ContentAccess,
    LevelSequence,
    AdEntry,
    PurchaseUnlock
}

public enum ProgressBlockReason
{
    NoHearts,
    AttemptsExhausted,
    NoCoins,
    AdNotReady,
    AdClosed,
    ContentLocked,
    TimerLocked,
    PreviousLocked,
    RewardedRequired,
    AdLoadFailed,
    AdShowFailed,
    IapNotReady,
    ProductUnavailable,
    IapFailed,
    PurchaseCancelled
}

public enum ProgressResolutionMethod
{
    Coins,
    RewardedAd,
    NaturalRefill,
    SinglePurchase,
    ContentPack,
    TimerElapsed,
    PreviousCompleted,
    PurchaseRetry
}

public enum LevelEntryAction
{
    Start,
    Restart,
    NextLevel
}

public readonly struct ProgressTarget
{
    public string TypeName { get; }
    public string Id { get; }

    private ProgressTarget(string typeName, string id)
    {
        TypeName = typeName;
        Id = id;
    }

    public static ProgressTarget Create(string typeName, string id)
        => new ProgressTarget(
            ValidateValue(typeName, nameof(typeName)),
            ValidateValue(id, nameof(id)));

    public override string ToString() => TypeName + ":" + Id;

    private static string ValidateValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Regex.IsMatch(value, "^[a-z][a-z0-9_]{0,63}$"))
            throw new ArgumentException(
                "Progress target values must start with a lowercase letter and use only lowercase letters, numbers, and underscores.",
                parameterName);
        return value;
    }
}

internal sealed class SDKProgressAnalytics
{
    private readonly SDKAnalytics analytics;

    internal SDKProgressAnalytics(SDKAnalytics sdkAnalytics)
    {
        analytics = sdkAnalytics;
        SaveSystem.State.progressGates ??= new List<string>();
    }

    internal void Register(ProgressArea area, ProgressTarget target)
    {
        ValidateTarget(area, target);
        string gate = GateKey(area, target);
        if (SaveSystem.State.progressGates.Contains(gate)) return;
        SaveSystem.State.progressGates.Add(gate);
        SaveSystem.Save();
    }

    internal void Block(ProgressArea area, ProgressBlockReason reason, ProgressTarget target)
    {
        ValidateBlocked(area, reason, target);
        string gate = GateKey(area, target);
        if (!SaveSystem.State.progressGates.Contains(gate))
        {
            SaveSystem.State.progressGates.Add(gate);
            SaveSystem.Save();
        }
        analytics.Design("progress_blocked:" + AreaName(area) + ":" + ReasonName(reason) + ":" + target);
    }

    internal bool Resolve(ProgressArea area, ProgressResolutionMethod method, ProgressTarget target)
    {
        ValidateResolved(area, method, target);
        string gate = GateKey(area, target);
        if (!SaveSystem.State.progressGates.Remove(gate)) return false;
        SaveSystem.Save();
        analytics.Design("progress_resolved:" + AreaName(area) + ":" + MethodName(method) + ":" + target);
        return true;
    }

    private static string GateKey(ProgressArea area, ProgressTarget target)
        => AreaName(area) + ":" + target.TypeName + ":" + target.Id;

    private static void ValidateBlocked(ProgressArea area, ProgressBlockReason reason, ProgressTarget target)
    {
        ValidateTarget(area, target);
        bool valid = area switch
        {
            ProgressArea.LevelStart or ProgressArea.LevelRestart or ProgressArea.NextLevel
                => reason == ProgressBlockReason.NoHearts,
            ProgressArea.Revive
                => reason == ProgressBlockReason.AttemptsExhausted ||
                   reason == ProgressBlockReason.NoCoins ||
                   reason == ProgressBlockReason.AdNotReady ||
                   reason == ProgressBlockReason.AdClosed,
            ProgressArea.ContentAccess
                => reason == ProgressBlockReason.ContentLocked ||
                   reason == ProgressBlockReason.TimerLocked,
            ProgressArea.LevelSequence
                => reason == ProgressBlockReason.PreviousLocked,
            ProgressArea.AdEntry
                => reason == ProgressBlockReason.RewardedRequired ||
                   reason == ProgressBlockReason.AdNotReady ||
                   reason == ProgressBlockReason.AdLoadFailed ||
                   reason == ProgressBlockReason.AdShowFailed ||
                   reason == ProgressBlockReason.AdClosed,
            ProgressArea.PurchaseUnlock
                => reason == ProgressBlockReason.IapNotReady ||
                   reason == ProgressBlockReason.ProductUnavailable ||
                   reason == ProgressBlockReason.IapFailed ||
                   reason == ProgressBlockReason.PurchaseCancelled,
            _ => false
        };
        if (!valid) throw new ArgumentException("Invalid progress_blocked area/reason combination: " + area + "/" + reason + ".");
    }

    private static void ValidateResolved(ProgressArea area, ProgressResolutionMethod method, ProgressTarget target)
    {
        ValidateTarget(area, target);
        bool valid = area switch
        {
            ProgressArea.LevelStart or ProgressArea.LevelRestart or ProgressArea.NextLevel
                => method == ProgressResolutionMethod.Coins ||
                   method == ProgressResolutionMethod.RewardedAd ||
                   method == ProgressResolutionMethod.NaturalRefill,
            ProgressArea.Revive
                => method == ProgressResolutionMethod.Coins ||
                   method == ProgressResolutionMethod.RewardedAd,
            ProgressArea.ContentAccess
                => method == ProgressResolutionMethod.SinglePurchase ||
                   method == ProgressResolutionMethod.ContentPack ||
                   method == ProgressResolutionMethod.TimerElapsed,
            ProgressArea.LevelSequence
                => method == ProgressResolutionMethod.PreviousCompleted,
            ProgressArea.AdEntry
                => method == ProgressResolutionMethod.RewardedAd,
            ProgressArea.PurchaseUnlock
                => method == ProgressResolutionMethod.PurchaseRetry,
            _ => false
        };
        if (!valid) throw new ArgumentException("Invalid progress_resolved area/method combination: " + area + "/" + method + ".");
    }

    private static void ValidateTarget(ProgressArea area, ProgressTarget target)
    {
        if (string.IsNullOrEmpty(target.TypeName) || string.IsNullOrEmpty(target.Id))
            throw new ArgumentException("Use ProgressTarget factory methods to create a target.", nameof(target));

    }

    private static string AreaName(ProgressArea value) => value switch
    {
        ProgressArea.LevelStart => "level_start",
        ProgressArea.LevelRestart => "level_restart",
        ProgressArea.NextLevel => "next_level",
        ProgressArea.Revive => "revive",
        ProgressArea.ContentAccess => "content_access",
        ProgressArea.LevelSequence => "level_sequence",
        ProgressArea.AdEntry => "ad_entry",
        ProgressArea.PurchaseUnlock => "purchase_unlock",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    private static string ReasonName(ProgressBlockReason value) => value switch
    {
        ProgressBlockReason.NoHearts => "no_hearts",
        ProgressBlockReason.AttemptsExhausted => "attempts_exhausted",
        ProgressBlockReason.NoCoins => "no_coins",
        ProgressBlockReason.AdNotReady => "ad_not_ready",
        ProgressBlockReason.AdClosed => "ad_closed",
        ProgressBlockReason.ContentLocked => "content_locked",
        ProgressBlockReason.TimerLocked => "timer_locked",
        ProgressBlockReason.PreviousLocked => "previous_locked",
        ProgressBlockReason.RewardedRequired => "rewarded_required",
        ProgressBlockReason.AdLoadFailed => "ad_load_failed",
        ProgressBlockReason.AdShowFailed => "ad_show_failed",
        ProgressBlockReason.IapNotReady => "iap_not_ready",
        ProgressBlockReason.ProductUnavailable => "product_unavailable",
        ProgressBlockReason.IapFailed => "iap_failed",
        ProgressBlockReason.PurchaseCancelled => "purchase_cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    private static string MethodName(ProgressResolutionMethod value) => value switch
    {
        ProgressResolutionMethod.Coins => "coins",
        ProgressResolutionMethod.RewardedAd => "rewarded_ad",
        ProgressResolutionMethod.NaturalRefill => "natural_refill",
        ProgressResolutionMethod.SinglePurchase => "single_purchase",
        ProgressResolutionMethod.ContentPack => "content_pack",
        ProgressResolutionMethod.TimerElapsed => "timer_elapsed",
        ProgressResolutionMethod.PreviousCompleted => "previous_completed",
        ProgressResolutionMethod.PurchaseRetry => "purchase_retry",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
