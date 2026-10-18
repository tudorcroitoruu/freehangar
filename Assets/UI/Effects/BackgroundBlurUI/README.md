# Background Blur UI

`Foundation.UI.Effects.UIBackgroundBlur` is a reusable one-shot GPU blur for Unity
UI. It captures the fully composed frame, including cameras, URP output, and UI
already rendered behind the target. It does not use `ReadPixels` or CPU texture
copies.

## Setup

1. Select a UI object with a `RectTransform`.
2. Add `UI > Background Blur (One Shot)`.
3. Enable the object.

No Inspector references are required. The component captures at the end of the
first frame and inserts a generated `RawImage` directly behind the target.

An existing `Image` or `RawImage` supplies the target shape and alpha mask.
`Simple`, `Sliced`, `Tiled`, and `Filled` images are supported. Without a source
texture, the result is rectangular.

Parent `CanvasGroup` alpha is respected. Animating the target's local
`CanvasGroup.alpha` fades the generated blur without capturing again.

## Controls

- `Downsample` — texture resolution reduction; `4` is the fast default.
- `Frost Strength` — additional frosted softness without extra shader passes;
  default `3`.
- `Blur Iterations` — blur pass count; default `4`.
- `Blur Radius` — radius in downsampled pixels; default `1`.
- `Tint` — optional final color tint.
- `Edge Padding` — extra captured pixels; `0` selects a safe automatic value.

## Runtime API

```csharp
using Foundation.UI.Effects;

UIBackgroundBlur blur = GetComponent<UIBackgroundBlur>();
blur.RefreshBlur(); // discard the old snapshot and capture again
blur.ClearBlur();   // remove the generated image and release GPU resources
```

The component intentionally does not recapture every frame. Call `RefreshBlur()`
only after the visible background changes and a fresh snapshot is required.

Disabling or destroying the component releases its generated `RawImage`, render
texture, and materials. Edit Mode preview is supported; preview-generated objects
are not saved into the scene.
