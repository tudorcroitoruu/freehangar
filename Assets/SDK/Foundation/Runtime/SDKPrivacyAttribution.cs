using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Facebook.Unity;
using GameAnalyticsSDK;
using GameAnalyticsSDK.Utilities;
using UnityEngine;

internal enum TrackingAuthorizationStatus
{
    Unavailable = -1,
    NotDetermined = 0,
    Restricted = 1,
    Denied = 2,
    Authorized = 3
}

internal sealed class SDKPrivacyAttribution : MonoBehaviour, IGameAnalyticsATTListener
{
    private SDKNotifications notifications;
    private BaseTenjin tenjin;
    private bool tenjinConnected;
    private bool trackingRequestInFlight;
    private bool notificationPermissionRequested;
    private bool resumeStatusTrackingPending;
    private bool facebookInitFailureTracked;
    private bool facebookInitializationStarted;
    private Action facebookInitializedCallbacks;
    private Action facebookInitializationFailedCallbacks;
    private readonly List<MetaCheckoutEvent> pendingMetaCheckoutEvents = new();
    private readonly List<MetaPurchaseEvent> pendingMetaPurchaseEvents = new();

    private readonly struct MetaCheckoutEvent
    {
        public readonly string ProductId;
        public readonly string Currency;
        public readonly decimal Price;

        public MetaCheckoutEvent(string productId, string currency, decimal price)
        {
            ProductId = productId;
            Currency = currency;
            Price = price;
        }
    }

    private readonly struct MetaPurchaseEvent
    {
        public readonly string ProductId;
        public readonly string Currency;
        public readonly decimal Price;

        public MetaPurchaseEvent(string productId, string currency, decimal price)
        {
            ProductId = productId;
            Currency = currency;
            Price = price;
        }
    }

    public BaseTenjin Tenjin => tenjin;
    public bool IsATTAndFacebookFlowComplete { get; private set; }

    public void TrackMetaInitiatedCheckout(
        string productId,
        string currency,
        decimal localizedPrice,
        int configuredUsdCents)
    {
        decimal price = localizedPrice;
        string eventCurrency = currency;
        if (price <= 0m || string.IsNullOrWhiteSpace(eventCurrency))
        {
            price = configuredUsdCents > 0 ? configuredUsdCents / 100m : 0m;
            eventCurrency = "USD";
        }

        var checkoutEvent = new MetaCheckoutEvent(productId, eventCurrency, price);
        if (!FB.IsInitialized || !IsATTAndFacebookFlowComplete)
        {
            pendingMetaCheckoutEvents.Add(checkoutEvent);
            return;
        }

        SendMetaInitiatedCheckout(checkoutEvent);
    }

    public void TrackMetaPurchase(
        string productId,
        string currency,
        decimal localizedPrice,
        int configuredUsdCents)
    {
        decimal price = localizedPrice;
        string eventCurrency = currency;
        if (price <= 0m || string.IsNullOrWhiteSpace(eventCurrency))
        {
            price = configuredUsdCents > 0 ? configuredUsdCents / 100m : 0m;
            eventCurrency = "USD";
        }

        var purchaseEvent = new MetaPurchaseEvent(productId, eventCurrency, price);
        if (!FB.IsInitialized || !IsATTAndFacebookFlowComplete)
        {
            pendingMetaPurchaseEvents.Add(purchaseEvent);
            return;
        }

        SendMetaPurchaseIfNeeded(purchaseEvent);
    }

    public void Initialize(SDKConfig config, SDKNotifications sdkNotifications)
    {
        notifications = sdkNotifications;
        notifications.BindPrivacyFlow(() => IsATTAndFacebookFlowComplete);
        tenjin = global::Tenjin.getInstance(config.tenjin.iosApiKey);
        tenjin.gameObject.hideFlags = HideFlags.None;
        tenjin.transform.SetParent(transform, false);

#if UNITY_IOS && !UNITY_EDITOR
        RequestTrackingAuthorizationImmediately();
#else
        ConnectTenjinOnce();
        IsATTAndFacebookFlowComplete = true;
        InitializeFacebook();
#endif
    }

    private void RequestTrackingAuthorizationImmediately()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (new Version(UnityEngine.iOS.Device.systemVersion).CompareTo(new Version("14.0")) < 0)
        {
            ConnectTenjinOnce();
            InitializeAndReactivateFacebook((int)TrackingAuthorizationStatus.Authorized);
            return;
        }

        if (trackingRequestInFlight || IsATTAndFacebookFlowComplete) return;

        trackingRequestInFlight = true;
        Debug.Log("[ATT] Native status before request: " + ReadNativeStatus() + ".");
        Debug.Log("[ATT] Requesting tracking authorization.");
        GameAnalytics.RequestTrackingAuthorization(this);
#endif
    }

    public void GameAnalyticsATTListenerNotDetermined() => DispatchTrackingAuthorizationStatus(0);
    public void GameAnalyticsATTListenerRestricted() => DispatchTrackingAuthorizationStatus(1);
    public void GameAnalyticsATTListenerDenied() => DispatchTrackingAuthorizationStatus(2);
    public void GameAnalyticsATTListenerAuthorized() => DispatchTrackingAuthorizationStatus(3);

    private void DispatchTrackingAuthorizationStatus(int status)
    {
        GA_MainThreadDispatcher.RunOnMainThread(() => OnTrackingAuthorizationCompleted(status));
    }

    private void OnTrackingAuthorizationCompleted(int status)
    {
        trackingRequestInFlight = false;
        Debug.Log("[ATT] App Tracking Transparency authorization status: " + status + ".");

#if UNITY_IOS && !UNITY_EDITOR
        if (status == 0)
        {
            StartCoroutine(RetryTrackingAuthorization());
            return;
        }
#endif

        ConnectTenjinOnce();

#if UNITY_IOS && !UNITY_EDITOR
        TrackStatusWhenApplicationIsActive();
        InitializeAndReactivateFacebook(status);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    private IEnumerator RetryTrackingAuthorization()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        RequestTrackingAuthorizationImmediately();
    }

    private void InitializeAndReactivateFacebook(int trackingAuthorizationStatus)
    {
        InitializeFacebook(
            () => ReactivateFacebook(trackingAuthorizationStatus),
            () => TrackFacebookInitializationFailure(ParseTrackingAuthorizationStatus(trackingAuthorizationStatus)));
        StartCoroutine(ReleaseNotificationIfFacebookIsDelayed(trackingAuthorizationStatus));
    }

    private void TrackStatusWhenApplicationIsActive()
    {
        if (resumeStatusTrackingPending) return;
        resumeStatusTrackingPending = true;
        StartCoroutine(TrackStatusWhenApplicationIsActiveRoutine());
    }

    private IEnumerator TrackStatusWhenApplicationIsActiveRoutine()
    {
        while (TrackingAuthorizationIsApplicationActive() != 1) yield return null;
        yield return null;
        resumeStatusTrackingPending = false;
        Debug.Log("[ATT] Native status after returning to foreground: " + ReadNativeStatus() + ".");
    }

    private IEnumerator ReleaseNotificationIfFacebookIsDelayed(int trackingAuthorizationStatus)
    {
        yield return new WaitForSecondsRealtime(5f);

        if (IsATTAndFacebookFlowComplete) yield break;

        if (FB.IsInitialized)
        {
            ReactivateFacebook(trackingAuthorizationStatus);
            yield break;
        }

        Debug.LogWarning("[ATT] Facebook initialization is delayed. Releasing notification flow; Facebook reactivation remains queued.");
        TrackFacebookInitializationFailure(ParseTrackingAuthorizationStatus(trackingAuthorizationStatus));
        IsATTAndFacebookFlowComplete = true;
        RequestNotificationPermission();
    }

    private void ReactivateFacebook(int trackingAuthorizationStatus)
    {
        bool authorized = trackingAuthorizationStatus == 3;
        FB.Mobile.SetAutoLogAppEventsEnabled(true);
        FB.Mobile.SetAdvertiserIDCollectionEnabled(authorized);

        string syncMode = "automatic";
        if (new Version(UnityEngine.iOS.Device.systemVersion).CompareTo(new Version("17.0")) < 0)
        {
            bool resultingState = FB.Mobile.SetAdvertiserTrackingEnabled(authorized);
            syncMode = resultingState == authorized ? "applied" : "mismatch";
        }

        FB.ActivateApp();
        FlushMetaCommerceEvents();
        FB.LogAppEvent(
            "att_status_resolved",
            parameters: new Dictionary<string, object>
            {
                ["status"] = trackingAuthorizationStatus.ToString(),
                ["authorized"] = authorized ? "1" : "0",
                ["sync_mode"] = syncMode,
                ["ios_version"] = UnityEngine.iOS.Device.systemVersion
            });

        Debug.Log(
            "[ATT] Facebook reactivated: status=" + trackingAuthorizationStatus +
            ", authorized=" + authorized +
            ", advertiser_id_collection=" + authorized +
            ", auto_log_events=True, sync=" + syncMode + ".");
        IsATTAndFacebookFlowComplete = true;
        RequestNotificationPermission();
    }

    private static TrackingAuthorizationStatus ReadNativeStatus()
        => ParseTrackingAuthorizationStatus(TrackingAuthorizationGetStatus());

    [DllImport("__Internal")]
    private static extern int TrackingAuthorizationGetStatus();

    [DllImport("__Internal")]
    private static extern int TrackingAuthorizationIsApplicationActive();
#endif

    private void InitializeFacebook(Action onInitialized = null, Action onInitializationFailed = null)
    {
        if (onInitialized != null) facebookInitializedCallbacks += onInitialized;
        if (onInitializationFailed != null) facebookInitializationFailedCallbacks += onInitializationFailed;

        if (FB.IsInitialized)
        {
            CompleteFacebookInitialization();
            return;
        }

        if (facebookInitializationStarted) return;

        facebookInitializationStarted = true;
        Debug.Log("[ATT] Starting Facebook SDK initialization after ATT status resolved.");
        FB.Init(OnFacebookInitialized, OnHideUnity);
    }

    private void OnFacebookInitialized()
    {
        facebookInitializationStarted = false;
        if (FB.IsInitialized)
        {
            CompleteFacebookInitialization();
            return;
        }

        Debug.LogError("[ATT] Failed to initialize the Facebook SDK.");
        var callbacks = facebookInitializationFailedCallbacks;
        facebookInitializationFailedCallbacks = null;
        callbacks?.Invoke();
    }

    private void CompleteFacebookInitialization()
    {
#if !UNITY_IOS || UNITY_EDITOR
        FB.ActivateApp();
        FlushMetaCommerceEvents();
#endif
        var facebookObject = GameObject.Find("UnityFacebookSDKPlugin");
        if (facebookObject != null) facebookObject.transform.SetParent(transform, false);
        Debug.Log("[ATT] Facebook SDK initialized.");
        var callbacks = facebookInitializedCallbacks;
        facebookInitializedCallbacks = null;
        facebookInitializationFailedCallbacks = null;
        callbacks?.Invoke();
    }

    private void TrackFacebookInitializationFailure(TrackingAuthorizationStatus status)
    {
        if (facebookInitFailureTracked) return;
        facebookInitFailureTracked = true;
        Debug.LogError("[ATT] Facebook initialization failed for tracking status " + status + ".");
    }

    private void FlushMetaCommerceEvents()
    {
        if (!FB.IsInitialized) return;
        var checkoutEvents = pendingMetaCheckoutEvents.ToArray();
        pendingMetaCheckoutEvents.Clear();
        foreach (MetaCheckoutEvent checkoutEvent in checkoutEvents)
            SendMetaInitiatedCheckout(checkoutEvent);

        var purchaseEvents = pendingMetaPurchaseEvents.ToArray();
        pendingMetaPurchaseEvents.Clear();
        foreach (MetaPurchaseEvent purchaseEvent in purchaseEvents)
            SendMetaPurchaseIfNeeded(purchaseEvent);
    }

    private static void SendMetaInitiatedCheckout(MetaCheckoutEvent checkoutEvent)
    {
        var parameters = new Dictionary<string, object>
        {
            [AppEventParameterName.ContentID] = checkoutEvent.ProductId,
            [AppEventParameterName.ContentType] = "product",
            [AppEventParameterName.Currency] = checkoutEvent.Currency,
            [AppEventParameterName.NumItems] = 1
        };
        float? value = checkoutEvent.Price > 0m
            ? decimal.ToSingle(checkoutEvent.Price)
            : null;
        FB.LogAppEvent(AppEventName.InitiatedCheckout, value, parameters);
        Debug.Log(
            "[Meta] InitiatedCheckout: product=" + checkoutEvent.ProductId +
            ", value=" + checkoutEvent.Price +
            ", currency=" + checkoutEvent.Currency + ".");
    }

    private static void SendMetaPurchaseIfNeeded(MetaPurchaseEvent purchaseEvent)
    {
        if (FB.Mobile.IsImplicitPurchaseLoggingEnabled())
        {
            Debug.Log("[Meta] Native implicit purchase logging is enabled; manual purchase event skipped.");
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            [AppEventParameterName.ContentID] = purchaseEvent.ProductId,
            [AppEventParameterName.ContentType] = "product"
        };
        FB.LogPurchase(purchaseEvent.Price, purchaseEvent.Currency, parameters);
        Debug.Log(
            "[Meta] Purchase: product=" + purchaseEvent.ProductId +
            ", value=" + purchaseEvent.Price +
            ", currency=" + purchaseEvent.Currency + ".");
    }

    private static TrackingAuthorizationStatus ParseTrackingAuthorizationStatus(int status)
    {
        if (status < (int)TrackingAuthorizationStatus.NotDetermined ||
            status > (int)TrackingAuthorizationStatus.Authorized)
            return TrackingAuthorizationStatus.Unavailable;
        return (TrackingAuthorizationStatus)status;
    }

    private void RequestNotificationPermission()
    {
        if (notificationPermissionRequested) return;
        notificationPermissionRequested = true;
        notifications.RequestPermission();
    }

    private void ConnectTenjinOnce()
    {
        if (tenjinConnected) return;
        tenjinConnected = true;
        tenjin.Connect();
    }

    private static void OnHideUnity(bool isGameShown)
    {
        Time.timeScale = isGameShown ? 1f : 0f;
    }
}
