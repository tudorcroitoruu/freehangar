# Gameplay — New_Map

Updated 2026-10-24 after the hill and texture revision.

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
