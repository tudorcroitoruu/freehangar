# Unity project context

2026-10-27 spatial correction: all five Gameplay piles now store local crop
regions and optional buried percentage layers in GrainSpatialField. The original
hangar is always 100% corn; hangars 4 and 5 are exactly 5% corn by starting mass.
The shader samples spatial composition maps, and scoop/settling callbacks move
the same typed material. GrainProgression no longer repaints spatial piles.
Inspector region presets, per-crop inventory and Preview Cleared Percent replace
global kg scrubbing for these hangars. Added 23 Edit Mode and 9 spatial Play Mode
assertions; existing mixed collection/sorting regressions pass. See the newest
GrainPrototype.md section and output/GrainTypes/Hangar5Spatial.png.

2026-10-27 grain expansion: Gameplay has ten crop appearances using an instanced
HDRP shader, mixed shovel cargo and single-crop receiver sorting, retained
overflow, three replacement-sack stations, and persistent progression based on
accepted bagged kilograms (0/100/250/500/1000 kg). GrainPile recipes use whole-pile
harvest budgets, not spatial strata; the last recipe layer fills remaining mass.
Edit Mode includes layer/progression scrubbing and closed-hangar previews.
GrainGallery.unity shows all ten crops. 28 Edit Mode assertions, 33 Play Mode
assertions and existing mass/contact-scoop regressions pass. See the newest
section of GrainPrototype.md for authoring, persistence boundaries and validation.

Terrain checkout recovery, 2026-10-26: the nine damaged Git terrain blobs were
re-staged from intact local sources under the existing binary attributes.
Fresh-GUID imports and byte-identical checkout exports passed. NewMap's seven
layers and active collider binding are valid. This supersedes the unresolved
terrain notes below; see TerrainRecovery.md for evidence and the remaining
commit/push and second-machine verification steps.

2026-10-25 shovel contact revision: scooping selects the center aim-dot hit point (superseding nearest-blade selection), performs an insertion/lever/extraction gesture with contact-gated grain transfer, and temporarily uses world depth for burial. The load is denser at the handle/base (-Z, A) and thinner at the cutting edge (+Z, B). Tilt limits and mass accounting are preserved; aimed scoops have a separate 0.65 m grip envelope to reach across from the right-hand carry pose. Pouring and bag handling require direct aim-dot hits. `output/GrainPrototype/ValidateContactScoop.cs` validates an isolated runtime rig with cleanup; see the latest section of GrainPrototype.md. Unsaved user scene edits were preserved, and the known corrupt NewMapTerrain asset remains unresolved.

Updated 2026-10-24 at base commit 22163292.

Forgiving bag aim: ShovelTool now has 0.35 m Bag Aim Padding for loaded shovels. Direct bag hits win; near misses use a bounded non-allocating sphere cast and require clear line of sight to the bag. Physical collider size, 4 m pour distance and 2.6 m scoop reach remain unchanged. ValidateBagAim.cs verifies six near-miss/obstruction/full-transfer cases.

Rim jitter fix: removed repeated boundary bounce and coarse inward snapping in ShovelGrainPhysics. Settled loads now sleep after 0.6 s of quiet, stable fill; meaningful tool movement or fill changes wake them, while airborne streams continue normally. MeasureRimStability.cs reduced stationary peak displacement from approximately 1.97 mm/frame to zero and checked motion/pour wake-up. ValidateCarryMotion now selects the active container rather than assuming the disabled original Sack exists in the active hierarchy.

Pour targeting fix: the user moved the active small sack and disabled the original Sack. The active bag was about 3.35 m down the aim ray, outside the shared 2.6 m reach. ShovelTool now uses a separate 4 m container range while loaded, still resolving the nearest obstruction and keeping pile reach at 2.6 m. ValidatePourRange.cs reproduces the approach and verifies successful transfer, obstruction rejection and range rejection. Preserve the user's bag layout and disabled original Sack.

Compact-load limits: user confirmed limiting both shovel tilt and corn height. Added a 30-degree upward tilt cap alongside the existing downward limits. ShovelGrainPhysics now caps the entire carried load at local Y=0.075 m and pushes particles inward from high side walls instead of raising their ceiling. Cosmetic spills are reduced to short hops, at most 8 alive and 10 emissions/second. ValidateShovelTilt.cs checks these constraints across 240 simulated pose combinations.

Downward tilt follow-up: ShovelTool clamps the final world-space handle-to-blade downward angle to 25 degrees, or 40 during pouring, after composing camera-relative sway and loaded carry leveling. Handle anchoring is preserved; camera pitch remains unrestricted. ValidateShovelTilt.cs verifies 240 combinations including the controller's 80-degree downward look limit.

Carry polish follow-up: the shovel now has a packed 3D interior below its 192 simulated surface kernels, fitted against a baked 17x17 floor from the imported mesh. Camera turns add smoothed Z roll; collecting corn straightens the Z carry angle until empty. Fast translation/rotation emits bounded cosmetic kernels without changing gameplay mass. The pile's geometry coverage is denser and the recessed substrate matches the corn color. ValidateCarryMotion.cs covers the new behavior with 14 Play Mode checks; existing interaction checks remain in ValidateCornMotion.cs.

Grip and corn presentation follow-up: shovel motion now pivots at the handle and keeps its displacement within 16 cm; pouring raises the blade over the changing sack rim. Smaller, flatter kernels use shuffled golden tones and tighter packing, with the visual substrate recessed below the pile edge. The collider still follows the physical surface. Fixed a tapered-bowl constraint ordering issue (clamp depth before evaluating width). Compilation, model checks, 30 animated interaction checks, local refill/collider checks and camera captures passed. Denser surface geometry is approximately 1.6M triangles before culling, not a Switch performance certification.

Local grain refill follow-up: scoops now wake only their footprint plus a 0.45 m margin; higher neighbors supply a partial refill while distant grid heights remain unchanged. Startup no longer relaxes the whole pile. Volume remains conserved and a residual depression remains. Model checks, a localized Play Mode/collider check, and the 28 animated interaction checks passed. See Docs/AI/GrainPrototype.md and output/GrainPrototype/ValidateLocalRefill.cs.

Grain prototype added 2026-10-24 from f012d770 and revised after visual feedback: Gameplay contains a deformable 33x33 pile, instanced 3D surface kernels, and 50 kg containers. The prominent original Sack is also wired for filling. PlayerController forwards left-click to ShovelTool, including dry swings and one queued follow-up, and composes grip-pivot animation with held-instrument presentation. ShovelGrainPhysics simulates up to 192 contained load kernels and a pooled ballistic pouring stream. Runtime source is Assets/_Scripts/Gameplay/Grain; authoring and model validation are in Assets/Editor. The flat load mesh is disabled. See Docs/AI/GrainPrototype.md for controls, costs, limitations and original Switch/HDRP incompatibility. No console build, gamepad migration or hardware performance claim.

- Confirmed project: F:/Projects/freehangar, Unity 6000.3.20f1.
- Active rendering is HDRP 17.3.0. The earlier 2026-10-18 URP inspection is superseded by the current manifest, active Editor and HDRP migration. Preserve imported material mappings; see HdrpMaterialValidation.txt and HdrpMaterialMigration.tsv.
- Input System 1.19.0 is installed. The first-person PlayerController actively uses legacy UnityEngine.Input axes/keys. It owns CharacterController movement, mouse look, crouch, gravity and held-shovel sway.
- First-party code: Assets/_Scripts; editor utilities: Assets/Editor and Assets/_Scripts/Editor. No separate first-party asmdef was found. Architecture is MonoBehaviour-centric, mostly global namespace, explicit serialized fields and Unity lifecycle callbacks.
- Boot waits for GlobalManager and routes through LoadingScene to Menu. Build Settings still enable ExampleScene only. Gameplay can be played directly; no startup/build-scene changes were made.
- Gameplay.unity has inactive original MAP and active New_Map. Following user adjustments, root Player and Graphics are active. EventSystem remains active and [Bootstraper] inactive.
- New_Map owns the new Terrain, modular hangar, farmyard, hilltop village, vegetation, route markers and a map-only surface grading volume. Root Player and Graphics provide player/sun/sky. Assets are under Assets/NewMap; imported originals and original volume profiles remain unchanged.
- One gameplay camera is enabled. Three disabled review cameras and a current interior marker live under New_Map/07_Reference_Views. Preserved player position (-0.80, 0.04, -8.35), camera height 1.74m, yaw 348.24 degrees, vertical FOV 60 degrees; TAA enabled.
- Unity Pipeline 0.9.1-exp.1 and local Unity CLI are connected. Use unity status and live Editor commands for scene/asset changes; do not edit scene YAML behind the Editor.
- Unity Test Framework 1.6.0 is installed; no targeted first-party test suite was identified for this scene-authoring task.
- Validation: live script compilation, rendered interior/exterior/village previews, serialized-reference audit, and a Play Mode CharacterController walk through the hangar aisle and doorway passed. Zero missing scripts/meshes/materials/unsupported shaders under New_Map. No new Console errors; an older HierarchyDecorator deserialization exception was present in baseline history.
- Revised road: 511.5m, approximately 63.9s at assumed 8m/s; hilltop village is about 12m above the farmyard. Actual sampled maximum grade is about 7.83%. Vehicles remain placed assets; no timed driving, loading, weighing or economy behavior was implemented.
- No standalone player build or performance benchmark was run.

Evidence: ProjectSettings/ProjectVersion.txt, EditorBuildSettings.asset, Packages/manifest.json, Assets/_Scripts/Boot.cs, Global/LoadingScene.cs, Gameplay/Controller/PlayerController.cs, live Gameplay hierarchy, output/NewMap/validation.txt. See Docs/AI/NewMap.md for editing and restoration.

Hill and texture revision evidence: output/NewMap/Revision2/validation.txt and TextureNotes.md. Original Play Mode walk results describe the first-pass environment. Current Console baseline included historical duplicate-sun-shadow errors; new-error checks use cursor 996. See current revision evidence for follow-up results.

Vegetation follow-up: 150 additional beech trees on the upper slopes and behind the village, plus 19,297 grass patches in 262 spatial cells with two LODs. Stored under New_Map/05_Landscape/Dense meadow and hill groves. A fourth disabled review camera, Hillside Review, was added. Route clearance and reference audits pass; evidence in output/NewMap/Vegetation. The Editor restarted during the first unbatched import; RecoverDenseVegetation.cs reused saved meshes and completed placement with batched imports.

Original farm style follow-up: outdoor terrain now blends six local copies of the original farm terrain layers at 1024 alphamap resolution. The red TFP_Barn_04A plus extensions wraps the existing furnished hangar using fitted local render/collision meshes. Original curved dirt/rut meshes and shoulder rocks are in 09_Original_Road_Details; 318 additional native grass/flower patches are in 05_Landscape. Group 10_Power_Line contains 20 original wooden poles, 19 connected native cable spans and three hangar service wires. Sampled minimum wire clearance is 5.24m. Final reference, road and doorway capsule audits pass; rendered previews and reports are in output/NewMap/OriginalStyle. No new Play Mode walk or performance benchmark was run for this pass. Preserve the user's current Graphics/profile/quality changes; imported source assets were not modified by this pass.

Road simplification follow-up: six active rut sections (80% fewer), brighter outdoor dirt blend using the prior packed-earth terrain layer, and an irregular rounded crop field with feathered edges and curved fencing. New verge/fence pieces exclude an 8m road corridor. Terrain heights and road alignment unchanged; route/reference audits pass. Latest evidence: output/NewMap/RoadRefinement.

Tall hangar revision: active modular hall at 01_Modular_Hangar/Tall timber hall with glazing; 20x30m, 12m eaves, 16m ridge, 10x9m clear entrance. Six glazed high wall windows and four framed skylights, grey interior panels, oak framing and red exterior with existing side wings. Previous low architecture and old red central shell disabled. Truck/props/player preserved. Evidence in output/NewMap/TallHangar; read-only clearance/reference validation and camera captures, no standalone build or performance benchmark.

Hangar material refinement: weathered grey plank texture/derived normal, native 1254px, physically scaled UVs on 406 surfaces, directional oak grain, textured red exterior and 72 iron fasteners. Evidence: output/NewMap/HangarMaterials. Geometry/layout/player/lighting unchanged.

Lamp/prop pass: three bell-shaped pendants with warm textured HDRP emission and existing real lights; 14 original farm prefab instances grouped along hangar walls, including ladders, pallets, tools, containers and rear steps/landing. Source assets unchanged. Current evidence and successful clearance/reference checks: output/NewMap/LampsAndProps.

Road bump follow-up: real terrain undulations/crown/shallow wheel depressions, max displacement 0.352m and dense sampled maximum longitudinal grade 14.95%. Earlier 7.83% grade is superseded. Road details and nearby props refitted, centerline CSV heights updated; clearance audit passes. Vehicle dynamics not playtested. Evidence and restoration data: output/NewMap/RoadBumps and terrain backup asset documented in NewMap.md.

Variable road-width pass: 5.2-10m dirt core with asymmetric feathered shoulders, wider bends/passing areas and narrower stretches; 271 verge grass patches, 26 rocks moved outward. Existing heightmap bumps unchanged, route clearance/reference audits pass. Evidence output/NewMap/VariableRoad.

2026-10-26 five-hangar map pass: Gameplay contains the original starter hall plus four larger, closed halls, the S cottage/shop, varied surrounding farm plots, perimeter collision/hills and consolidated static LOD meshes. Scene/assets and validation details are in NewMap.md, section Five-hangar farm layout. Shop purchasing and hangar progression logic are intentionally deferred by the user. CornSurfaceKernels now initializes its GrainPile explicitly before building/caching its generated surface, removing a scene-load OnEnable ordering dependency discovered during this pass.

2026-10-26 follow-up: perimeter/backdrop hills now have combined low-poly tree groves with spatial LODs. S shop/cottage have textures and stocked tools/bags/crates, with six passing Play Mode access/counter checks. Split-rail farm fences follow both road shoulders; the former square village-approach dirt stamp has been repainted as a curved, feathered road edge. See the latest NewMap.md section and output/NewMap/{FinishedShop,RoadsideFarmyards,FiveHangars}. Gameplay buying/unlocking remains deferred.

2026-10-26 scene light controller: root Scene Light Visibility in Gameplay controls ten realtime non-directional lights in Play Mode, explicitly bound to the untagged Player/Main Camera. View-gated light enabling and 18m shadow distance (2m exit buffer), cached registry and 20Hz updates preserve authored states on disable. Directional/baked/mixed lights are excluded. Reuse via Tools/Lighting/Add Scene Light Visibility Controller. Runtime source: Assets/_Scripts/Rendering/SceneLightVisibilityController.cs; editor setup/inspector: Assets/Editor/SceneLightVisibilitySetup.cs. 37 Play Mode checks and Gameplay restoration/direction probes passed; output/Lighting contains evidence. See NewMap.md for configuration and ownership details.

2026-10-26 nearby-light follow-up: realtime local lights now remain enabled in every viewing direction within an 8m camera radius, with a 1m exit buffer. Radius is configurable and zero disables the exception. Shadow distance stays independent. Gameplay configuration saved; expanded 50-check Play Mode validation passed.

2026-10-26 tree/grain performance follow-up: the final, moderate tree pass supersedes the rejected 8-triangle billboard reduction. Ordinary trees retain complete native 3D LOD1/2/3/4 (first threshold 0.5); hill cells retain native 3D LOD2/3/4. No active tree billboards. Highest-detail triangle budget is 41% below the original. HangarGrainVisibility keeps hangars 2-5 grain GameObjects inactive until SetHangarOpen(true); the starter remains active. Tree, map-reference and open/closed grain lifecycle checks pass. See NewMap.md and output/TreeOptimization for final evidence.

2026-10-26 final follow-up: tree detailed ranges now approximately 110m inside /90m Outer groves with solid 3D distant crowns and tree-only foliage alpha clipping. CornSurfaceKernels uses spatially coherent submission and tight surface LOD bounds, preserving all seeded kernel transforms while reducing same-view triangles by 33.4%. Gameplay now includes the red 8,064-triangle bicycle with separated moving parts, centered +Z pivot, E mount/dismount, first-person arcade riding and rear bag/box cargo. See Bicycle.md, GrainPrototype.md and the final NewMap.md entries for evidence and limits.

2026-10-26 bicycle controls follow-up: smooth steering (0.14s), Undo-aware Adjust Riding Position Inspector window with seated preview, runtime V/click camera adjustment with local saved preferences, and Shift forward boost (1.5x speed, 12 stamina/sec) sharing StaminaManager and HUD. Exhaustion/recovery and mounted Play Mode shutdown validated. 24 isolated runtime checks and six Editor checks passed; scene/prefab saved. See Bicycle.md.

2026-10-26 bicycle steering repair: a common inclined headset pivot now carries handlebars/fork/front brake/mudguard and front wheel, with axle-only wheel spin. A two-bone cable skin connects moving lever to frame. Source is Bicycle_Rigged.fbx / ArtSource/Bicycle/Bicycle_Rigged.blend, preserving earlier originals. Full-lock/axle/cable checks and first-person captures pass. Bike camera pitch amplitude reduced 1.5x to 0.3 degrees. See Bicycle.md for evidence.

2026-10-27 Motoroller: original scooter preserved inactive; optimized 12,443-triangle rideable prefab placed at its parking position. Common inclined steering and axle pivots; MotorollerController composes BicycleController movement with engine settings and length-aware collision. PlayerBicycleInteraction selects aimed bikes/scooters, handles camera/mount/dismount and rear cargo. 27 scooter + 11 targeting/cargo + 22 bicycle regression checks passed. See Motoroller.md.

2026-10-27 Trike: Gameplay now uses Trike_Driveable.prefab with upright +Z-forward baked mesh copies, preserved articulated steering, three wheel animation, shared motor drive, driver camera and four bed cargo anchors. Original Trike is inactive. Body-sized sweep and wider exit/unload clearances configured. 47 Trike checks plus 49 bicycle/Motoroller regressions passed; see Trike.md.

2026-10-27: Added driveable lawnmower + jointed tow trailer prefab using existing motor vehicle interaction; details in Docs/AI/Lawnmower.md. Bicycle rack now holds one bag OR two boxes centered above its rear rack, with exclusiveBagSlot enforcing shared capacity.
