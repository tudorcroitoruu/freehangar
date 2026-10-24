# Grain interaction prototype

Updated 2026-10-24 in Unity 6000.3.20f1. Includes 3D kernel presentation and localized refill after scooping.

## Local refill

A scoop wakes only its footprint and a 0.45 m surrounding margin (0.85 m radius for the default shovel). Higher neighboring material feeds the excavation and loses the same volume. Distant parts of the pile remain unchanged, and the pile no longer relaxes globally on startup. The local solver distributes the removed-height deficit relative to the original slope, respects downhill transfer, and leaves a small residual depression. The slope setting controls the residual rim; this is a localized disturbed-material approximation, not a global angle-of-repose simulation. Without higher donor grain, the hole remains. No volume is regenerated.

Validated example: removing 5 kg at local (0, -1.5) lowered the surface from 0.8000 m to 0.7712 m; nearby grain then raised it to 0.7894 m over 21 settling steps (about 1.4 seconds at 15 Hz). The final spot stayed about 1.06 cm lower. The Play Mode check verified 1,020 distant grid vertices were exactly unchanged, total mass was conserved, and the collider matched the settled surface within 1 mm. Reusable validation: output/GrainPrototype/ValidateLocalRefill.cs.

## Play

Open Assets/Scenes/Gameplay.unity and enter Play Mode. Left-click to swing the shovel, including when aiming at empty air, scenery, an empty pile, a full shovel, or a full sack. A click during a gesture queues one subsequent gesture; the queue cannot grow indefinitely. Escape releases the cursor and the next click captures it.

Approach the small conical pile ahead/right of spawn, aim at it within 2.6 m, and click. The motion pulls back, inserts, curls, lifts, and returns over 1.25 seconds. During contact, up to 5 kg transfers progressively into the shovel. With corn loaded, aim at an active open sack within the separate 4 m Pour Reach and click to pour. Each holds 50 kg; overflow remains on the shovel. Their fill morphs follow accepted mass. The nearest obstruction still blocks pouring, and scooping remains limited to 2.6 m. The decorative large stockpile geometry is not made interactive by this prototype.

## Ownership and rendering

Bag aiming has a configurable 0.35 m margin around the physical collider (ShovelTool > Bag Aim Padding). With corn loaded, a bounded sphere query finds nearby open containers when the centre aim ray misses. Direct container hits take priority, and a separate unobstructed ray must reach the candidate bag's centre or opening. This aiming margin does not enlarge the physical collider or extend the 4 m pour distance. ValidateBagAim.cs checks left/right/above near misses, padding bounds, wall rejection and a complete edge-aim pour.

The shovel's final presentation is limited to 25 degrees downward and 30 degrees upward relative to the world horizon, including camera pitch, sway and action rotation. Pouring allows 40 degrees downward. These limits are serialized on ShovelTool. Extra Pour Roll adds up to 12 degrees of negative local-Z rotation during the pouring gesture, easing in toward the sack and returning afterwards. The correction pivots at the handle, preserving the 16 cm grip movement limit and leaving camera look unrestricted. ValidateShovelTilt.cs checks 240 loaded idle/action and camera pitch/yaw/roll combinations after PreviewCornCarry, including looking down at 80 degrees, and verifies the corn height bound while simulating each pose.

- GrainHeightField owns a 33 x 33 volume grid on a 6 m patch. Removal and neighboring slope relaxation conserve volume; 720 kg/m3 is an illustrative configurable bulk density. GrainPile converts volume to kg and maintains the visual surface and one MeshCollider.
- CornSurfaceKernels renders roughly 24,900 rounded 64-triangle kernels at the current 0.0315 m spacing using GPU instancing, in batches of at most 1,023. Smaller, flatter kernels have a broad crown and narrower germ end. Three muted golden materials are shuffled across the surface; position, tilt, heading and size also vary. Kernel transforms follow the changing pile surface; their positions are not independent PhysX bodies. The plain substrate is recessed 4.5 cm to bury the orange polygon apron at ground level; collision retains the actual height-field surface. Detail is omitted beyond 22 m.
- ShovelTool owns held mass and the timed action. It accepts dry swings and checks line of sight/range during the transfer interval. Animation pivots at a handle anchor; its displacement is limited to 16 cm in camera space, including when aiming at distant surfaces. Pouring adjusts the grip rotation to clear a filling sack's rim. Interaction remains aim-based, not blade-mesh collision based. No grain is reserved at action start; cancelling retains whatever has already transferred.
- ShovelGrainPhysics owns up to 192 simulated top-layer kernels plus a pool of 432 packed interior kernels (287 visible in the current full-bowl fit). The packed layers grow progressively and follow the actual metal floor, baked into a 17x17 grid by the authoring tool; kernels outside the interior are omitted. This fills the bowl volume without adding hundreds of extra pairwise physics bodies. Moving kernels enter at the cutting edge with inward velocity. A local sphere solver applies gravity, inertial response, damping, pair separation, bounce, friction and tapered bowl constraints using three substeps. Rotations are normalized after integration. The original overflowing load sheet remains disabled.
- PlayerController measures actual camera turning and applies a smoothed Z roll, capped at 7 degrees empty and reduced to about 2 degrees when loaded. ShovelTool smoothly straightens the bowl's Z roll during pickup and keeps that carry pose until unloading, preserving the authored forward tilt. All these rotations share the handle pivot.
- The carried load has a hard top at bowl-local Y=0.075 m, including a kernel-size margin. Kernels meeting high side walls move inward rather than lifting the ceiling. Packed interior geometry also obeys this cap. Maximum Load Height is serialized on ShovelGrainPhysics.
- Rim contacts remove outward velocity without rebounding. Invalid side-wall positions project to the valid boundary instead of repeatedly jumping a large distance inward. After 0.6 seconds of quiet, stable fill, the load sleeps in bowl space; fill changes or substantial tool movement wake it. Ballistic pouring particles continue updating independently. MeasureRimStability.cs reproduced about 1.97 mm/frame peak idle jitter before the fix and measured zero movement after it, also verifying wake-up and re-settling.
- Fast turns or abrupt movement emit extra cosmetic kernels into the existing 256-slot ballistic pool, capped at 10 emissions/second and 8 cosmetic kernels alive at once. Low upward launch speed keeps the bounce close to the bowl. They bounce, expire, and never remove held kilograms, alter the pile, or add grain to a container. Empty shovels emit nothing. Packed layers have no independent physics; only the top layer and airborne particles move independently.
- Pouring recycles up to 256 world-space particles with ballistic velocity, gravity, world collision raycasts and bounce. Particles disappear on entering the receiver collider; stray cosmetic particles have finite lifetimes. Gameplay mass transfers progressively, independently of cosmetic kernel count. A visual kernel represents a portion of the load, not one measured real-world kernel.
- GrainContainer handles capacity and optional SackController morphs. The visible original Sack is now wired as a container in addition to the small prototype sack. Sack placement and scale were preserved. Their box colliders are fitted in the correct imported coordinate space and interpolate between baked empty/full body bounds, so the pour point follows the rim. The interactive pile was shifted 0.8 m right and 0.4 m back to avoid intersecting the visible sack.

The pile uses bulk height-field physics; the shovel uses a small custom granular solver. This is not a full discrete-element simulation of every kernel in the stockpile. The pile supports slopes and holes but not tunnels, overhangs, moving piles, spilling outside its fixed boundary, or mixed grain types. Unit world scale and an upright pile transform are required; yaw is supported. No delivery, selling or persistence is implemented.

## Costs and platform limits

The core grid has at most 2,048 triangles. Local refill runs at up to 15 Hz over the disturbed neighborhood and sleeps when stable. Collider cooking is limited to 5 Hz, including progressive scoops, so physical contact can lag the surface by up to 0.2 seconds. Near-pile kernels add approximately 1.6 million triangles before culling at the current settings; the denser presentation increases GPU cost and requires target-hardware profiling and LOD tuning. The bowl solver has a fixed particle cap and allocates its working arrays at startup. It does not instantiate GameObjects per kernel.

This revision has not been benchmarked or built on Nintendo Switch. The current project is HDRP, which Unity 6000.3 does not support on the original Switch; a supported pipeline such as URP is still required. The simulation code is pipeline-independent, while authored materials currently target HDRP. Existing legacy movement and mouse input remain; gamepad mappings are not implemented.

Platform reference: https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison

## Authoring and validation

Bag interaction range regression: the moved active sack was approximately 3.35 m down the aim ray from the standing position, beyond the original shared 2.6 m range. ValidatePourRange.cs reproduces that approach after PreviewCornCarry, confirms a full 5 kg transfer, and checks blocked and over-4 m attempts transfer nothing. Disabled decorative/alternate sacks and the user's placement are preserved.

Use Tools > Grain > Create playable prototype for a scene without a Grain Prototype group. Tools > Grain > Upgrade to 3D kernels and grain motion updates the existing prototype and fits the visible Sack. Both author through the live Editor; save the scene afterwards. Source assets are in Assets/GrainPrototype and runtime scripts in Assets/_Scripts/Gameplay/Grain.

Tools > Grain > Validate mass and capacity tests volume conservation, capacity, excavation, settling into a hole, invalid/missed scoops and exhaustion. For animated regression checks, start a fresh Play Mode session and run:

```powershell
unity command script run --file output/GrainPrototype/ValidateCornMotion.cs --entry ValidateCornMotion.Main --timeout 60 --format json
```

The test disables/repositions the player and fills a sack in Play Mode. It advances the same public action API over real frames while the grain solver updates, checks dry and queued gestures, gradual pickup/pouring, mass conservation, full conditions, retained overflow, cancellation, obstruction and bowl containment. Leave Play Mode to restore the saved scene. It does not automate physical mouse clicks.

Validation: runtime and editor scripts compile; mass/capacity model checks pass; 30 animated Play Mode assertions cover gestures, grip bounds, pickup, pouring, capacity, containment and cancellation. ValidateCarryMotion.cs adds 14 checks for the packed interior, loaded Z leveling, opposite turn roll, translation/rotation spills, unchanged mass, expiry and the empty state. Captures are stored in output/GrainPrototype. No standalone build or hardware performance guarantee.

