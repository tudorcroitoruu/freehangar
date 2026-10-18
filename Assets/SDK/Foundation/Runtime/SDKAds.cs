using System;
using GameAnalyticsSDK;
using Unity.Services.LevelPlay;
using UnityEngine;

public enum RewardedAdOutcome
{
    Rewarded,
    NotReady,
    LoadFailed,
    ShowFailed,
    Closed
}

internal sealed class SDKAds : MonoBehaviour
{
    private const float RewardedCloseDelay = 0.5f;
    private const float RewardedLoadTimeout = 15f;
    private const float TenjinSubscriptionRetryDelay = 1f;
    private SDKConfig config;
    private SDKAnalytics analytics;
    private SDKPrivacyAttribution attribution;
    private LevelPlayRewardedAd rewarded;
    private LevelPlayInterstitialAd interstitial;
    private LevelPlayBannerAd banner;
    private Action<RewardedAdOutcome> rewardedCallback;
    private Action interstitialCallback;
    private string rewardedPlacement;
    private string interstitialPlacement;
    private bool rewardGranted;
    private bool showRewardedAfterLoad;
    private float rewardedLoadDeadline;
    private bool tenjinRewardedSubscribed;
    private bool tenjinInterstitialSubscribed;
    private bool tenjinBannerSubscribed;
    private float nextTenjinSubscriptionAttempt;

    public bool IsInitialized { get; private set; }
    public bool IsRewardedReady => rewarded != null && rewarded.IsAdReady();
    public bool IsInterstitialReady => !SaveSystem.State.adsRemoved && interstitial != null && interstitial.IsAdReady();

    public void Initialize(SDKConfig sdkConfig, SDKAnalytics sdkAnalytics, SDKPrivacyAttribution sdkAttribution)
    {
        config = sdkConfig;
        analytics = sdkAnalytics;
        attribution = sdkAttribution;
        LevelPlay.OnInitSuccess += OnInitialized;
        LevelPlay.OnInitFailed += OnInitializationFailed;
        LevelPlay.ValidateIntegration();
        LevelPlay.Init(Current.appKey);
    }

    public void ShowRewarded(string placement, Action<bool> onCompleted)
        => ShowRewardedDetailed(
            placement,
            false,
            outcome => onCompleted?.Invoke(outcome == RewardedAdOutcome.Rewarded));

    public void ShowRewardedDetailed(
        string placement,
        bool waitForLoad,
        Action<RewardedAdOutcome> onCompleted)
    {
        if (rewardedCallback != null)
        {
            onCompleted?.Invoke(RewardedAdOutcome.NotReady);
            return;
        }

        if (!IsRewardedReady)
        {
            if (rewarded == null || !waitForLoad)
            {
                analytics.Ad(GAAdAction.FailedShow, GAAdType.RewardedVideo, placement);
                onCompleted?.Invoke(RewardedAdOutcome.NotReady);
                rewarded?.LoadAd();
                return;
            }

            rewardedPlacement = placement;
            rewardedCallback = onCompleted;
            rewardGranted = false;
            showRewardedAfterLoad = true;
            rewardedLoadDeadline = Time.realtimeSinceStartup + RewardedLoadTimeout;
            rewarded.LoadAd();
            return;
        }

        rewardedPlacement = placement;
        rewardedCallback = onCompleted;
        rewardGranted = false;
        ShowReadyRewarded();
    }

    private void ShowReadyRewarded()
    {
        rewardedLoadDeadline = 0f;
        showRewardedAfterLoad = false;
        analytics.Ad(GAAdAction.Request, GAAdType.RewardedVideo, rewardedPlacement);
        try { rewarded.ShowAd(rewardedPlacement); }
        catch (Exception exception)
        {
            Debug.LogWarning("[SDK] Rewarded show failed: " + exception.Message);
            FinishRewarded(RewardedAdOutcome.ShowFailed, GAAdAction.FailedShow);
        }
    }

    public void ShowInterstitial(string placement, Action onClosed)
    {
        if (SaveSystem.State.adsRemoved)
        {
            onClosed?.Invoke();
            return;
        }

        if (!IsInterstitialReady)
        {
            analytics.Ad(GAAdAction.FailedShow, GAAdType.Interstitial, placement);
            onClosed?.Invoke();
            interstitial?.LoadAd();
            return;
        }
        interstitialPlacement = placement;
        interstitialCallback = onClosed;
        analytics.Ad(GAAdAction.Request, GAAdType.Interstitial, placement);
        interstitial.ShowAd(placement);
    }

    public void ShowBanner(bool top = false, string placement = "banner")
    {
        if (!IsInitialized || SaveSystem.State.adsRemoved) return;
        if (banner == null)
        {
            var builder = new LevelPlayBannerAd.Config.Builder()
                .SetSize(LevelPlayAdSize.BANNER)
                .SetPosition(top ? LevelPlayBannerPosition.TopCenter : LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(true)
                .SetRespectSafeArea(true)
                .SetPlacementName(placement);
            banner = new LevelPlayBannerAd(Current.bannerAdUnitId, builder.Build());
            banner.OnAdLoaded += info => analytics.Ad(GAAdAction.Loaded, GAAdType.Banner, placement);
            banner.OnAdDisplayed += info => analytics.Ad(GAAdAction.Show, GAAdType.Banner, placement);
            banner.OnAdClicked += info => analytics.Ad(GAAdAction.Clicked, GAAdType.Banner, placement);
            banner.OnAdDisplayFailed += (info, error) => analytics.Ad(GAAdAction.FailedShow, GAAdType.Banner, placement);
            banner.OnAdLoadFailed += error => Debug.LogWarning("[SDK] Banner load failed: " + error);
            tenjinBannerSubscribed = false;
            SubscribeTenjinAdRevenue();
        }
        banner.LoadAd();
    }

    public void HideBanner()
    {
        banner?.DestroyAd();
        banner = null;
        tenjinBannerSubscribed = false;
    }

    private void OnInitialized(LevelPlayConfiguration value)
    {
        IsInitialized = true;
        rewarded = new LevelPlayRewardedAd(Current.rewardedAdUnitId);
        interstitial = new LevelPlayInterstitialAd(Current.interstitialAdUnitId);

        rewarded.OnAdLoaded += info =>
        {
            analytics.Ad(GAAdAction.Loaded, GAAdType.RewardedVideo, rewardedPlacement);
            if (showRewardedAfterLoad && rewardedCallback != null) ShowReadyRewarded();
        };
        rewarded.OnAdLoadFailed += error =>
        {
            Debug.LogWarning("[SDK] Rewarded load failed: " + error);
            if (showRewardedAfterLoad && rewardedCallback != null)
                FinishRewarded(RewardedAdOutcome.LoadFailed, GAAdAction.Undefined);
        };
        rewarded.OnAdDisplayed += info => analytics.Ad(GAAdAction.Show, GAAdType.RewardedVideo, rewardedPlacement);
        rewarded.OnAdClicked += info => analytics.Ad(GAAdAction.Clicked, GAAdType.RewardedVideo, rewardedPlacement);
        rewarded.OnAdDisplayFailed += (info, error) =>
        {
            FinishRewarded(RewardedAdOutcome.ShowFailed, GAAdAction.FailedShow);
            rewarded.LoadAd();
        };
        rewarded.OnAdRewarded += (info, reward) =>
        {
            if (rewardGranted) { Debug.LogWarning("[SDK] Ignoring duplicate rewarded callback for " + rewardedPlacement); return; }
            rewardGranted = true;
            CancelInvoke(nameof(FinishClosedWithoutReward));
            FinishRewarded(RewardedAdOutcome.Rewarded, GAAdAction.RewardReceived);
        };
        rewarded.OnAdClosed += info =>
        {
            if (rewardedCallback != null && !rewardGranted)
            {
                CancelInvoke(nameof(FinishClosedWithoutReward));
                Invoke(nameof(FinishClosedWithoutReward), RewardedCloseDelay);
            }
            rewarded.LoadAd();
        };

        interstitial.OnAdLoaded += info => analytics.Ad(GAAdAction.Loaded, GAAdType.Interstitial, interstitialPlacement);
        interstitial.OnAdLoadFailed += error => Debug.LogWarning("[SDK] Interstitial load failed: " + error);
        interstitial.OnAdDisplayed += info => analytics.Ad(GAAdAction.Show, GAAdType.Interstitial, interstitialPlacement);
        interstitial.OnAdClicked += info => analytics.Ad(GAAdAction.Clicked, GAAdType.Interstitial, interstitialPlacement);
        interstitial.OnAdDisplayFailed += (info, error) => FinishInterstitial(true);
        interstitial.OnAdClosed += info => FinishInterstitial(false);

        tenjinRewardedSubscribed = false;
        tenjinInterstitialSubscribed = false;
        SubscribeTenjinAdRevenue();
        rewarded.LoadAd();
        interstitial.LoadAd();

#if UNITY_EDITOR
        AttachEditorObject("MockRewardedEditorAd(Clone)");
        AttachEditorObject("MockInterstitialEditorAd(Clone)");
        AttachEditorObject("[Debug Updater]");
#endif
    }

    private void OnInitializationFailed(LevelPlayInitError error)
    {
        Debug.LogError("[SDK] LevelPlay initialization failed: " + error);
    }

    private void FinishRewarded(RewardedAdOutcome outcome, GAAdAction action)
    {
        CancelInvoke(nameof(FinishClosedWithoutReward));
        showRewardedAfterLoad = false;
        rewardedLoadDeadline = 0f;
        if (action != GAAdAction.Undefined) analytics.Ad(action, GAAdType.RewardedVideo, rewardedPlacement);
        var callback = rewardedCallback;
        rewardedCallback = null;
        callback?.Invoke(outcome);
    }

    private void FinishClosedWithoutReward() => FinishRewarded(RewardedAdOutcome.Closed, GAAdAction.Undefined);

    private void Update()
    {
        if (NeedsTenjinAdRevenueSubscription() &&
            Time.realtimeSinceStartup >= nextTenjinSubscriptionAttempt)
        {
            nextTenjinSubscriptionAttempt = Time.realtimeSinceStartup + TenjinSubscriptionRetryDelay;
            SubscribeTenjinAdRevenue();
        }

        if (showRewardedAfterLoad &&
            rewardedCallback != null &&
            rewardedLoadDeadline > 0f &&
            Time.realtimeSinceStartup >= rewardedLoadDeadline)
            FinishRewarded(RewardedAdOutcome.LoadFailed, GAAdAction.Undefined);
    }

    private bool NeedsTenjinAdRevenueSubscription()
    {
        return (rewarded != null && !tenjinRewardedSubscribed) ||
               (interstitial != null && !tenjinInterstitialSubscribed) ||
               (banner != null && !tenjinBannerSubscribed);
    }

    private void SubscribeTenjinAdRevenue()
    {
        BaseTenjin tenjin = attribution?.Tenjin;
        if (tenjin == null) return;

        if (rewarded != null && !tenjinRewardedSubscribed)
        {
            tenjin.SubscribeLevelPlayRewardedAdImpressions(rewarded);
            tenjinRewardedSubscribed = true;
            Debug.Log("[Tenjin] LevelPlay rewarded ad revenue tracking enabled.");
        }

        if (interstitial != null && !tenjinInterstitialSubscribed)
        {
            tenjin.SubscribeLevelPlayInterstitialAdImpressions(interstitial);
            tenjinInterstitialSubscribed = true;
            Debug.Log("[Tenjin] LevelPlay interstitial ad revenue tracking enabled.");
        }

        if (banner != null && !tenjinBannerSubscribed)
        {
            tenjin.SubscribeLevelPlayBannerAdImpressions(banner);
            tenjinBannerSubscribed = true;
            Debug.Log("[Tenjin] LevelPlay banner ad revenue tracking enabled.");
        }
    }

    private void FinishInterstitial(bool failed)
    {
        if (failed) analytics.Ad(GAAdAction.FailedShow, GAAdType.Interstitial, interstitialPlacement);
        var callback = interstitialCallback;
        interstitialCallback = null;
        callback?.Invoke();
        interstitial?.LoadAd();
    }

    private AdPlatformConfig Current
    {
        get
        {
            return config.levelPlay.ios;
        }
    }

#if UNITY_EDITOR
    private void AttachEditorObject(string objectName)
    {
        var sdkObject = GameObject.Find(objectName);
        if (sdkObject != null) sdkObject.transform.SetParent(transform, false);
    }
#endif

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= OnInitialized;
        LevelPlay.OnInitFailed -= OnInitializationFailed;
        rewarded?.DestroyAd();
        interstitial?.DestroyAd();
        banner?.DestroyAd();
    }
}
