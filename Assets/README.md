# Unity iOS Game Template

This project provides a ready-to-use SDK foundation for iOS games. All required
SDKs initialize automatically before the first scene. Game code uses only the
public `SDKManager` API and typed IDs.

## Developer rules

Developers may edit:

- `Assets/SDK/Game/AdPlacements.cs` — typed LevelPlay placement IDs.
- Normal game scripts, scenes, prefabs, and assets.

Developers must not edit:

- `Assets/SDK/Foundation/` — initialization, wrappers, validation, analytics,
  attribution, ads, purchases, notifications, reviews, and save internals.
- `Assets/SDK/Vendor/` — imported third-party SDK distributions.
- `Assets/SDK/Game/IAPProducts.cs` — generated from `Assets/sdk_config.json`.
- `Assets/SDK/Resources/SDKSettings.asset` — generated compiled settings.
- Vendor-generated LevelPlay and dependency files.
- `Assets/SDK/Vendor/DOTween/` — the untouched DOTween distribution. Update it
  through the official DOTween package instead of editing vendor files.

Never create another SDK configuration file. `Assets/sdk_config.json` is the only
editable source of truth. It is processed in the Unity Editor and is not copied
into the Xcode project or final application.

## Configuration reference

Every section is mandatory; SDKs cannot be disabled from JSON.

| JSON field | Purpose |
| --- | --- |
| `schemaVersion` | Configuration schema version. |
| `app.privacyTrackingDescription` | iOS tracking usage description written to the project settings/build. |
| `facebook.appId` | Meta application ID. |
| `facebook.clientToken` | Meta client token. |
| `tenjin.iosApiKey` | Tenjin iOS API key. |
| `oneSignal.appId` | OneSignal application ID. |
| `storeLinks.termsUrl` | Production Terms URL used by `OpenTerms()`. |
| `storeLinks.privacyPolicyUrl` | Production Privacy URL used by `OpenPrivacyPolicy()`. |
| `gameAnalytics.ios.gameKey` | GameAnalytics iOS game key. |
| `gameAnalytics.ios.secretKey` | GameAnalytics iOS secret key. |
| `levelPlay.ios.appKey` | LevelPlay iOS application key. |
| `levelPlay.ios.adMobAppId` | Google Mobile Ads iOS application ID. |
| `levelPlay.ios.rewardedAdUnitId` | LevelPlay rewarded ad unit ID. |
| `levelPlay.ios.interstitialAdUnitId` | LevelPlay interstitial ad unit ID. |
| `levelPlay.ios.bannerAdUnitId` | LevelPlay banner ad unit ID. |
| `levelPlay.networks` | Exact set of installed LevelPlay mediation adapters. |
| `iap.products[].id` | Exact App Store product ID. |
| `iap.products[].type` | Unity IAP type; currently required to be `NonConsumable`. |
| `iap.products[].usdCents` | Catalog USD price in cents for attribution/conversion fallback. |

Bundle identifiers, Android package names, Rate Us settings, and per-SDK `enabled`
flags do not belong in this file.

## Automatic initialization

Game code does not call any initialization method. Before the first scene Unity
creates a persistent `SDKManager` object containing the Foundation components and
the GameAnalytics component. The object survives scene changes. The bootstrap also
sets `Application.targetFrameRate` to `60` automatically.

The Foundation automatically initializes and coordinates:

- Meta/Facebook, including ATT-aware advertiser tracking and `ActivateApp`;
- Tenjin attribution;
- GameAnalytics;
- OneSignal and notification permission;
- Unity LevelPlay and configured mediation networks;
- Unity IAP;
- SaveSystem and SDK-owned state.

On iOS the order is ATT request, Meta configuration/reactivation, and notification
permission. If Meta initialization is delayed, notification permission is released
after the internal timeout while Meta continues initializing.

In Editor Play Mode, an invalid configuration stops SDK startup and shows a
centered English error overlay. The overlay is excluded from production builds;
the pre-build validator prevents an invalid configuration from being built.

## Mandatory game calls

Real game code must use these calls outside `Examples.cs`:

```csharp
SDKManager.StartLevel(level);
SDKManager.CompleteLevel(level);
SDKManager.FailLevel(level);

SDKManager.RequestRateUs();
SDKManager.OpenTerms();
SDKManager.OpenPrivacyPolicy();
```

Levels start at `1`. `FailLevel` is sent only after all revive options are finished.
Do not fail a level while a revive popup is still active.

GameAnalytics progression, first-loss tracking, launch/session metadata, and save
updates are handled internally. When game code needs the standardized display ID:

```csharp
string levelId = SDKManager.FormatLevel(level); // level_0001
```

The build validator blocks builds when mandatory calls are absent from real game
scripts.

## Revive API

Revive is optional, but developers must use the wrapper when it is present:

```csharp
SDKManager.ShowRevivePopup();

SDKManager.ReviveWithRewardedAd(
    RewardPlacement.revive,
    RevivePlayer,
    HandleAdUnavailable);

bool revived = SDKManager.TryReviveWithCurrency(
    coins >= revivePrice,
    () => coins -= revivePrice,
    RevivePlayer);

SDKManager.SkipRevive();
```

The wrapper sends standardized `popup_shown`, `revived`, `not_enough_money`, and
`skipped` analytics under the `gameplay:revive` namespace.

## Typed ad placements

Define every placement in `Assets/SDK/Game/AdPlacements.cs`:

```csharp
public enum RewardPlacement
{
}

public enum InterstitialPlacement
{
    level_complete
}

public enum BannerPlacement
{
}
```

The template ships with only `InterstitialPlacement.level_complete`. Rewarded and
banner enums are intentionally empty. Add their real dashboard placements before
using the corresponding wrapper methods.

Placement names must start with a lowercase letter, contain only `a-z`, `0-9`, or
underscore, and contain no more than 64 characters. The enum name is sent to
LevelPlay exactly as written.

```csharp
// After adding bonus_coins to RewardPlacement:
SDKManager.ShowRewarded(RewardPlacement.bonus_coins, GiveCoins);

SDKManager.ShowRewarded(RewardPlacement.bonus_coins, granted =>
{
    if (granted) GiveCoins();
});

SDKManager.ShowInterstitial(
    InterstitialPlacement.level_complete,
    ContinueAfterAd);

// After adding gameplay to BannerPlacement:
SDKManager.ShowBanner(BannerPlacement.gameplay);
SDKManager.ShowBanner(BannerPlacement.gameplay, top: true);
SDKManager.HideBanner();

bool rewardedReady = SDKManager.IsRewardedReady;
bool interstitialReady = SDKManager.IsInterstitialReady;
```

Ad loading, retry behavior, callbacks, GameAnalytics ad events, and Tenjin
ad revenue are internal. Every LevelPlay rewarded, interstitial, and banner ad
object is automatically subscribed to Tenjin ILRD. Revenue, network, ad unit,
auction, country, instance, and precision data are forwarded when an impression
is displayed. If Tenjin is not ready yet, Foundation retries the subscription.
Never call LevelPlay or Tenjin ad-revenue APIs directly.

## Typed IAP product

The current template intentionally supports exactly one product: the lowercase
`no.ads` Store ID with type `NonConsumable`.

Edit only the product in `Assets/sdk_config.json`. The Editor generates
`Assets/SDK/Game/IAPProducts.cs`. C# enums cannot contain string values, so the
generated API is a closed typed value:

```csharp
public static readonly IAPProduct noads = new IAPProduct("no.ads");
```

The dot is omitted only from the generated C# member name. At runtime the typed
value already contains the exact Store ID; there is no switch, lookup table, or
string conversion.

```csharp
SDKManager.PurchaseIAP(IAPProduct.noads, result =>
{
    if (result.Succeeded)
        RefreshNoAdsUI();
});

string localizedPrice = SDKManager.GetPrice(IAPProduct.noads);
bool owned = SDKManager.IsPurchased(IAPProduct.noads);

SDKManager.RestorePurchases(success =>
{
    RefreshNoAdsUI();
});
```

The simpler success-only overload is also available:

```csharp
SDKManager.PurchaseIAP(IAPProduct.noads, RemoveAds);
```

The wrapper automatically applies the No Ads entitlement, saves it, and hides an
active banner. Receipt validation, three initialization/product-fetch attempts,
transaction deduplication, recovered transactions, and App Store restore are
internal.

Purchase analytics are also internal:

- GameAnalytics `shop:purchase_start` and validated business events;
- Tenjin `initiateCheckout`, transaction data, and conversion value updates;
- Meta standard `InitiatedCheckout`;
- Meta implicit purchase logging, with `FB.LogPurchase` only as a fallback to
  prevent duplicates;
- first-purchase milestone events;
- sandbox purchases excluded from revenue and conversion tracking.

`IAPPurchaseResult.Status` is one of `Success`, `NotInitialized`,
`ProductUnavailable`, `Cancelled`, or `Failed`.

Subscription query methods exist for future templates:

```csharp
bool cachedValue = SDKManager.IsSubscribed();
SDKManager.CheckSubscription(isSubscribed => UpdateSubscriptionUI(isSubscribed));
```

The current configuration contains no subscription and validation allows only the
single No Ads product, so these methods currently return `false`.

## SaveSystem

Add only game-owned fields in a separate partial file under `Assets/SDK/Game/`:

```csharp
using System;

[Serializable]
public partial class PlayerData
{
    public int coins;
    public int currentLevel = 1;
    public int lives = 5;
}
```

Use the shared instance and save after changes:

```csharp
SDKManager.Data.coins += 100;
SDKManager.Save();

PlayerData data = SDKManager.Load();
```

The save payload is encrypted with AES-256-CBC and authenticated with HMAC-SHA256.
Keys are derived for the application and current device. The JSON file exposes
only encryption metadata and Base64 ciphertext. Temporary-file replacement and an
encrypted backup fallback protect against interrupted writes. Existing legacy
plaintext saves are migrated automatically on the next load, including the backup.

SDK bookkeeping is stored separately from `PlayerData`. Do not add transactions,
analytics flags, launch counters, purchase totals, or ad-removal fields to game
data.

## Rate Us, legal links, and notifications

```csharp
SDKManager.RequestRateUs();
SDKManager.OpenTerms();
SDKManager.OpenPrivacyPolicy();
SDKManager.RequestNotificationPermission();
```

Rate Us always requests the native iOS review prompt; it has no enable flag or
schedule in JSON. Terms and Privacy URLs come only from `Assets/sdk_config.json`.
The validator rejects missing, invalid, or example URLs and requires all three
mandatory UI calls in real game code.

The normal notification permission request is automatic after the ATT/Meta flow.
The public notification method is available only when the game intentionally needs
another user-triggered entry point. OneSignal initialization remains internal.

## iOS haptics

Haptics use native UIKit feedback generators and Core Haptics. No initialization
is required. Common calls are available directly from `SDKManager`:

```csharp
SDKManager.HapticButton();
SDKManager.HapticSelection();
SDKManager.HapticToggle();
SDKManager.HapticSuccess();
SDKManager.HapticWarning();
SDKManager.HapticError();
SDKManager.HapticExplosion();
SDKManager.HapticLightImpact();
SDKManager.HapticMediumImpact();
SDKManager.HapticHeavyImpact();
SDKManager.HapticSoftImpact();
SDKManager.HapticRigidImpact();
SDKManager.HapticCollision();
SDKManager.HapticReward();
SDKManager.HapticHeartbeat();
SDKManager.HapticTick();
SDKManager.HapticLongPress();
SDKManager.HapticContinuousLight();
SDKManager.HapticContinuousMedium();
SDKManager.HapticContinuousHeavy();
```

Every preset is also available through the typed API:

```csharp
SDKManager.Haptic(HapticPreset.Collision);
SDKManager.Haptic(HapticPreset.SoftImpact);
SDKManager.Haptic(HapticPreset.RigidImpact);
SDKManager.Haptic(HapticPreset.Tick);
SDKManager.Haptic(HapticPreset.ContinuousHeavy);
```

Custom continuous vibration supports duration, intensity, and sharpness:

```csharp
SDKManager.HapticContinuous(duration: 1.5f, intensity: 0.65f, sharpness: 0.5f);
SDKManager.StopHaptics();
```

The duration is clamped to `0.05–30` seconds and intensity/sharpness to `0–1`.
Continuous feedback stops automatically when the application enters the
background. Devices without Core Haptics use the native vibration fallback.

The user setting is stored inside the encrypted SDK save state:

```csharp
SDKManager.SetHapticsEnabled(false);
bool enabled = SDKManager.HapticsEnabled;
bool supported = SDKManager.HapticsSupported;
```

## DOTween

DOTween 1.3.030 is installed under `Assets/SDK/Vendor/DOTween`. Its project
settings live at `Assets/SDK/Resources/DOTweenSettings.asset`. Developers use
DOTween directly in game and UI scripts; no SDKManager wrapper is needed:

```csharp
using DG.Tweening;

transform.DOMoveX(5f, 0.5f).SetEase(Ease.OutCubic);
panel.DOScale(1f, 0.25f).From(0.8f).SetEase(Ease.OutBack);
canvasGroup.DOFade(1f, 0.2f);
```

Kill tweens owned by scene objects when appropriate:

```csharp
private void OnDestroy() => transform.DOKill();
```

Do not move or modify files inside the vendor folder. Use
`Tools > Demigiant > DOTween Utility Panel` when updating modules or DOTween
settings.

## Custom analytics

### Progress blockage and resolution

Never build `progress_blocked` or `progress_resolved` strings manually. Foundation
uses fixed enums, validates every area/reason/method/target combination, and keeps
active gates in the encrypted SDK save state.

An explicit failed attempt calls `ProgressBlocked`. Displaying locked content does
not. Each attempted blockage sends one event and registers its gate. A resolution
is sent only when that exact gate exists and is removed; otherwise the method
returns `false` and sends nothing.

Use typed targets:

```csharp
ProgressTarget.MainLevel(42);                  // main_level:level_042
ProgressTarget.PremiumEpisode("vip_chapter_03");
ProgressTarget.PremiumLevel("vip_level_018");
```

Progress analytics deliberately uses the requested three-digit main-level target
format (`level_042`). The existing GameAnalytics progression API keeps its original
four-digit progression format for historical compatibility.

Level entry blocked by zero hearts:

```csharp
SDKManager.LevelEntryBlockedByNoHearts(LevelEntryAction.Start, 42);
SDKManager.LevelEntryBlockedByNoHearts(LevelEntryAction.Restart, 42);
SDKManager.LevelEntryBlockedByNoHearts(LevelEntryAction.NextLevel, 43);

// Call only after the heart was actually granted.
SDKManager.ResolveLevelEntry(
    LevelEntryAction.Start,
    ProgressResolutionMethod.Coins,
    42,
    GrantHeartWithCoins);

SDKManager.ResolveLevelEntry(
    LevelEntryAction.Start,
    ProgressResolutionMethod.RewardedAd,
    42,
    GrantHeartFromReward);

// Sends only for an exact zero-to-one transition and an active gate.
SDKManager.ResolveNaturalHeartRefill(
    LevelEntryAction.Start,
    42,
    previousHearts: 0,
    currentHearts: 1);
```

Revive attempts:

```csharp
SDKManager.ReviveAttemptsExhausted(42);
```

`TryReviveWithCurrency` automatically sends `revive:no_coins` on a failed spend
and resolves with `revive:coins` only after currency is spent and the revive
callback runs. `ReviveWithRewardedAd` reports `ad_not_ready` or `ad_closed` and
resolves with `rewarded_ad` only after the reward callback restores the attempt.
Existing revive analytics remain enabled for historical comparison.

Premium episode access:

```csharp
SDKManager.PremiumEpisodeAccessBlocked(
    "vip_chapter_03",
    ProgressBlockReason.PremiumLocked);

SDKManager.PremiumEpisodeAccessBlocked(
    "vip_chapter_02",
    ProgressBlockReason.TimerLocked);

SDKManager.ResolvePremiumEpisodeAccess(
    "vip_chapter_02",
    ProgressResolutionMethod.TimerElapsed,
    GrantEpisodeAccess);
```

The grant callback runs before the resolution event. `SinglePurchase`,
`PremiumPack`, and `TimerElapsed` are the only valid episode-access methods.

Premium Pack must receive the complete pack episode list, an ownership predicate,
and the real grant callback:

```csharp
SDKManager.UnlockPremiumPack(
    packEpisodeIds,
    episodeId => save.OwnsEpisode(episodeId),
    episodeId => save.GrantEpisode(episodeId));
```

It skips already owned episodes, removes duplicates, and sends exactly one
`premium_pack` resolution for each newly unlocked episode.

Sequential locks:

```csharp
SDKManager.LevelSequenceBlocked(ProgressTarget.MainLevel(43));
SDKManager.LevelSequenceBlocked(ProgressTarget.PremiumLevel("vip_level_018"));
```

`CompleteLevel(42)` automatically resolves a previously registered
`main_level:level_043` sequence gate. For premium sequences, grant access and use:

```csharp
SDKManager.ResolveLevelSequence(
    ProgressTarget.PremiumLevel("vip_level_018"),
    GrantPremiumLevelAccess);
```

Ad-only premium levels use a detailed rewarded flow:

```csharp
SDKManager.ShowAdEntryRewarded(
    RewardPlacement.premium_entry,
    "vip_level_003",
    GrantPremiumLevelAccess,
    outcome => UpdatePremiumLevelUI(outcome));
```

The wrapper sends `rewarded_required`, waits for loading when necessary, preserves
the target ID, and distinguishes `ad_not_ready`, `ad_load_failed`,
`ad_show_failed`, `ad_closed`, and `rewarded_ad`. A 15-second load timeout reports
`LoadFailed`. Raw SDK error messages never enter event names.

Premium IAP unlocks preserve the intended episode ID across the asynchronous
purchase:

```csharp
SDKManager.PurchasePremiumEpisodeUnlock(
    IAPProduct.premiumepisode,
    "vip_chapter_03",
    GrantEpisodeAccess,
    result => UpdatePurchaseUI(result.Status));
```

`NotInitialized`, `ProductUnavailable`, `Failed`, and `Cancelled` map separately
to `iap_not_ready`, `product_unavailable`, `iap_failed`, and
`purchase_cancelled`. A later successful retry sends `purchase_retry` only when a
purchase-failure gate exists. The current template contains only `IAPProduct.noads`;
the premium product example applies when a future game adds that product.

For gates known to exist before an explicit attempt, such as content initialized
as locked, register them without sending an event:

```csharp
SDKManager.RegisterProgressGate(
    ProgressArea.EpisodeAccess,
    ProgressTarget.PremiumEpisode("vip_chapter_03"));
```

This never sends `blocked`; it only allows a later real unlock to send `resolved`.
Do not register optional boosters or double rewards because they never prevent
progression. Reaching the normal end of an episode is not a blockage.

### Game-specific events

Progression, ads, purchases, first loss, first purchase, and revive events are
already standardized. Use custom events only for game-specific behavior:

```csharp
SDKManager.TrackEvent("gameplay:booster:used");

SDKManager.TrackEvent("gameplay:booster:used", new Dictionary<string, object>
{
    ["booster"] = "hint",
    ["level"] = currentLevel
});
```

Never duplicate Foundation events by calling GameAnalytics, Meta, or Tenjin
directly.

## Configuration synchronization

When `Assets/sdk_config.json` changes or scripts reload, Editor tooling updates:

- compiled `SDKSettings.asset` used by runtime builds;
- Meta app ID, client token, automatic event logging, and advertiser collection;
- GameAnalytics iOS keys and build settings;
- the LevelPlay AdMob iOS application ID;
- the generated typed IAP product file.

The source JSON remains Unity-only. Xcode receives the already configured SDK
settings and generated plist/project values, never a copied JSON file.

## Build validation

Validation runs immediately after pressing Build, before the long Unity build.
The project currently validates iOS builds only.
The saved Compression Method is automatically set to LZ4HC for every developer
when the project opens, scripts reload, or the active build target changes. Every
build is also forced to use LZ4HC regardless of the previous local Editor value.

A build is blocked for:

- missing, malformed, or incomplete `Assets/sdk_config.json`;
- missing SDK folders or required Unity packages;
- incorrect LevelPlay adapter set;
- invalid or missing ad placement enums;
- anything other than one NonConsumable No Ads product;
- missing Unity IAP receipt obfuscation data;
- missing mandatory level, Rate Us, Terms, or Privacy calls;
- missing or invalid `Assets/SDK/skad.txt` data.

SDK and adapter versions are checked online before every build. Version results are
readable Console warnings only and never block the build.

Editor commands:

- `Tools > Clean Saves` — immediately deletes every local save file and all
  PlayerPrefs for the current project. The command is disabled during Play Mode.
- `Tools > Compress Textures` — applies smart iOS ASTC overrides with High
  Quality compression. Important UI uses ASTC 5x5 at up to 512 px, then 6x6,
  8x8, and 10x10 as resolution increases. Other textures use 6x6 up to 512 px,
  8x8 below 1024 px, and 10x10 at 1024 px or above. Vendor, Editor, Gizmos,
  lightmap, shadowmask, and HDR textures are left untouched.
- `SDK > Validate Configuration`
- `SDK > Check SDK Versions`
- `SDK > Open sdk_config.json`
- `SDK > Regenerate IAP Product IDs`

## Examples and reusable UI

- `Assets/Examples.cs` — all public calls and the large mobile test UI.
- The haptics section exposes every preset as a large device-test button.
- `Assets/Scenes/ExampleScene.unity` — interactive test scene.
- `Assets/SDK/Game/SDKIntegrationExample.cs.txt` — minimum required level calls.
- `Assets/UI/Effects/BackgroundBlurUI/` — reusable one-shot UI background blur.

Examples do not satisfy mandatory build validation. Required calls must exist in
real game scripts. `Assets/TemporarySDKUsage.cs` is ignored by Git and is intended
only for temporary local template builds.

## Complete SDKManager API reference

Game code should normally use the examples above. This list documents every
currently public member so developers never need to inspect Foundation code.

### State and saves

| Member | Meaning |
| --- | --- |
| `SDKManager.Data` | Shared game-owned `PlayerData` instance. |
| `SDKManager.PlayerData` | Alias of `Data`. |
| `SDKManager.Save()` | Saves game data and returns whether the write succeeded. |
| `SDKManager.Load()` | Loads and returns `PlayerData`. |
| `SDKManager.AdsDisabled` | Saved No Ads entitlement state. |
| `SDKManager.DisableAds()` | Saves No Ads and hides an active banner. Normally called automatically after purchase. |
| `SDKManager.IsReady` | Diagnostic readiness flag. Normal scene code does not wait for or initialize the SDK. |
| `SDKManager.Config` | Read-only compiled configuration for diagnostics; game logic should use wrapper methods. |

### Levels, revive, and analytics

| Member | Meaning |
| --- | --- |
| `StartLevel(int)` | Starts standardized progression and saves the current level. |
| `CompleteLevel(int)` | Completes progression and updates the highest completed level. |
| `FailLevel(int)` | Sends final failure and first-loss milestones when applicable. |
| `FormatLevel(int)` | Returns `level_0001` formatting. |
| `ShowRevivePopup()` | Tracks the revive popup. |
| `ReviveWithRewardedAd(...)` | Shows a typed rewarded placement and revives only after reward. |
| `TryReviveWithCurrency(...)` | Spends currency and revives, or tracks insufficient currency. |
| `SkipRevive()` | Tracks that revive was skipped. |
| `TrackEvent(string, fields)` | Sends a game-specific GameAnalytics design event. |
| `RegisterProgressGate(area, target)` | Registers a real lock without sending analytics. |
| `ProgressBlocked(area, reason, target)` | Sends one validated blocked event for an explicit failed attempt. |
| `ProgressResolved(area, method, target)` | Removes an active gate and sends resolution, or returns `false`. |
| `LevelEntryBlockedByNoHearts(...)` | Typed zero-hearts blockage for start, restart, or next level. |
| `ResolveLevelEntry(...)` | Resolves a registered heart gate with coins, rewarded ad, or refill. |
| `ResolveNaturalHeartRefill(...)` | Resolves only an exact zero-to-one heart transition. |
| `ReviveAttemptsExhausted(level)` | Sends the revive attempts-exhausted blockage. |
| `PremiumEpisodeAccessBlocked(...)` | Sends premium- or timer-locked episode blockage. |
| `ResolvePremiumEpisodeAccess(...)` | Grants access, then resolves the episode gate. |
| `UnlockPremiumPack(...)` | Grants and resolves every newly unlocked, non-owned pack episode once. |
| `LevelSequenceBlocked(target)` | Sends previous-level-required blockage. |
| `ResolveLevelSequence(target, grant)` | Grants the newly unlocked target, then resolves its gate. |

`RevivePopupShown()`, `ReviveUsed()`, `ReviveNotEnoughMoney()`, `ReviveSkipped()`,
and `GetLocalizedIAPPrice()`/`HasIAPReceipt()` are compatibility aliases. Prefer
the primary methods shown in the examples.

### Ads

| Member | Meaning |
| --- | --- |
| `IsRewardedReady` | Whether the rewarded ad unit is ready. |
| `IsInterstitialReady` | Whether the interstitial ad unit is ready. |
| `ShowRewarded(placement, Action)` | Invokes the action only when the reward is granted. |
| `ShowRewarded(placement, Action<bool>)` | Reports whether the reward was granted. |
| `ShowRewardedDetailed(...)` | Reports `Rewarded`, `NotReady`, `LoadFailed`, `ShowFailed`, or `Closed`. |
| `ShowAdEntryRewarded(...)` | Complete typed ad-only premium-level access flow. |
| `ShowInterstitial(placement, onClosed)` | Shows an interstitial and optionally reports close. |
| `ShowBanner(placement, top)` | Shows a bottom banner by default or a top banner when requested. |
| `HideBanner()` | Hides the active banner. |

### Purchases

| Member | Meaning |
| --- | --- |
| `PurchaseIAP(product, Action)` | Purchase with a success-only entitlement callback. |
| `PurchaseIAP(product, Action<IAPPurchaseResult>)` | Purchase with complete status and transaction data. |
| `PurchasePremiumEpisodeUnlock(...)` | Preserves episode target and tracks distinct unlock failures/retry. |
| `GetPrice(product)` | Localized App Store price string. |
| `IsPurchased(product)` | Whether purchase history contains the product receipt. |
| `RestorePurchases(callback)` | Starts App Store restore and reports completion. |
| `RestorePurchases()` | Starts restore and writes completion to the Console. |
| `PurchaseCompleted` | Event for a validated recovered purchase that has no active request callback. |
| `IsSubscribed()` | Latest cached active-subscription state. |
| `CheckSubscription(callback)` | Waits for purchase history before reporting subscription state. |

When subscribing to `PurchaseCompleted`, grant the matching game-specific
entitlement and unsubscribe with the owning object's lifecycle. The built-in No
Ads entitlement is handled internally.

### System UI

| Member | Meaning |
| --- | --- |
| `RequestNotificationPermission()` | Requests OneSignal/iOS notification permission after privacy flow completion. |
| `RequestRateUs()` | Requests the native iOS review prompt. |
| `OpenTerms()` | Opens the configured Terms URL. |
| `OpenPrivacyPolicy()` | Opens the configured Privacy URL. |

### iOS haptics

| Member | Meaning |
| --- | --- |
| `HapticsEnabled` | Saved user preference. Enabled by default. |
| `HapticsSupported` | Whether the current iOS device supports Core Haptics. |
| `SetHapticsEnabled(enabled)` | Saves the preference and immediately stops feedback when disabled. |
| `Haptic(preset)` | Plays any typed `HapticPreset`. |
| `HapticButton/Selection/Toggle()` | Ready-made interaction feedback. |
| `HapticSuccess/Warning/Error()` | Native notification feedback. |
| `HapticLightImpact/MediumImpact/HeavyImpact()` | Ready-made impact strengths. |
| `HapticSoftImpact/RigidImpact/Collision()` | Ready-made physical feedback. |
| `HapticExplosion/Reward/Heartbeat/Tick/LongPress()` | Ready-made game patterns. |
| `HapticContinuousLight/Medium/Heavy()` | Ready-made continuous patterns. |
| `HapticContinuous(duration, intensity, sharpness)` | Plays a custom continuous Core Haptics pattern. |
| `StopHaptics()` | Immediately stops the active continuous or custom pattern. |
