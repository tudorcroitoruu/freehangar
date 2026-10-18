# Vendor SDKs — Do Not Edit

This folder contains imported third-party SDK distributions. They remain isolated
from project code so complete SDK folders can be replaced during updates.

Installed Foundation dependencies:

- Meta/Facebook Unity SDK using FBSDK iOS 18.1.0;
- Tenjin Unity SDK 1.19.1;
- GameAnalytics Unity SDK 8.0.1;
- Unity LevelPlay package 9.5.0 and matching adapter dependency assets;
- Unity IAP package 5.4.2;
- OneSignal Unity packages 5.3.2;
- DOTween 1.3.030;
- External Dependency Manager for Unity 1.2.169.

Configured LevelPlay mediation networks:

- Unity Ads
- Google AdMob
- AppLovin
- Chartboost
- Meta Audience Network
- DT Exchange
- InMobi
- Liftoff Monetize

Project-specific behavior belongs only in `Assets/SDK/Foundation`. Never modify a
vendor file to add callbacks, settings, analytics, ATT behavior, or game logic.

After replacing or updating a complete SDK/package:

1. Let Unity import and resolve dependencies.
2. Run `SDK > Validate Configuration`.
3. Run `SDK > Check SDK Versions`.
4. Confirm the LevelPlay adapter set still exactly matches `Assets/sdk_config.json`.

The README and changelog files inside individual vendor folders belong to those
SDK distributions and are intentionally preserved unchanged.
