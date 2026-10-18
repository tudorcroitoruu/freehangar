# SDK Folder Guide

The complete developer API is documented in `Assets/README.md`.

## Structure

- `Foundation/Runtime` — automatic initialization and internal wrappers for
  analytics, ads, attribution, IAP, notifications, reviews, encrypted saves,
  and native iOS haptics.
- `Foundation/Editor` — JSON synchronization, immediate build validation, iOS
  post-processing, SDK version checks, and generated IAP product tooling.
- `Game` — the only SDK area intended for game-specific types: ad placements,
  partial `PlayerData` fields, and generated typed IAP products.
- `Resources/SDKSettings.asset` — generated compiled configuration used by builds.
- `Resources/DOTweenSettings.asset` — shared DOTween project settings.
- `Vendor` — imported third-party distributions; never patch these files.
- `skad.txt` — canonical SKAdNetwork source used for iOS post-processing.

## Ownership

Game developers use `SDKManager` and edit only:

- `Assets/sdk_config.json`;
- `Assets/SDK/Game/AdPlacements.cs`;
- their own partial `PlayerData` file under `Assets/SDK/Game`.

DOTween is available directly through `using DG.Tweening;`. Developers may use
its public API but must not modify the distribution under `Vendor/DOTween`.

`Assets/SDK/Game/IAPProducts.cs` and `Assets/SDK/Resources/SDKSettings.asset` are
generated. Do not edit them manually.

Do not initialize vendor SDKs, access Foundation classes, or place project-specific
code inside `Vendor`. The Foundation is the only integration layer.
