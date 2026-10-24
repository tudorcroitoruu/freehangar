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
