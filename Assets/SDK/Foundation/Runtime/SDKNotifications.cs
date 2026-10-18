using System;
using OneSignalSDK;
using OneSignalSDK.Notifications.Models;

internal sealed class SDKNotifications
{
    private readonly string appId;
    private bool initialized;
    private Func<bool> privacyFlowCompleted;

    private bool NotificationPermissionGranted =>
        initialized &&
        (OneSignal.Notifications.Permission ||
         OneSignal.Notifications.PermissionNative == NotificationPermission.Authorized ||
         OneSignal.Notifications.PermissionNative == NotificationPermission.Provisional ||
         OneSignal.Notifications.PermissionNative == NotificationPermission.Ephemeral);

    public SDKNotifications(SDKConfig config)
    {
        appId = config.oneSignal.appId;
#if UNITY_EDITOR
        OneSignal.Debug.LogLevel = OneSignalSDK.Debug.Models.LogLevel.Verbose;
#endif
#if !UNITY_IOS || UNITY_EDITOR
        InitializeOnce();
#endif
    }

    public void BindPrivacyFlow(Func<bool> completed)
    {
        privacyFlowCompleted = completed;
    }

    public async void RequestPermission()
    {
#if UNITY_IOS && !UNITY_EDITOR
        while (privacyFlowCompleted != null && !privacyFlowCompleted())
            await System.Threading.Tasks.Task.Yield();
        UnityEngine.Debug.Log("[ATT] ATT and Facebook flow complete. Requesting notification permission.");
#endif

        InitializeOnce();
        if (NotificationPermissionGranted) return;
        if (OneSignal.Notifications.CanRequestPermission)
            await OneSignal.Notifications.RequestPermissionAsync(false);
    }

    private void InitializeOnce()
    {
        if (initialized) return;
        OneSignal.Initialize(appId);
        initialized = true;
    }
}
