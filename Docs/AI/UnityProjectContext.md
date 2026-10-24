# Unity project context

Updated 2026-10-24 at base commit 22163292.

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
