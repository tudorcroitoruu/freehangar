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

## Variable road width
Latest road paint varies the clear dirt core from approximately 5.2 to 10.0m, with asymmetric edges and 1.5m feathered shoulders per side. Narrower stretches alternate with bend/passing-area flares. 271 verge grass patches were added and 26 rocks shifted outward. Existing terrain bumps, road route and terrain heights remain unchanged. Backup terrain: Assets/NewMap/Terrain/Before variable road width.asset. Audit finds zero 3.3m corridor obstructions or missing references. Evidence: output/NewMap/VariableRoad; disabled Road Width Review camera under 07_Reference_Views. VariableRoadWidth.cs is one-off authoring history.

2026-10-26 hangar mesh consolidation: Tall timber hall with glazing in Gameplay now contains 28 section meshes instead of 587 individual renderers: four walls, roof, timber structure, two independent door leaves, six windows and four skylights (glass and opaque frames remain separate to preserve shadow/render behavior). Material submeshes and all 11,940 triangles are preserved; world bounds match within 2 mm. The 375 box collision shapes are baked into 18 static section MeshColliders. Geometry assets: Assets/NewMap/CombinedHangar. Authoring: output/NewMap/Authoring/CombineHangar.cs; evidence: output/NewMap/CombinedHangar/validation.txt. Original scene snapshot: Temp/HangarCombine/Gameplay.before-combine.unity. Lighting, props, floor and inactive legacy hangar pieces remain in place. Live script compilation and reference audit passed. Two UnityEditor.SceneViewMotion shortcut NullReferenceExceptions appeared while checking; no compilation errors were reported. No player build or performance benchmark run.


## Five-hangar farm layout (2026-10-26)

Gameplay now follows the annotated plan: the existing starter hall is at the right of the top view, medium halls 2 and 3 flank it diagonally, and larger halls 4 and 5 sit farther back above/below the central yard. The S area contains a small caretaker cottage and an open-front farm supply shop with bag/crate/pallet dressing. This is a map and scene-authoring pass only; purchasing, money, unlocking, and completion tracking are intentionally not implemented.

- Hangar 1 remains 20 x 30m, open, with its existing small interactive grain pile and props.
- Hangars 2/3 are 26 x 42m with closed, braced wooden sliding doors and almost-full grain banks, leaving a larger approach inside the threshold.
- Hangars 4/5 are 33 x 55.5m with closed galvanized metal doors, corrugated exterior ribs and grain banks approaching the entrances. All four added halls have distinct exterior/roof finishes and numbered locked signs. Left/right leaves are independent transforms for future progression animation.
- The new halls share the original combined structure mesh assets. Their grain uses the existing deformable GrainPile/CornSurfaceKernels components. Approximate initial mass is 25t in the starter pile, 2,165t per medium hall and 5,517t per large hall; these are geometric prototype quantities, not economy balancing.
- Four surrounding farm plots add a timber/hay paddock, whitewashed market garden, orchard and village machinery court. New fence gates preserve the main road. Hangar 3's yard approach curves around the retained service pole. Three existing silos were moved to (-20,-28,-36 X; Z=70) to free Hangar 4's approach.
- Expanded Farm Terrain.asset is a separate copy bound to both Terrain and TerrainCollider. New sites are flattened; the old road height route remains intact. Raised perimeter hills, a textured distant hill ring, split-rail fences/trees and four continuous tall collision barriers surround the map. The source terrain asset remains unchanged.
- Static consolidation processed 2,928 mesh renderers into 790 section/LOD renderers. Existing LOD thresholds and references are retained for processed buildings. Fence cells preserve spatial culling. Vehicles, interactive grain and future moving doors remain separate. Inactive original hangar construction is archived as Legacy hangar construction.prefab outside the live hierarchy. This is a renderer/hierarchy reduction, not a frame-time or GPU batch benchmark.

Scene organization: New_Map/11_Five_Hangar_Farm and 12_Farmyards_and_boundaries. Assets: Assets/NewMap/FiveHangars. One-off authoring scripts: output/NewMap/Authoring/ExpandFarm.cs, FarmFinishing.cs, CompactFarmStatics.cs and finishing scripts; do not rerun over the finished scene. Pre-change scene snapshot: Temp/FiveHangars/Gameplay.before-expansion.unity. Revert the changed Gameplay scene and added asset folder together when rolling back.

Validation: saved scene reload; zero missing scripts, active meshes/materials or unsupported active shaders; valid terrain collider binding; 100/100 perimeter ray samples; clear 3.3m main road corridor; all four yard approaches clear. Nineteen Play Mode checks cover CharacterController entry/door blocking, all four map edges, initialized kernel surfaces and mass-conserving scoops from every pile. The final reload/Play Mode cycle produced no new Console errors. Compilation passed with seven existing unused-variable/field warnings in unrelated global scripts. Evidence and previews: output/NewMap/FiveHangars. No standalone build or performance certification.

Reload testing exposed CornSurfaceKernels.Build reading an uninitialized GrainPile height field when its OnEnable ran first. It now explicitly initializes its pile before sampling/caching the generated surface and skips unsupported placements safely. This fixes the component-order dependency for added piles as well as existing instances.


## Hill groves, finished supply shop and roadside farmyards (2026-10-26)

Added 1,002 low-poly beech trees in irregular groves: 294 on the terrain's perimeter slopes and 708 on the outer hill backdrop. Three existing tree variants use varied sizes/rotations. Geometry is grounded on the actual terrain/backdrop triangles, combined into 227 spatial cells with three LODs (681 renderer components), and adds no per-tree hierarchy or collision. Source models/materials are reused. Authoring: PlantHillTrees.cs; assets: FiveHangars/HillTrees; evidence: output/NewMap/FiveHangars/hill-trees.txt and HillTrees.png. Static scene/reference/road checks pass; no FPS or GPU benchmark was run.

The S supply shop now has weatherboard siding, physical-scale UVs, timber floor/joinery, a textured green metal roof, closed triangular gables and a front canopy. The cottage and displayed stock use an HDRP atlas material with the existing farm texture. The shop counter is lowered to about 1.15m so the player can see inside. Shelves hold sacks, seed bags, crates, shovels, hammers, sledgehammer, rake, handsaws, hand rake, broom and bucket. Fixtures and stock are combined into three display sections with two LODs each. Decorative tool collision was removed where it narrowed the customer aisle; the building, counter and shelving retain solid collision. Shelf signage identifies bags/boxes and farm tools. Buying remains intentionally deferred. Assets: FiveHangars/FinishedShop; one-off authoring: FinishSupplyShop.cs; previews/summary/accessibility checks: output/NewMap/FinishedShop. Six Play Mode checks pass for both entry aisles, reaching the rear floor, solid counter collision and eye-level visibility.

Roadside fences now follow both shoulders using 262 split-rail posts, combined into 22 spatial render/collision cells, with access openings at the main yard, village and farm plots. Sixteen hay/pallet/crate props form four roadside supply bays. The obsolete square dirt stamp left at the market garden's former location near the village was removed: terrain blending now follows the existing curved road with feathered grass shoulders. The centerline and terrain heights are preserved. Assets: FiveHangars/RoadsideFarmyards; authoring: RoadsideFarmyards.cs; previews and report: output/NewMap/RoadsideFarmyards. The 3.3m main road corridor and all four hangar approaches remain clear. Snapshot before this pass: Temp/FiveHangars/Gameplay.before-roadside-farms.unity.

## Yard-road transition and shop detail corrections (2026-10-26)

The start of the road now blends smoothly into the farmyard. The shop-site flattening had left a sharp cross-slope across the road. A cubic ramp now connects route distances 18-100m, with a shallow crown and feathered shoulders out to 11m. Sampled maximum centreline grade on this segment is 7.33%; the largest localized terrain correction is 2.72m. Fifty-eight generated road/fence/grass/wire detail meshes were copied and refitted; 181 nearby props/route markers were regrounded. The painted yard edge narrows gradually into the road. Route CSV heights were refreshed. Assets are under FiveHangars/YardRoadTransition; the prior terrain binary, CSV and scene snapshot are in Temp/FiveHangars/*before-yard*.

Shop display tools are now 11 separate editable GameObjects, each with its own saved mesh under FiveHangars/ShopCorrections. This supersedes the earlier combined tool display; bags and building fixtures remain combined. Counter hammer and handsaw lie flat, with their lowest vertices at local Y=1.151m; measured vertical thickness is 7.13cm and 3.45cm respectively. Other wall/shelf tools retain upright display poses. A solid weatherboard fascia fills the upper shopfront from canopy to eaves, with a closed front gable, structural headers, side supports and roof framing behind the sign. The facade support geometry is combined as one mesh.

Validation: five Edit Mode geometry/reference checks pass; the temporary CharacterController walked the 82m ramp in 164 half-metre steps with no horizontal obstruction error; all six shop-access/counter checks pass. Existing main road, hangar approach, active mesh/material and terrain collider audits pass. Final Play Mode validation produced no new errors after cursor 29. A temporary preview-camera setup logged a null CopyFrom error before validation; that camera was removed and captures were redone using the Scene View camera. Previews/reports: output/NewMap/ShopCorrections. One-off authoring: ShopDetailCorrections.cs and BlendYardRoad.cs; validation: ValidateShopCorrections.cs. User lighting/quality/sky changes remain outside this edit.


## Scene-wide realtime light visibility (2026-10-26)

Gameplay has an enabled root object named Scene Light Visibility, with SceneLightVisibilityController explicitly bound to Player/Main Camera (which is untagged). It runs only in Play Mode and controls realtime, non-directional Light components in its own scene. Directional, mixed, baked and explicitly excluded lights are left alone. All ten current local realtime lights are registered. The bound camera's orientation, vertical FOV and aspect determine a view cone around each light's origin, with 10 degrees of view padding and a further 5-degree exit margin to prevent rapid switching at the edge. Perspective and orthographic cameras are supported. No occlusion raycasts are performed.

Shadow distance defaults to 18m from the camera; a shadow already on remains allowed until 20m. Lights outside the view are disabled, and visible lights farther away retain illumination with their shadows disabled. Authored Hard/Soft/None shadow types and enabled states are cached and restored when the controller is disabled, a light leaves management, or the camera becomes unavailable. Inactive or authored-disabled lights are not forced on. HDRP's shadow API uses the same Light.shadows property, so no global HDRP asset/settings changes are needed.

View evaluations run every 0.05s in LateUpdate, with cached light state and no per-update discovery/allocations. New/removed lights are discovered every two seconds and after scene-load events; RefreshLights and the Inspector rescan button can update immediately. The registry is scene-scoped, so additive scenes may each have their own controller. A second enabled controller in the same scene disables itself to prevent competing writes. Runtime Inspector statistics show managed/enabled/shadowed counts. Disabling the component restores authored lighting; Edit Mode never culls lights.

Reuse in another scene: Tools > Lighting > Add Scene Light Visibility Controller. The setup command supports Undo, avoids duplicates and selects the controller. It prefers a world camera under an active PlayerController; otherwise assign View Camera explicitly or use a tagged MainCamera. Shadow distance, view margins, timing and exclusions are exposed in the Inspector. Gameplay switches should call controller.SetLightEnabled(light, enabled), particularly when the light is already culled: direct writes equal to the controller's last value cannot communicate a distinct authored change. Distinguishable external enabled/shadow changes are tracked automatically. The controller changes the Light component, not emissive fixture materials.

Files: Assets/_Scripts/Rendering/SceneLightVisibilityController.cs and Assets/Editor/SceneLightVisibilitySetup.cs. Scene snapshot before installation: Temp/Lighting/Gameplay.before-light-visibility.unity. Validation: Unity compilation passes; 37 isolated Play Mode checks pass, covering view/shadow hysteresis, HDRP properties, excluded lights, dynamic discovery, restoration, gameplay switching, lifecycle and duplicate ownership. Five hundred stable six-light evaluations allocate zero managed bytes (about 0.0025ms per evaluation in this Editor fixture). In the current Gameplay starting view, 4 of 10 local lights and 1 local-light shadow are enabled; an eight-heading probe ranges from 0 to 5 local lights, with the directional unchanged. All 11 scene lights retain authored enabled/shadow values after returning to Edit Mode. No new Console errors during the integration cycle after cursor 44. Reports and validation scripts: output/Lighting. No GPU/frame-rate or standalone-build claim.
