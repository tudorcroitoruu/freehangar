# Gameplay — New_Map

Updated 2026-10-24 after the original farm style revision.

## Open and inspect

Open Assets/Scenes/Gameplay.unity and press Play. The current root Player and Graphics objects are active; the user's adjusted spawn (-0.80, 0.04, -8.35), camera height 1.74m, yaw 348.24 and 60-degree vertical FOV were preserved. Movement bindings remain unchanged.

The new environment is grouped under New_Map:
- 01_Modular_Hangar: original hall kit, timber cladding, clear-span frames, sliding doors, fitted skylight bays and pendant lights.
- 02_Hangar_Props: container shelves, sacks, crates, barrels, red truck, wheelbarrow, filling sack and maize piles.
- 03_Farmyard: silos, weighbridge, office, vehicles and signs.
- 04_Mini_Village: houses, barn, silo and water tower, on a hill approximately 12m above the farmyard.
- 05_Landscape: trees, vegetation, crop field, terrain-fitted fences and utility poles.
- 06_Road_Route: sampled centerline markers.
- 07_Reference_Views: interior marker and disabled Exterior Review, Map Overview and Village Review cameras.
- 08_Surface_Grading: map-only HDRP grading override. Original Graphics profiles remain unchanged.
- 09_Original_Road_Details: terrain-fitted original curved dirt/rut meshes and rocky shoulders.
- 10_Power_Line: original wooden poles, connected sagging cable spans and hangar service wires.
- New Terrain - 384 x 384.

## Hill and road

The first climb is visible through the hangar entrance, then the road curves around the hillside into the village. Road length is 511.5m: approximately 63.9 seconds at an assumed constant 8m/s (28.8km/h). Actual vehicle time depends on speed and acceleration; no timed driving test was performed.

The sampled terrain grade is about 7.83% maximum. The village fence has an opening where the road enters. Farmyard and hangar floor remain level.

## Surface quality

Map-owned assets in Assets/NewMap include detailed oak and packed-earth albedo textures with subtle derived normal maps, fine canvas, world-space mapping to avoid stretching on scaled pieces, trilinear mipmaps and 16x anisotropic texture filtering. Source imported materials and textures remain untouched. Existing prop/vegetation material copies use filtered local textures; large PSD/TIF copies are flattened to PNG at their existing runtime resolution.

The generated albedo images are 1254 x 1254 pixels, not 2048. Generation provenance and prompts are in output/NewMap/Revision2/TextureNotes.md. TAA is enabled on the current player camera and review cameras. Grading reduces clipped highlights and oversaturation while retaining a warm farm palette.

## Preserve or restore

Original MAP remains inactive. Root Player and Graphics are active following the user's adjustments after the first pass; there is no longer a copied Player or separate sun under New_Map. Do not blindly restore the previous hierarchy instructions.

To switch environments, disable New_Map and enable MAP, then position the root Player appropriately for that map. Disabling New_Map also disables the new grading override.

A full pre-revision scene copy is at Temp/NewMap/Gameplay.before-hill-revision.unity; the pre-first-pass copy is Gameplay.before-new-map.unity in the same directory. These local Temp copies are not durable version-controlled backups.

## Validation and limits

Current evidence and previews are in output/NewMap/Revision2. The audit checks missing scripts, meshes, materials/shaders, terrain collider, enabled cameras, preserved player pose, actual road slope and a 3.3m-wide route clearance. Rerun its read-only entry:
unity command script run --file output/NewMap/Authoring/SurfaceQualityAndAudit.cs --entry SurfaceQualityAndAudit.Audit

Other authoring scripts record one-off operations and are not an idempotent rebuild tool; do not rerun them over an edited scene. Earlier output/NewMap validation and images describe the first pass.

The environment uses the existing low-poly assets, so model silhouettes and asset style still differ from the painted references. Grain, truck cargo and weighing station remain visual set dressing. No new HUD, grain transfer, vehicle simulation, economy, standalone build or performance benchmark was added.

## Denser hillside vegetation

A follow-up vegetation pass adds clustered beech trees on the upper slopes and behind the village, with thicker meadow grass along the road shoulders and across hillside clearings. It is grouped under New_Map/05_Landscape/Dense meadow and hill groves, so coverage is easy to toggle or edit. The farmyard, village buildings, crop field, road corridor and central hangar-to-village sightline are excluded.

Grass is combined into spatial cells with two mesh LODs; no grass colliders are added. Existing grass materials, wind shader and tree prefabs are reused. See output/NewMap/Vegetation for current counts, previews and validation. The one-off authoring history is DenseHillsideVegetation.cs; do not rerun over the existing vegetation group.

Final vegetation counts: 150 added trees and 19,297 grass patches in 262 cells. Hillside Review is an additional disabled camera under 07_Reference_Views. The completed recovery/batched authoring history is RecoverDenseVegetation.cs. The scene is saved; temporary capture assets were removed after copying previews to output/NewMap/Vegetation.

## Original farm style revision

The outdoor terrain now uses six map-owned copies of the original farm terrain layers, retaining their original textures. A 1024-resolution blend combines grass, dirt, stony dirt and earth with irregular road shoulders. This supersedes the generated packed-earth terrain appearance; the interior floor and timber still use the earlier materials. Terrain heights and the uphill road alignment are unchanged.

The hangar exterior uses the original red TFP_Barn_04A and two extensions around the furnished modular interior. Map-owned mesh copies fit the existing hall, including separate fitted collision meshes and open entrance doors. Native road meshes follow the bends and terrain, with spaced earthy ruts and small rocks along the shoulders. Another 318 original grass and wildflower prefab patches mix into the dense meadow.

Twenty TFP_Wooden_Electricity_Pole_01A poles follow the road, connected by 19 adapted native cable spans. Three service wires connect the first pole to the hangar. Minimum sampled cable clearance above terrain is 5.24m. Imported source assets remain unchanged.

Current previews and audits are in output/NewMap/OriginalStyle. The final read-only audit found zero missing scripts, meshes or unsupported/missing materials, zero obstructions in the sampled 3.3m road corridor, and a clear capsule path through the hangar doorway from z=4 to z=21. This revision was visually inspected in rendered camera captures; no new Play Mode traversal or performance benchmark was run. Earlier walk results apply to the earlier layout.

Authoring history: OriginalFarmStyle.cs, OriginalRoadsidePlanting.cs and FinishOriginalStyle.cs under output/NewMap/Authoring. These are one-off operations, not safe rebuild commands. OriginalStyleValidation.Audit is read-only. Original MAP remains inactive and all new environment objects remain inside New_Map; the user's root Player, Graphics and current lighting/quality adjustments are preserved.

## Road and field simplification
The latest pass keeps six separated rut sections instead of 30, with a lighter earth tint. Outdoor dirt blends 60% of the previous packed-earth layer with 30% original dirt, 8% stony dirt and 2% soil. The crop field now has an irregular rounded outline, feathered grass transition, curved fencing and crop patches repositioned within its boundary. New verge grass and fence pieces within 8m of the route are disabled. Terrain elevations, route length, red barn and power line are unchanged. Reference audit and 3.3m route clearance pass; evidence is in output/NewMap/RoadRefinement. SoftenRoadAndFarm.cs is one-off authoring history, not a rerunnable rebuild.

## Tall glazed hangar
The latest hangar is grouped under 01_Modular_Hangar/Tall timber hall with glazing. Its 20 x 30m footprint is preserved, wall tops raised from 7m to 12m, ridge from 9.45m to 16m, and entrance clearance is 10m wide by 9m tall. Six glazed high windows and four timber-framed skylights have real openings through the wall/roof panels. Grey interior cladding contrasts with exposed oak framing. Red exterior panels and native red side extensions preserve the exterior colour. Prior low interior architecture and the old red central shell are disabled for restoration. Player, truck, shelves and grain remain at their previous scale and positions. Pendant fixtures were raised to suit the taller interior. One-off authoring: TallGlazedHangar.cs; current evidence in output/NewMap/TallHangar.

## Hangar surface detail
The tall hall now uses generated weathered grey plank albedo and a derived normal map, stored in Assets/NewMap/TallHangar at native 1254 x 1254 resolution with mipmaps and 16x anisotropic filtering. 406 surface meshes have physical-scale UVs; timber grain follows each beam length. Grey walls, roof lining and red exterior have textured materials, and 72 small iron joint fasteners were added. Original material assets, lighting, glazing and layout are preserved. One-off authoring: HangarSurfaceDetail.cs. Current previews and successful reference/route audit: output/NewMap/HangarMaterials.

## Pendant lamps and interior props
Reference pendant lamps under 01_Modular_Hangar contain three spun bell shades, socket caps, suspension cables and luminous undersides using a saved 256px warm emission texture. Existing actual pendant lights are retained inside the fixtures; prior simple shades/cables are disabled. Wall side farm storage and ladders under 02_Hangar_Props adds 14 original prefab instances: two ladders, pallet stack, sack, broom, buckets, tools, barrel, stairs and crate. Rear steps at (-3.7,0.04,-9.95), yaw zero, meet the 1.84m-high storage landing behind them. Source assets unchanged. Reference/route and entrance capsule checks pass. Evidence in output/NewMap/LampsAndProps; one-off authoring HangarLampsAndProps.cs, with final stair placement corrected after visual inspection.

## Uneven dirt road surface
Latest terrain pass adds gentle longitudinal undulations, a shallow crown, and shallow wheel depressions across the road corridor. Maximum elevation change is 0.352m; sampled longitudinal grade is now up to 14.95% at roughly 0.4m spacing, superseding the previous 7.83% smooth-road grade. Farmyard and village approaches fade to the original surface. All 45 rut meshes were refitted, 64 shoulder/vegetation props regrounded, and route marker/CSV heights updated. Dense grass begins outside the main changed corridor. The 3.3m clearance audit passes, but vehicle dynamics have not been playtested. Original terrain asset retained at Assets/NewMap/Terrain/Before road surface undulations.asset; original route CSV in output/NewMap/RoadBumps/centerline-before.csv. One-off authoring UnevenDirtRoad.cs must not be rerun. Preview and checks: output/NewMap/RoadBumps. Disabled Road Surface Review camera is available under 07_Reference_Views.
