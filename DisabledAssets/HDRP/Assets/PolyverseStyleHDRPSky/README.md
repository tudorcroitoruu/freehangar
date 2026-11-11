# Polyverse-style custom sky for Unity 6 HDRP

This is an HDRP custom-sky implementation based on the feature set of the supplied URP/legacy skybox shader.

## Included

- `Runtime/PolyverseStyleSky.cs`
  - HDRP Volume component containing the sky properties.
- `Runtime/PolyverseStyleSkyRenderer.cs`
  - HDRP `SkyRenderer` implementation.
- `Runtime/PolyverseStyleSkyLightBridge.cs`
  - Copies the sun and moon Directional Light orientations into the sky component.
- `Runtime/PolyverseStyleSkyTimeController.cs`
  - Optional time-of-day controller for Day, Night and Full 24 Hours modes.
  - Drives sun/moon direction, cloud wind offset, cloud fade, moon phase and stars fading near local lights.
- `Shaders/PolyverseStyleSky.shader`
  - Fullscreen HDRP custom-sky shader with gradient/cubemap background, pattern overlay, procedural or cubemap stars, procedural or texture sun/moon, procedural or cubemap clouds and horizon fog overlay.
- `Editor/PolyverseStyleStarCubemapGenerator.cs`
  - Creates `Generated/Polyverse_DefaultStarsCubemap.asset`, a procedural RGB star cubemap for the `Stars Cubemap` slot.

## Installation

1. Copy the entire `PolyverseStyleHDRPSky` folder into your Unity project's `Assets` folder.
2. Wait for Unity to compile.
3. Create or select a global HDRP Volume.
4. Add `Visual Environment` and set **Sky Type** to **Polyverse Style Sky**.
5. Add the `Polyverse Style Sky` override to the same Volume.
6. Make sure the camera uses **Sky** as its clear mode and that the camera's Volume Layer Mask includes the Volume layer.
7. Optional: add `PolyverseStyleSkyTimeController` to a GameObject and assign:
   - the global sky Volume;
   - the camera;
   - the Sun Directional Light;
   - the Moon Directional Light.
8. Alternative: add `PolyverseStyleSkyLightBridge` to a GameObject if another time controller already rotates your lights. Assign:
   - the global sky Volume;
   - the Sun Directional Light transform;
   - the Moon Directional Light transform.
9. In a player build, add `Hidden/HDRP/Sky/PolyverseStyleSky` to:
   - `Project Settings > Graphics > Always Included Shaders`
   if your build strips the shader.

The time controller can create the required `Polyverse Style Sky` and `Visual Environment` overrides on the assigned Volume when `Force Custom Sky On Volume` is enabled.

## Important HDRP differences

- The old shader's built-in fog colour was based on `unity_FogColor`. HDRP does not use that legacy fog path. This port has an explicit **Horizon Fog Color** for the visual sky overlay.
- Use HDRP's normal **Fog** or **Volumetric Fog** Volume override for fog that must affect scene geometry.
- The custom sky is rendered by HDRP's sky system and therefore participates in environment lighting/reflection cubemap updates.
- The inherited HDRP sky `Rotation`, `Exposure`, `Multiplier`, and `Update Mode` properties remain available.
- The time controller leaves realtime environment updates disabled by default because forcing realtime environment cubemap updates while tuning can make the editor stall.

## Quick debug

If the sky does not visibly change:

1. Enable `Debug Solid Color` on the `Polyverse Style Sky` Volume override.
2. Set `Sky Color` to a clear test color.
3. Enter Play Mode and check for this one-time log:
   - `[PolyverseStyleSkyRenderer] Rendering Polyverse Style Sky.`
4. If the log appears but the view does not change, check the camera clear mode and Volume Layer Mask.
5. If the log does not appear, the active `Visual Environment` is not using `Polyverse Style Sky`, or another higher-priority Volume is overriding it.

## Day/night use

Use `PolyverseStyleSkyTimeController` when you want this package to own the sky clock. Use `PolyverseStyleSkyLightBridge` when another controller already owns time and light rotation.

The controller supports:

- `Day`: loops between `Day Start Hour` and `Day End Hour`.
- `Night`: loops between `Night Start Hour` and `Night End Hour`.
- `Full24Hours`: loops the entire 24-hour range.

It drives these sky parameters every frame:

- `Sky Color`, `Equator Color`, `Ground Color`
- `Stars Visibility`
- `Sun Intensity`
- `Moon Intensity`
- `Sun Direction`, `Moon Direction`
- `Moon Phase`
- `Clouds Offset`
- `Clouds Fade`

## Clouds

Disable `Enable Clouds Rotation` for wind-style movement. The controller moves clouds through `Clouds Offset` using `Wind Direction` and `Wind Speed`.

`Clouds Fade` is separate from `Clouds Opacity`. Use `Clouds Opacity` for the preset strength and `Clouds Fade` for appearance/disappearance animation.

Leave `Clouds Cubemap` empty for individual stylized 3D cumulus clouds in a
horizontal X/Z layer. Each cloud smoothly merges five ellipsoid volumes, with
seeded dimensions and orientation. A bounded 16-step integration gives the clouds
an underside, including directly overhead, without the old polar distribution or
internal lobe-normal seams. Clouds move linearly along X and Z. Perspective reduces
their apparent size with distance; additional distance scaling and fading remove
far clouds smoothly. Birth/death uses 3D noise erosion of density, breaking the cloud
into fragments instead of uniformly fading its alpha. Distance fading remains a
separate effect.

Cloud occupancy is stratified across 4x4 groups rather than independently random
at every site. Each group receives a similar share of the requested coverage,
with scrambled ranks and jittered positions. Lifetimes are staggered among
occupied sites and clouds spend longer at full size, reducing large empty patches
while keeping noise breakup during formation and dissipation.

Use `Tools > Polyverse Style Sky > Enable Animated Procedural Clouds` to configure
the active scene's current Polyverse profile, disable the separate HDRP volumetric
cloud layer, and enable animated Scene view preview. This modifies only cloud
settings and supports Undo; the current sky colors and sunlight are preserved.

- `Clouds Coverage`: 0 is clear sky, 1 is a full cloud layer.
- `Clouds Scale`: lower values produce fewer, larger clouds.
- `Clouds Softness` and `Clouds Detail`: edge softness and variation between cloud silhouettes.
- `Clouds Wind Velocity`: linear X/Z velocity in layer-height units/second; `(0.003, 0.001)` means `(3, 1)` m/s at a 1000 m layer height.
- `Clouds Evolution Speed`: independent lifetime/erosion rate; try `0.025` (roughly 2–3 minute lifetimes). Set it to 0 to freeze shapes and lifetimes.
- `Clouds Deformation`: gentle movement of the merged volume lobes.
- `Clouds Layer Altitude`: horizontal layer height in metres (default 1000).
- `Clouds Distance Fade`: start/end distances in metres (default 3000/8000).
- `Clouds Distant Scale`: extra size reduction at distance (default 0.35).
- `Clouds Breakup`: noise erosion during formation/dissipation (default 0.8).
- `Clouds Horizon Fade`: removes cloud clutter near the horizon.

For completely still procedural clouds, set wind velocity and evolution speed to
zero, disable cloud rotation, and stop any time controller's wind. With a time
controller attached, its existing Wind Direction/Speed drive translation through
Clouds Offset; shader wind is disabled on that runtime profile to avoid double movement.

Assign an RGBA cubemap for authored cloud shapes (RGB lighting, alpha coverage).
This retains its previous offset/rotation behavior and does not use procedural
shape or evolution controls. Procedural cloud volumes are integrated inside the
sky shader; they do not cast cloud shadows onto scene geometry.

Animation works without a time controller in Play Mode. In the Scene view, enable
Always Refresh / animated effects to preview it. Environment cubemaps still follow
the sky's Update Mode; changing the visual sky does not force expensive realtime
reflection updates every frame.

`Tools > Polyverse Style Sky > Validate Cloud Animation` runs GPU checks for
independent wind/lifetimes, frozen animation, continuity across X/Z boundaries,
overhead coverage, distance fading, noise breakup, coverage endpoints, the lower
hemisphere and the existing cubemap path. Results and evolution snapshots
are written under `Library/PolyverseCloudValidation.txt`, `Library/clouds-evolution-*.png`
and `Library/clouds-zenith-*.png`. `Preview Clouds Overhead` aims only the Scene view
straight up to check the underside; it does not move gameplay cameras.

## Cloud rotation and maximum elevation

The `Cloud Lighting` box lights procedural cloud lobes using normals from the
smooth merged volume, avoiding hard seams between ellipsoids. `Clouds Follow
Scene Sun` uses HDRP's sun direction and a gentle tint from its colour/temperature;
without a scene sun it falls back to the sky's `Sun Direction`, independently of
the sun-disc toggle. Tint is normalized rather than using lux as skybox brightness.
`Clouds Direct Light` controls directional contrast, `Clouds Ambient Fill` fills
shadows, `Clouds Light Softness` softens the light/shadow boundary, and
`Clouds Silver Lining` adds an edge when viewing clouds toward the sun.
Light and shadow colours remain art controls. Lighting does not change opacity
or distance fading. Cubemap clouds keep their authored lighting.

`Enable Clouds Rotation` toggles circular motion. Use `Clouds Rotation Axis`
`(0, 1, 0)` to rotate around the horizon and `Clouds Rotation Speed` for degrees
per second. Linear X/Z wind remains independent and can run simultaneously.
For rotation only, set the wind to zero (in the time controller when it drives drift).

`Clouds Max Height` limits cloud elevation above the world horizon from 0 to 90
degrees. Try 15–30 for clouds near the horizon and clear sky overhead. Zero hides
clouds; 90 allows overhead clouds. The upper edge fades softly over 5 degrees.
This limit applies to procedural and cubemap clouds, independently of rotation.
`Clouds Layer Altitude` separately controls the physical layer height in metres.

## Stars and local lights

`Stars Visibility` is the final multiplier for stars. The time controller combines night amount with local light fading:

- Far from local point/spot lights: stars can reach `Max Star Visibility`.
- Near local point/spot lights: stars fade out based on light range and intensity.
- Directional lights are ignored for this local-light fade.

Assign `Local Lights` manually for exact control, or leave it empty and the controller periodically scans scene lights.

The package auto-generates a default star cubemap at `Assets/PolyverseStyleHDRPSky/Generated/Polyverse_DefaultStarsCubemap.asset` after Unity recompiles. You can regenerate it from `Tools > Polyverse Style Sky > Generate Default Stars Cubemap`.

## Moon phases

`Moon Phase` uses this range:

- `0.0`: full moon.
- `0.5`: new moon.
- `1.0`: full moon again.

The in-between phases progress in the reversed direction, so the crescent/gibbous sides are swapped compared to the original shader.

`Moon Dark Side Visibility` controls the shadowed side created by the phase mask:

- `0.0`: hidden side disappears completely.
- `0.1` to `0.25`: hidden side remains faintly visible and darker.
- `1.0`: the phase mask no longer visually hides the moon.

`Moon Phase Roundness` controls how pronounced the crescent/gibbous inner arc is:

- `0.5`: straighter, flatter phase boundary.
- `1.0`: moderately rounded phase boundary.
- `1.6`: clean stylized semicircle for crescent and gibbous phases.
- `2.0` to `4.0`: exaggerated crescent/gibbous curve.

`Moon Phase Fade` controls how soft the line between the lit and shadowed sides is:

- `0.0`: perfectly sharp phase line.
- `0.2`: visible but noticeably softened phase line.
- `0.5`: practical smooth fade where the line is not obvious.
- `1.0`: very broad fading across the terminator.

The controller can advance the value automatically with `Advance Moon Phase`.

## Weather use

For different weather presets, use separate Volume Profiles containing:

- Polyverse Style Sky values for procedural or cubemap clouds and horizon appearance;
- HDRP Volumetric Clouds where physically volumetric clouds are desired;
- HDRP Fog/Volumetric Fog;
- Exposure, White Balance and Color Adjustments;
- VFX Graph rain/snow effects managed separately.

Float and colour properties can blend through Volume weights. Cubemap and texture properties are object references and can switch rather than smoothly interpolate, so a fully seamless cloud-texture crossfade needs a two-cubemap blend extension.

## Reference daylight preset

The reusable preset is `Assets/Scenes/Gameplay/Polyverse Reference Daylight.asset`.
This is a separate copy of the original sky/fog profile. It combines the Polyverse
blue gradient with a pale horizon, softer distance fog, animated Polyverse cumulus
clouds, and warm directional sunlight. The sun's intensity and orientation are
preserved. HDRP volumetric clouds, moon and stars are disabled for this daytime reference.

Use `Tools > Polyverse Style Sky > Apply Reference Daylight` to apply the preset
to the active scene's existing global sky Volume. The change supports Undo and
saves the active scene. `Preview Reference Horizon` moves only the Scene view
to an elevated exterior viewpoint; it does not move gameplay cameras.

The preset approximates the supplied sunny farm image. Exact cloud silhouettes
and the full landscape comparison require further art direction and an intact
terrain asset; this preset does not repair or replace the terrain.

## Compatibility

Designed against Unity 6 HDRP 17.x custom-sky APIs. The code could require small API adjustments on materially older HDRP package versions.
