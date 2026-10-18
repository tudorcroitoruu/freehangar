using System;
using System.Collections.Generic;
using UnityEngine;

public static class SDKManager
{
    private static SDKRuntime runtime;
    public static event Action<IAPPurchaseResult> PurchaseCompleted;

    public static bool IsReady => runtime != null && runtime.IsReady;
    public static SDKConfig Config => Require().Config;
    public static PlayerData Data => SaveSystem.Data;
    public static PlayerData PlayerData => SaveSystem.Data;
    public static bool IsRewardedReady => Require().Ads.IsRewardedReady;
    public static bool IsInterstitialReady => Require().Ads.IsInterstitialReady;
    public static bool IsSubscribed() => Require().IAP.HasActiveSubscription;
    public static void CheckSubscription(Action<bool> callback) => Require().IAP.CheckSubscription(callback);

    public static void StartLevel(int level) => RequireLevel(level).Analytics.StartLevel(level);
    public static void CompleteLevel(int level)
    {
        var value = RequireLevel(level);
        value.Analytics.CompleteLevel(level);
        SaveSystem.State.highestCompletedLevel = Math.Max(SaveSystem.State.highestCompletedLevel, level);
        SaveSystem.Save();
        value.Progress.Resolve(
            ProgressArea.LevelSequence,
            ProgressResolutionMethod.PreviousCompleted,
            LevelTarget(level + 1));
    }
    public static void FailLevel(int level) => RequireLevel(level).Analytics.FailLevel(level);

    public static void ShowRevivePopup() => Require().Analytics.Design("gameplay:revive:popup_shown");

    public static void RegisterProgressGate(ProgressArea area, ProgressTarget target)
        => Require().Progress.Register(area, target);

    public static void ProgressBlocked(ProgressArea area, ProgressBlockReason reason, ProgressTarget target)
        => Require().Progress.Block(area, reason, target);

    public static bool ProgressResolved(ProgressArea area, ProgressResolutionMethod method, ProgressTarget target)
        => Require().Progress.Resolve(area, method, target);

    public static void LevelEntryBlockedByNoHearts(LevelEntryAction action, int targetLevel)
        => ProgressBlocked(EntryArea(action), ProgressBlockReason.NoHearts, LevelTarget(targetLevel));

    public static bool ResolveLevelEntry(
        LevelEntryAction action,
        ProgressResolutionMethod method,
        int targetLevel,
        Action makeHeartAvailable)
    {
        if (makeHeartAvailable == null) throw new ArgumentNullException(nameof(makeHeartAvailable));
        makeHeartAvailable.Invoke();
        return ProgressResolved(EntryArea(action), method, LevelTarget(targetLevel));
    }

    public static bool ResolveNaturalHeartRefill(
        LevelEntryAction action,
        int targetLevel,
        int previousHearts,
        int currentHearts)
    {
        if (previousHearts != 0 || currentHearts != 1) return false;
        return ProgressResolved(
            EntryArea(action),
            ProgressResolutionMethod.NaturalRefill,
            LevelTarget(targetLevel));
    }

    public static void ReviveAttemptsExhausted(int level)
        => ProgressBlocked(
            ProgressArea.Revive,
            ProgressBlockReason.AttemptsExhausted,
            LevelTarget(level));

    public static void ContentAccessBlocked(ProgressTarget target, ProgressBlockReason reason)
        => ProgressBlocked(ProgressArea.ContentAccess, reason, target);

    public static bool ResolveContentAccess(
        ProgressTarget target,
        ProgressResolutionMethod method,
        Action grantAccess)
    {
        if (grantAccess == null) throw new ArgumentNullException(nameof(grantAccess));
        grantAccess.Invoke();
        return ProgressResolved(ProgressArea.ContentAccess, method, target);
    }

    public static int UnlockContentPack(
        IEnumerable<ProgressTarget> targets,
        Func<ProgressTarget, bool> isAlreadyOwned,
        Action<ProgressTarget> grantAccess)
    {
        if (targets == null) throw new ArgumentNullException(nameof(targets));
        if (isAlreadyOwned == null) throw new ArgumentNullException(nameof(isAlreadyOwned));
        if (grantAccess == null) throw new ArgumentNullException(nameof(grantAccess));

        int resolved = 0;
        var handled = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProgressTarget target in targets)
        {
            if (!handled.Add(target.ToString()) || isAlreadyOwned.Invoke(target)) continue;
            Require().Progress.Register(ProgressArea.ContentAccess, target);
            grantAccess.Invoke(target);
            if (Require().Progress.Resolve(
                    ProgressArea.ContentAccess,
                    ProgressResolutionMethod.ContentPack,
                    target))
                resolved++;
        }
        return resolved;
    }

    public static void LevelSequenceBlocked(ProgressTarget target)
        => ProgressBlocked(ProgressArea.LevelSequence, ProgressBlockReason.PreviousLocked, target);

    public static bool ResolveLevelSequence(ProgressTarget target, Action grantAccess)
    {
        if (grantAccess == null) throw new ArgumentNullException(nameof(grantAccess));
        grantAccess.Invoke();
        return ProgressResolved(
            ProgressArea.LevelSequence,
            ProgressResolutionMethod.PreviousCompleted,
            target);
    }

    public static void ReviveWithRewardedAd(
        RewardPlacement placement,
        Action revivePlayer,
        Action onAdUnavailable = null)
    {
        if (revivePlayer == null) throw new ArgumentNullException(nameof(revivePlayer));
        Require().Ads.ShowRewardedDetailed(placement.ToString(), false, outcome =>
        {
            int level = SaveSystem.State.currentLevel;
            if (outcome != RewardedAdOutcome.Rewarded)
            {
                if (level > 0)
                    Require().Progress.Block(
                        ProgressArea.Revive,
                        outcome == RewardedAdOutcome.Closed
                            ? ProgressBlockReason.AdClosed
                            : ProgressBlockReason.AdNotReady,
                        LevelTarget(level));
                onAdUnavailable?.Invoke();
                return;
            }

            RevivePlayer(revivePlayer);
            if (level > 0)
                Require().Progress.Resolve(
                    ProgressArea.Revive,
                    ProgressResolutionMethod.RewardedAd,
                    LevelTarget(level));
        });
    }

    public static bool TryReviveWithCurrency(bool hasEnoughCurrency, Action spendCurrency, Action revivePlayer)
    {
        if (spendCurrency == null) throw new ArgumentNullException(nameof(spendCurrency));
        if (revivePlayer == null) throw new ArgumentNullException(nameof(revivePlayer));
        if (!hasEnoughCurrency)
        {
            Require().Analytics.Design("gameplay:revive:not_enough_money");
            if (SaveSystem.State.currentLevel > 0)
                Require().Progress.Block(
                    ProgressArea.Revive,
                    ProgressBlockReason.NoCoins,
                    LevelTarget(SaveSystem.State.currentLevel));
            return false;
        }

        spendCurrency.Invoke();
        RevivePlayer(revivePlayer);
        if (SaveSystem.State.currentLevel > 0)
            Require().Progress.Resolve(
                ProgressArea.Revive,
                ProgressResolutionMethod.Coins,
                LevelTarget(SaveSystem.State.currentLevel));
        return true;
    }

    public static void SkipRevive() => Require().Analytics.Design("gameplay:revive:skipped");

    public static void RevivePopupShown() => ShowRevivePopup();
    public static void ReviveUsed() => RevivePlayer(null);
    public static void ReviveNotEnoughMoney() => Require().Analytics.Design("gameplay:revive:not_enough_money");
    public static void ReviveSkipped() => SkipRevive();

    public static void TrackEvent(string eventName, Dictionary<string, object> fields = null)
        => Require().Analytics.Design(eventName, fields);

    public static void PurchaseIAP(IAPProduct product, Action<IAPPurchaseResult> callback)
        => Require().IAP.Purchase(product.Id, callback);

    public static void PurchaseNoAds()
        => PurchaseIAP(IAPProduct.noads, (Action<IAPPurchaseResult>)null);

    public static void PurchaseNoAds(Action<IAPPurchaseResult> onCompleted)
        => PurchaseIAP(IAPProduct.noads, onCompleted);

    public static void PurchaseNoAds(Action onSuccess)
        => PurchaseIAP(IAPProduct.noads, onSuccess);

    public static void PurchaseNoAds(Action onSuccess, Action<IAPPurchaseResult> onFailed)
        => PurchaseIAP(IAPProduct.noads, onSuccess, onFailed);

    public static void PurchaseContentUnlock(
        IAPProduct product,
        ProgressTarget target,
        Action grantAccess,
        Action<IAPPurchaseResult> callback = null)
    {
        if (grantAccess == null) throw new ArgumentNullException(nameof(grantAccess));
        Require().IAP.Purchase(product.Id, result =>
        {
            if (!result.Succeeded)
            {
                Require().Progress.Block(
                    ProgressArea.PurchaseUnlock,
                    PurchaseFailureReason(result.Status),
                    target);
                callback?.Invoke(result);
                return;
            }

            grantAccess.Invoke();
            Require().Progress.Resolve(
                ProgressArea.ContentAccess,
                ProgressResolutionMethod.SinglePurchase,
                target);
            Require().Progress.Resolve(
                ProgressArea.PurchaseUnlock,
                ProgressResolutionMethod.PurchaseRetry,
                target);
            callback?.Invoke(result);
        });
    }
    public static void PurchaseIAP(IAPProduct product, Action onSuccess)
        => PurchaseIAP(product, onSuccess, null);

    public static void PurchaseIAP(
        IAPProduct product,
        Action onSuccess,
        Action<IAPPurchaseResult> onFailed)
        => Require().IAP.Purchase(product.Id, result =>
        {
            if (result.Succeeded) onSuccess?.Invoke();
            else
            {
                onFailed?.Invoke(result);
                Debug.LogWarning("[SDK] Purchase failed: " + product + " / " + result.Status);
            }
        });
    public static string GetLocalizedIAPPrice(IAPProduct product) => Require().IAP.LocalizedPrice(product.Id);
    public static string GetPrice(IAPProduct product) => Require().IAP.LocalizedPrice(product.Id);
    public static bool HasIAPReceipt(IAPProduct product) => Require().IAP.HasReceipt(product.Id);
    public static bool IsPurchased(IAPProduct product) => Require().IAP.HasReceipt(product.Id);
    public static void RestorePurchases(Action<bool> callback) => Require().IAP.Restore(callback);
    public static void RestorePurchases() => Require().IAP.Restore(success =>
        Debug.Log("[SDK IAP] Restore finished. Success: " + success + "."));
    public static void RequestNotificationPermission() => Require().Notifications.RequestPermission();
    public static void RequestRateUs() => Require().Reviews.RequestNow();
    public static void OpenTerms() => Application.OpenURL(Require().Config.storeLinks.termsUrl);
    public static void OpenPrivacyPolicy() => Application.OpenURL(Require().Config.storeLinks.privacyPolicyUrl);

    public static bool HapticsEnabled => !SaveSystem.State.hapticsDisabled;
    public static bool HapticsSupported => SDKHaptics.IsSupported;
    public static void SetHapticsEnabled(bool enabled)
    {
        SaveSystem.State.hapticsDisabled = !enabled;
        if (!enabled) SDKHaptics.Stop();
        SaveSystem.Save();
    }
    public static void Haptic(HapticPreset preset) => SDKHaptics.Play(preset);
    public static void HapticButton() => Haptic(HapticPreset.Button);
    public static void HapticSelection() => Haptic(HapticPreset.Selection);
    public static void HapticToggle() => Haptic(HapticPreset.Toggle);
    public static void HapticSuccess() => Haptic(HapticPreset.Success);
    public static void HapticWarning() => Haptic(HapticPreset.Warning);
    public static void HapticError() => Haptic(HapticPreset.Error);
    public static void HapticExplosion() => Haptic(HapticPreset.Explosion);
    public static void HapticLightImpact() => Haptic(HapticPreset.LightImpact);
    public static void HapticMediumImpact() => Haptic(HapticPreset.MediumImpact);
    public static void HapticHeavyImpact() => Haptic(HapticPreset.HeavyImpact);
    public static void HapticSoftImpact() => Haptic(HapticPreset.SoftImpact);
    public static void HapticRigidImpact() => Haptic(HapticPreset.RigidImpact);
    public static void HapticCollision() => Haptic(HapticPreset.Collision);
    public static void HapticReward() => Haptic(HapticPreset.Reward);
    public static void HapticHeartbeat() => Haptic(HapticPreset.Heartbeat);
    public static void HapticTick() => Haptic(HapticPreset.Tick);
    public static void HapticLongPress() => Haptic(HapticPreset.LongPress);
    public static void HapticContinuousLight() => Haptic(HapticPreset.ContinuousLight);
    public static void HapticContinuousMedium() => Haptic(HapticPreset.ContinuousMedium);
    public static void HapticContinuousHeavy() => Haptic(HapticPreset.ContinuousHeavy);
    public static void HapticContinuous(float duration = 0.8f, float intensity = 0.6f, float sharpness = 0.5f)
        => SDKHaptics.PlayContinuous(duration, intensity, sharpness);
    public static void StopHaptics() => SDKHaptics.Stop();

    public static void ShowRewarded(RewardPlacement placement, Action<bool> onCompleted)
        => Require().Ads.ShowRewarded(placement.ToString(), onCompleted);
    public static void ShowRewarded(RewardPlacement placement, Action onReward)
        => Require().Ads.ShowRewarded(placement.ToString(), granted => { if (granted) onReward?.Invoke(); });
    public static void ShowRewardedDetailed(
        RewardPlacement placement,
        bool waitForLoad,
        Action<RewardedAdOutcome> onCompleted)
        => Require().Ads.ShowRewardedDetailed(placement.ToString(), waitForLoad, onCompleted);

    public static void ShowAdEntryRewarded(
        RewardPlacement placement,
        ProgressTarget target,
        Action grantAccess,
        Action<RewardedAdOutcome> onCompleted = null)
    {
        if (grantAccess == null) throw new ArgumentNullException(nameof(grantAccess));
        Require().Progress.Block(ProgressArea.AdEntry, ProgressBlockReason.RewardedRequired, target);
        Require().Ads.ShowRewardedDetailed(placement.ToString(), true, outcome =>
        {
            if (outcome == RewardedAdOutcome.Rewarded)
            {
                grantAccess.Invoke();
                Require().Progress.Resolve(
                    ProgressArea.AdEntry,
                    ProgressResolutionMethod.RewardedAd,
                    target);
            }
            else
            {
                Require().Progress.Block(ProgressArea.AdEntry, AdEntryFailureReason(outcome), target);
            }
            onCompleted?.Invoke(outcome);
        });
    }
    public static void ShowInterstitial(InterstitialPlacement placement, Action onClosed = null)
        => Require().Ads.ShowInterstitial(placement.ToString(), onClosed);
    public static void ShowBanner(BannerPlacement placement, bool top = false)
        => Require().Ads.ShowBanner(top, placement.ToString());
    public static void HideBanner() => Require().Ads.HideBanner();
    public static bool AdsDisabled => SaveSystem.State.adsRemoved;
    public static void DisableAds()
    {
        SaveSystem.State.adsRemoved = true;
        SaveSystem.Save();
        runtime?.Ads?.HideBanner();
    }

    public static bool Save() => SaveSystem.Save();
    public static PlayerData Load() => SaveSystem.Load();
    public static string FormatLevel(int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Levels must start at 1.");
        return "level_" + level.ToString("0000");
    }

    private static ProgressTarget LevelTarget(int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Levels must start at 1.");
        return ProgressTarget.Create("level", "level_" + level.ToString("000"));
    }

    private static void RevivePlayer(Action revivePlayer)
    {
        Require().Analytics.Design("gameplay:revive:revived");
        revivePlayer?.Invoke();
    }

    private static ProgressArea EntryArea(LevelEntryAction action) => action switch
    {
        LevelEntryAction.Start => ProgressArea.LevelStart,
        LevelEntryAction.Restart => ProgressArea.LevelRestart,
        LevelEntryAction.NextLevel => ProgressArea.NextLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private static ProgressBlockReason PurchaseFailureReason(IAPPurchaseStatus status) => status switch
    {
        IAPPurchaseStatus.NotInitialized => ProgressBlockReason.IapNotReady,
        IAPPurchaseStatus.ProductUnavailable => ProgressBlockReason.ProductUnavailable,
        IAPPurchaseStatus.Cancelled => ProgressBlockReason.PurchaseCancelled,
        _ => ProgressBlockReason.IapFailed
    };

    private static ProgressBlockReason AdEntryFailureReason(RewardedAdOutcome outcome) => outcome switch
    {
        RewardedAdOutcome.NotReady => ProgressBlockReason.AdNotReady,
        RewardedAdOutcome.LoadFailed => ProgressBlockReason.AdLoadFailed,
        RewardedAdOutcome.ShowFailed => ProgressBlockReason.AdShowFailed,
        RewardedAdOutcome.Closed => ProgressBlockReason.AdClosed,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    internal static void Bind(SDKRuntime value) => runtime = value;
    internal static bool NotifyPurchase(IAPPurchaseResult result)
    {
        if (PurchaseCompleted == null) return false;
        bool succeeded = true;
        foreach (Action<IAPPurchaseResult> handler in PurchaseCompleted.GetInvocationList())
        {
            try { handler.Invoke(result); }
            catch (Exception exception) { succeeded = false; Debug.LogException(exception); }
        }
        return succeeded;
    }

    private static SDKRuntime Require()
    {
        if (!IsReady) throw new InvalidOperationException("SDKManager is not ready. Check the on-screen SDK configuration error and Console.");
        return runtime;
    }

    private static SDKRuntime RequireLevel(int level)
    {
        FormatLevel(level);
        return Require();
    }
}

[DefaultExecutionOrder(-10000)]
internal sealed class SDKRuntime : MonoBehaviour
{
    public SDKConfig Config { get; private set; }
    public SDKAnalytics Analytics { get; private set; }
    public SDKPrivacyAttribution Attribution { get; private set; }
    public SDKAds Ads { get; private set; }
    public SDKIAP IAP { get; private set; }
    public SDKNotifications Notifications { get; private set; }
    public SDKReviewService Reviews { get; private set; }
    public SDKProgressAnalytics Progress { get; private set; }
    public bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Application.targetFrameRate = 60;

        if (FindFirstObjectByType<SDKRuntime>() != null) return;

        var gameAnalytics = FindFirstObjectByType<GameAnalyticsSDK.GameAnalytics>();
        if (gameAnalytics == null)
        {
            var analyticsObject = new GameObject("GameAnalytics");
            gameAnalytics = analyticsObject.AddComponent<GameAnalyticsSDK.GameAnalytics>();
        }

        gameAnalytics.gameObject.name = "GameAnalytics";
        var sdkObject = new GameObject("SDKManager");
        DontDestroyOnLoad(sdkObject);
        gameAnalytics.transform.SetParent(sdkObject.transform, false);
        sdkObject.AddComponent<SDKRuntime>();
    }

    private void Awake()
    {
        if (!SDKConfigLoader.TryLoad(out var loadedConfig, out var error))
        {
            Debug.LogError("[SDK] Configuration is invalid:\n" + error);
#if UNITY_EDITOR
            gameObject.AddComponent<SDKErrorOverlay>().Message = error;
#endif
            return;
        }

        Config = loadedConfig;
        SaveSystem.Load();
        SaveSystem.State.launchCount++;
        SaveSystem.Save();
        Analytics = new SDKAnalytics(Config);
        Progress = new SDKProgressAnalytics(Analytics);
        Notifications = new SDKNotifications(Config);
        Attribution = gameObject.AddComponent<SDKPrivacyAttribution>();
        Attribution.Initialize(Config, Notifications);
        Reviews = new SDKReviewService();
        Ads = gameObject.AddComponent<SDKAds>();
        Ads.Initialize(Config, Analytics, Attribution);
        IAP = new SDKIAP(Config, Analytics, Attribution);
        IsReady = true;
        SDKManager.Bind(this);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SDKHaptics.Stop();
        if (!IsReady) return;
        if (paused) SaveSystem.Save();
    }

    private void OnApplicationQuit()
    {
        if (!IsReady) return;
        SaveSystem.Save();
    }
}

#if UNITY_EDITOR
internal sealed class SDKErrorOverlay : MonoBehaviour
{
    public string Message;
    private GUIStyle title;
    private GUIStyle body;

    private void OnGUI()
    {
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        body ??= new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(24, 24, 16, 16) };
        float width = Mathf.Min(720, Screen.width - 40);
        float height = Mathf.Min(420, Screen.height - 40);
        var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 20, rect.y + 20, rect.width - 40, 50), "SDK CONFIGURATION ERROR", title);
        GUI.Label(new Rect(rect.x + 20, rect.y + 82, rect.width - 40, rect.height - 102),
            "The application cannot start because the SDK configuration is invalid.\n\nFile to fix: Assets/sdk_config.json\n\n" + Message, body);
    }
}
#endif
