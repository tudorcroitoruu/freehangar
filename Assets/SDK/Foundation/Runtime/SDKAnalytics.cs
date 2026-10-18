using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using UnityEngine;

internal sealed class SDKAnalytics
{
    private readonly SDKConfig config;

    public SDKAnalytics(SDKConfig config)
    {
        this.config = config;
        ConfigureSettings();
        GameAnalytics.Initialize();
    }

    public void StartLevel(int level)
    {
        SaveSystem.State.currentLevel = level;
        SaveSystem.Save();
        Progress(GAProgressionStatus.Start, level);
    }
    public void CompleteLevel(int level) => Progress(GAProgressionStatus.Complete, level);
    public void FailLevel(int level)
    {
        Progress(GAProgressionStatus.Fail, level);
        if (SaveSystem.State.firstLoseTracked) return;
        SaveSystem.State.firstLoseTracked = true;
        TrackFirstMilestone("gameplay:first_lose", level);
        SaveSystem.Save();
    }
    public void Design(string eventName, Dictionary<string, object> fields = null)
    {
        if (fields == null) GameAnalytics.NewDesignEvent(eventName);
        else GameAnalytics.NewDesignEvent(eventName, fields);
    }

    public void Ad(GAAdAction action, GAAdType type, string placement)
    {
        GameAnalytics.NewAdEvent(action, type, "LevelPlay", SafePlacement(placement));
    }

    public void Purchase(string currency, int amountCents, string productId, string receipt)
    {
#if UNITY_IOS && !UNITY_EDITOR
        GameAnalytics.NewBusinessEventIOS(currency, amountCents, productId, productId, "iap", receipt);
#else
        GameAnalytics.NewBusinessEvent(currency, amountCents, productId, productId, "iap");
#endif
    }

    public void TrackFirstPurchase()
    {
        if (SaveSystem.State.firstPurchaseTracked) return;
        SaveSystem.State.firstPurchaseTracked = true;
        TrackFirstMilestone("economy:first_purchase", SaveSystem.State.currentLevel);
        SaveSystem.Save();
    }

    private void TrackFirstMilestone(string prefix, int level)
    {
        int minutes = 0;
        if (DateTime.TryParse(SaveSystem.State.firstLaunchUtc, out var firstLaunch))
            minutes = Math.Max(0, (int)(DateTime.UtcNow - firstLaunch.ToUniversalTime()).TotalMinutes);
        Design(prefix + ":min_" + minutes.ToString("0000"));
        Design(prefix + ":lvl_" + level.ToString("0000"));
        Design(prefix + ":session_" + SaveSystem.State.launchCount.ToString("0000"));
    }

    private void Progress(GAProgressionStatus status, int level)
    {
        GameAnalytics.NewProgressionEvent(status, "level", level.ToString("0000"));
    }

    private static string SafePlacement(string placement)
    {
        if (string.IsNullOrWhiteSpace(placement)) return "default";
        return placement.Length <= 64 ? placement : placement.Substring(0, 64);
    }

    private void ConfigureSettings()
    {
        var settings = GameAnalytics.SettingsGA;
        settings.Platforms.Clear();
        settings.Build.Clear();
        settings.SelectedPlatformOrganization.Clear();
        settings.SelectedPlatformStudio.Clear();
        settings.SelectedPlatformGame.Clear();
        settings.SelectedPlatformGameID.Clear();
        settings.SelectedOrganization.Clear();
        settings.SelectedStudio.Clear();
        settings.SelectedGame.Clear();
        Add(RuntimePlatform.IPhonePlayer, config.gameAnalytics.ios);
        settings.UsePlayerSettingsBuildNumber = true;
        settings.SubmitErrors = true;

        void Add(RuntimePlatform platform, AnalyticsPlatformConfig value)
        {
            int index = settings.Platforms.Count;
            settings.AddPlatform(platform);
            settings.UpdateGameKey(index, value.gameKey);
            settings.UpdateSecretKey(index, value.secretKey);
        }
    }
}
