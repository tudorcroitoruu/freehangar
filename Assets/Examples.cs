using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Examples : MonoBehaviour
{
    [SerializeField] private int level = 1;

    #region Developer API Examples

    public void StartLevel() => SDKManager.StartLevel(level);

    public void CompleteLevel() => SDKManager.CompleteLevel(level);

    public void FailLevel() => SDKManager.FailLevel(level);

    public void ShowRevivePopup() => SDKManager.ShowRevivePopup();

    public void ReviveWithCoins() => SDKManager.TryReviveWithCurrency(
        SDKManager.Data.coins >= 100,
        () => SDKManager.Data.coins -= 100,
        RevivePlayer);

    public void SkipRevive() => SDKManager.SkipRevive();

    public void TrackCustomEvent() => SDKManager.TrackEvent("example:button_clicked");

    public void BlockLevelStartNoHearts()
        => SDKManager.LevelEntryBlockedByNoHearts(LevelEntryAction.Start, level);

    public void ResolveLevelStartWithCoins()
        => SDKManager.ResolveLevelEntry(
            LevelEntryAction.Start,
            ProgressResolutionMethod.Coins,
            level,
            () => SDKManager.Data.lives = 1);

    public void ResolveNaturalHeartRefill(int previousHearts, int currentHearts)
        => SDKManager.ResolveNaturalHeartRefill(
            LevelEntryAction.Start,
            level,
            previousHearts,
            currentHearts);

    public void BlockReviveAttemptsExhausted() => SDKManager.ReviveAttemptsExhausted(level);

    public void BlockContentAccess()
        => SDKManager.ContentAccessBlocked(
            ProgressTarget.Create("content", "content_03"),
            ProgressBlockReason.ContentLocked);

    public void ResolveContentTimer()
        => SDKManager.ResolveContentAccess(
            ProgressTarget.Create("content", "content_03"),
            ProgressResolutionMethod.TimerElapsed,
            () => AddLog("Content access granted."));

    public void BlockSequentialLevel()
        => SDKManager.LevelSequenceBlocked(
            ProgressTarget.Create("level", "level_" + (level + 1).ToString("000")));

    public void UnlockContentPack()
        => SDKManager.UnlockContentPack(
            new[]
            {
                ProgressTarget.Create("content", "content_02"),
                ProgressTarget.Create("content", "content_03")
            },
            target => false,
            target => AddLog("Unlocked " + target));

    public void ShowAdOnlyContent()
    {
        // Add a RewardPlacement value, then uncomment the call below.
        // SDKManager.ShowAdEntryRewarded(
        //     RewardPlacement.your_placement,
        //     ProgressTarget.Create("content", "content_03"),
        //     GrantContentAccess);
    }

    public void ShowRewarded()
    {
        // Add a RewardPlacement value, then uncomment the call below.
        // SDKManager.ShowRewarded(RewardPlacement.your_placement, GiveRewardedCoins);
    }

    public void ShowInterstitial()
    {
        SDKManager.ShowInterstitial(InterstitialPlacement.level_complete);
    }

    public void ShowBanner()
    {
        // Add a BannerPlacement value, then uncomment the call below.
        // SDKManager.ShowBanner(BannerPlacement.your_placement);
    }

    public void PurchaseNoAds() => SDKManager.PurchaseNoAds(NoAdsPurchaseSuccess, NoAdsPurchaseFailed);

    public void ShowLocalizedPrice() => AddLog("No Ads price: " + SDKManager.GetPrice(IAPProduct.noads));

    public void CheckNoAdsReceipt() => AddLog("No Ads purchased: " + SDKManager.IsPurchased(IAPProduct.noads));

    public void CheckSubscription() => SDKManager.CheckSubscription(subscribed =>
        AddLog("Active subscription: " + subscribed));

    public void RestorePurchases() => SDKManager.RestorePurchases(success =>
        AddLog("Restore finished. Success: " + success));

    public void RequestNotificationPermission() => SDKManager.RequestNotificationPermission();

    public void RequestRateUs() => SDKManager.RequestRateUs();

    public void OpenTerms() => SDKManager.OpenTerms();

    public void OpenPrivacyPolicy() => SDKManager.OpenPrivacyPolicy();

    public void HapticButton() => SDKManager.HapticButton();

    public void HapticSelection() => SDKManager.HapticSelection();

    public void HapticToggle() => SDKManager.HapticToggle();

    public void HapticLightImpact() => SDKManager.HapticLightImpact();

    public void HapticMediumImpact() => SDKManager.HapticMediumImpact();

    public void HapticHeavyImpact() => SDKManager.HapticHeavyImpact();

    public void HapticSoftImpact() => SDKManager.HapticSoftImpact();

    public void HapticRigidImpact() => SDKManager.HapticRigidImpact();

    public void HapticSuccess() => SDKManager.HapticSuccess();

    public void HapticWarning() => SDKManager.HapticWarning();

    public void HapticError() => SDKManager.HapticError();

    public void HapticExplosion() => SDKManager.HapticExplosion();

    public void HapticCollision() => SDKManager.HapticCollision();

    public void HapticReward() => SDKManager.HapticReward();

    public void HapticHeartbeat() => SDKManager.HapticHeartbeat();

    public void HapticTick() => SDKManager.HapticTick();

    public void HapticLongPress() => SDKManager.HapticLongPress();

    public void HapticContinuousLight() => SDKManager.HapticContinuousLight();

    public void HapticContinuousMedium() => SDKManager.HapticContinuousMedium();

    public void HapticContinuousHeavy() => SDKManager.HapticContinuousHeavy();

    public void HapticContinuous() => SDKManager.HapticContinuous(1.5f, 0.65f, 0.5f);

    public void StopHaptics() => SDKManager.StopHaptics();

    public void ToggleHaptics() => SDKManager.SetHapticsEnabled(!SDKManager.HapticsEnabled);

    public void Save() => SDKManager.Save();

    public void SaveExample()
    {
        SDKManager.Data.coins += 100;
        SDKManager.Save();
    }

    public void LoadExample() => AddLog("Coins after Load: " + SDKManager.Load().coins);

    private void GiveRewardedCoins()
    {
        SDKManager.Data.coins += 25;
        SDKManager.Save();
    }

    private void GrantContentAccess() => AddLog("Content access granted.");

    private void RevivePlayer()
    {
        SDKManager.Data.lives = Mathf.Max(1, SDKManager.Data.lives);
        SDKManager.Save();
        AddLog("Player revived.");
    }

    private void NoAdsPurchaseSuccess()
    {
        AddLog("No Ads purchase succeeded.");
    }

    private void NoAdsPurchaseFailed(IAPPurchaseResult result)
    {
        switch (result.Status)
        {
            case IAPPurchaseStatus.NotInitialized:
                AddLog("No Ads purchase failed: IAP is not initialized.");
                break;
            case IAPPurchaseStatus.ProductUnavailable:
                AddLog("No Ads purchase failed: product is unavailable.");
                break;
            case IAPPurchaseStatus.Cancelled:
                AddLog("No Ads purchase cancelled by the player.");
                break;
            case IAPPurchaseStatus.Failed:
                AddLog("No Ads purchase failed.");
                break;
            default:
                AddLog("No Ads purchase finished with status: " + result.Status + ".");
                break;
        }
    }

    #endregion

    #region Test Scene GUI

    private readonly List<string> messages = new();
    private Vector2 pageScroll;
    private Vector2 logScroll;
    private Vector2 pageDragStart;
    private Vector2 pageDragPrevious;
    private float pageScrollVelocity;
    private bool pageDragActive;
    private bool pageDragMoved;
    private string levelInput = "1";
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle statusStyle;
    private GUIStyle sectionStyle;
    private GUIStyle buttonStyle;
    private GUIStyle inputStyle;
    private GUIStyle logStyle;
    private bool portraitLayout;
    private bool stylesArePortrait;

    private void OnEnable()
    {
        SDKManager.PurchaseCompleted += HandleRecoveredPurchase;
    }

    private void Start()
    {
        level = Mathf.Max(1, level);
        levelInput = level.ToString();
        AddLog("Example scene started. SDK initialization is automatic.");
    }

    private void OnDisable()
    {
        SDKManager.PurchaseCompleted -= HandleRecoveredPurchase;
    }

    private void OnGUI()
    {
        portraitLayout = Screen.height > Screen.width * 1.1f;
        BuildStyles(portraitLayout);
        float referenceWidth = portraitLayout ? 430f : 1180f;
        float scale = Mathf.Clamp(Screen.width / referenceWidth, 0.75f, 3f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        Rect safeArea = Screen.safeArea;
        float margin = portraitLayout ? 8f : 18f;
        float safeTop = (Screen.height - safeArea.yMax) / scale;
        var layoutArea = new Rect(
            safeArea.x / scale + margin,
            safeTop + margin,
            safeArea.width / scale - margin * 2f,
            safeArea.height / scale - margin * 2f);
        HandlePageSwipe(layoutArea);
        GUILayout.BeginArea(layoutArea, GUI.skin.box);
        pageScroll = GUILayout.BeginScrollView(pageScroll);

        GUILayout.Label("SDK FOUNDATION — DEVELOPER EXAMPLES", titleStyle);
        GUILayout.Label("No Init calls. Every SDK is created automatically before the first scene loads.", subtitleStyle);
        DrawStatus();

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && SDKManager.IsReady;
        DrawLevelControls();
        if (portraitLayout)
        {
            DrawAnalyticsColumn();
            DrawAdsColumn();
            DrawPurchasesColumn();
            DrawSaveColumn();
        }
        else
        {
            GUILayout.BeginHorizontal();
            DrawAnalyticsColumn();
            DrawAdsColumn();
            DrawPurchasesColumn();
            DrawSaveColumn();
            GUILayout.EndHorizontal();
        }
        GUI.enabled = previousEnabled;

        DrawHapticsControls();
        DrawLog();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    private void Update()
    {
        if (pageDragActive || Mathf.Abs(pageScrollVelocity) < 1f) return;

        pageScroll.y = Mathf.Max(0f, pageScroll.y + pageScrollVelocity * Time.unscaledDeltaTime);
        pageScrollVelocity = Mathf.MoveTowards(pageScrollVelocity, 0f, 2600f * Time.unscaledDeltaTime);
    }

    private void HandlePageSwipe(Rect touchArea)
    {
        Event current = Event.current;
        if (current == null) return;

        if (current.rawType == EventType.MouseDown && current.button == 0 && touchArea.Contains(current.mousePosition))
        {
            pageDragActive = true;
            pageDragMoved = false;
            pageDragStart = current.mousePosition;
            pageDragPrevious = current.mousePosition;
            pageScrollVelocity = 0f;
            return;
        }

        if (current.rawType == EventType.MouseDrag && pageDragActive)
        {
            Vector2 position = current.mousePosition;
            Vector2 delta = position - pageDragPrevious;
            pageDragPrevious = position;

            if (!pageDragMoved && Vector2.Distance(pageDragStart, position) >= 8f)
                pageDragMoved = true;

            if (!pageDragMoved) return;

            pageScroll.y = Mathf.Max(0f, pageScroll.y - delta.y);
            pageScrollVelocity = -delta.y / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            GUIUtility.hotControl = 0;
            current.Use();
            return;
        }

        if (current.rawType != EventType.MouseUp || !pageDragActive) return;

        pageDragActive = false;
        if (!pageDragMoved) return;

        GUIUtility.hotControl = 0;
        current.Use();
    }

    private void DrawStatus()
    {
        string status = SDKManager.IsReady ? "READY — automatic initialization completed" : "WAITING / CONFIGURATION ERROR";
        Color previous = GUI.color;
        GUI.color = SDKManager.IsReady ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.7f, 0.25f);
        GUILayout.Label(status, statusStyle);
        GUI.color = previous;

        if (!SDKManager.IsReady)
            GUILayout.Label("Check the centered configuration error and Console. Game code must never initialize SDKs manually.", subtitleStyle);
        else
            GUILayout.Label("Rewarded ready: " + SDKManager.IsRewardedReady + "   |   Interstitial ready: " + SDKManager.IsInterstitialReady, subtitleStyle);
    }

    private void DrawLevelControls()
    {
        GUILayout.BeginVertical(sectionStyle);
        GUILayout.Label("LEVEL PROGRESSION", subtitleStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Level number", GUILayout.Width(110f));
        levelInput = GUILayout.TextField(levelInput, inputStyle, GUILayout.Width(portraitLayout ? 100f : 90f));
        if (int.TryParse(levelInput, out int parsed)) level = Mathf.Max(1, parsed);
        GUILayout.Label("Event ID: " + SDKManager.FormatLevel(level));
        GUILayout.EndHorizontal();
        if (portraitLayout)
        {
            ActionButton("Start Level", StartLevel);
            ActionButton("Complete Level", CompleteLevel);
            ActionButton("Final Fail", FailLevel);
        }
        else
        {
            GUILayout.BeginHorizontal();
            ActionButton("Start Level", StartLevel);
            ActionButton("Complete Level", CompleteLevel);
            ActionButton("Final Fail", FailLevel);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawAnalyticsColumn()
    {
        BeginColumn("ANALYTICS");
        ActionButton("Show Revive Popup", ShowRevivePopup);
        ActionButton("Revive With 100 Coins", ReviveWithCoins);
        ActionButton("Skip Revive", SkipRevive);
        ActionButton("Custom Event", TrackCustomEvent);
        EndColumn();
    }

    private void DrawAdsColumn()
    {
        BeginColumn("LEVELPLAY ADS");
        ActionButton("Show Interstitial", ShowInterstitial);
        ActionButton("Hide Banner", SDKManager.HideBanner);
        GUILayout.Space(8f);
        GUILayout.Label("The wrapper automatically handles loading, retries, callbacks, analytics and Tenjin impressions.", subtitleStyle);
        EndColumn();
    }

    private void DrawPurchasesColumn()
    {
        BeginColumn("UNITY IAP");
        ActionButton("Purchase No Ads", PurchaseNoAds);
        ActionButton("Get Localized Price", ShowLocalizedPrice);
        ActionButton("Check No Ads Receipt", CheckNoAdsReceipt);
        ActionButton("Check Subscription", CheckSubscription);
        ActionButton("Restore Purchases", RestorePurchases);
        GUILayout.Space(8f);
        GUILayout.Label("Receipt validation, transaction deduplication, analytics, Tenjin transaction and conversion value are internal.", subtitleStyle);
        EndColumn();
    }

    private void DrawSaveColumn()
    {
        BeginColumn("SAVE SYSTEM");
        ActionButton("Add 100 Test Coins + Save", SaveExample);
        ActionButton("Load Player Data", LoadExample);
        ActionButton("Explicit Save", Save);
        GUILayout.Space(8f);
        PlayerData data = SDKManager.Data;
        GUILayout.Label("Coins: " + data.coins, subtitleStyle);
        GUILayout.Label("Current level: " + data.currentLevel, subtitleStyle);
        GUILayout.Label("Lives: " + data.lives, subtitleStyle);
        GUILayout.Label("Ads disabled: " + SDKManager.AdsDisabled, subtitleStyle);
        EndColumn();
    }

    private void DrawHapticsControls()
    {
        BeginColumn("IOS HAPTICS");
        GUILayout.Label(
            "Enabled: " + SDKManager.HapticsEnabled + "   |   Supported on this device: " + SDKManager.HapticsSupported,
            subtitleStyle);

        DrawHapticGroup(
            "INTERACTIONS",
            new[] { "Button", "Selection", "Toggle", "Tick", "Long Press" },
            new Action[] { HapticButton, HapticSelection, HapticToggle, HapticTick, HapticLongPress });

        DrawHapticGroup(
            "IMPACTS",
            new[] { "Light Impact", "Medium Impact", "Heavy Impact", "Soft Impact", "Rigid Impact", "Collision" },
            new Action[] { HapticLightImpact, HapticMediumImpact, HapticHeavyImpact, HapticSoftImpact, HapticRigidImpact, HapticCollision });

        DrawHapticGroup(
            "NOTIFICATIONS AND GAME PATTERNS",
            new[] { "Success", "Warning", "Error", "Reward", "Explosion", "Heartbeat" },
            new Action[] { HapticSuccess, HapticWarning, HapticError, HapticReward, HapticExplosion, HapticHeartbeat });

        DrawHapticGroup(
            "CONTINUOUS",
            new[] { "Light 0.6s", "Medium 0.8s", "Heavy 1.0s", "Custom 1.5s", "Stop", "Enable / Disable" },
            new Action[] { HapticContinuousLight, HapticContinuousMedium, HapticContinuousHeavy, HapticContinuous, StopHaptics, ToggleHaptics });

        EndColumn();
    }

    private void DrawHapticGroup(string title, string[] labels, Action[] actions)
    {
        GUILayout.Space(8f);
        GUILayout.Label(title, subtitleStyle);
        int columns = portraitLayout ? 2 : 4;
        for (int index = 0; index < labels.Length; index += columns)
        {
            GUILayout.BeginHorizontal();
            for (int column = 0; column < columns; column++)
            {
                int item = index + column;
                if (item < labels.Length) ActionButton(labels[item], actions[item]);
                else GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawLog()
    {
        GUILayout.Space(10f);
        GUILayout.Label("RESULT LOG", subtitleStyle);
        float logHeight = portraitLayout ? 240f : 170f;
        logScroll = GUILayout.BeginScrollView(logScroll, GUI.skin.box, GUILayout.Height(logHeight));
        for (int index = messages.Count - 1; index >= 0; index--)
            GUILayout.Label(messages[index], logStyle);
        GUILayout.EndScrollView();
    }

    private void HandleRecoveredPurchase(IAPPurchaseResult result)
    {
        HandlePurchase(result, true);
    }

    private void HandlePurchase(IAPPurchaseResult result, bool recovered)
    {
        if (!result.Succeeded)
        {
            AddLog("Purchase " + result.ProductId + " failed: " + result.Status);
            return;
        }

        SDKManager.Save();
        AddLog((recovered ? "Recovered" : "Completed") + " purchase: " + result.ProductId + " / " + result.LocalizedPrice + " " + result.Currency);
    }

    private void AddLog(string message)
    {
        messages.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        if (messages.Count > 40) messages.RemoveAt(0);
        Debug.Log("[SDK Example] " + message);
    }

    private void BeginColumn(string title)
    {
        if (portraitLayout)
            GUILayout.BeginVertical(sectionStyle, GUILayout.ExpandWidth(true));
        else
            GUILayout.BeginVertical(sectionStyle, GUILayout.MinWidth(245f), GUILayout.ExpandWidth(true));
        GUILayout.Label(title, subtitleStyle);
    }

    private static void EndColumn() => GUILayout.EndVertical();

    private void ActionButton(string label, Action action)
    {
        if (GUILayout.Button(label, buttonStyle)) action.Invoke();
    }

    private void BuildStyles(bool portrait)
    {
        if (titleStyle != null && stylesArePortrait == portrait) return;
        stylesArePortrait = portrait;
        int titleSize = portrait ? 22 : 26;
        int bodySize = portrait ? 17 : 14;
        int statusSize = portrait ? 20 : 17;
        int controlSize = portrait ? 18 : 14;
        float controlHeight = portrait ? 56f : 36f;
        int padding = portrait ? 16 : 12;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = titleSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = bodySize, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        statusStyle = new GUIStyle(GUI.skin.label) { fontSize = statusSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        sectionStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(padding, padding, padding, padding), margin = new RectOffset(4, 4, 8, 8) };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = controlSize, fixedHeight = controlHeight, margin = new RectOffset(2, 2, 5, 5), wordWrap = true };
        inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = controlSize, fixedHeight = portrait ? 48f : 34f, alignment = TextAnchor.MiddleCenter };
        logStyle = new GUIStyle(GUI.skin.label) { fontSize = bodySize, wordWrap = true };
    }

    #endregion
}

public partial class PlayerData
{
    public int currentLevel = 1;
    public int coins;
    public int lives = 5;
}
