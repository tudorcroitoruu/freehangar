# Material pipeline converter

2026-11-11 URP held-tool cameras: `HeldToolCamera` now uses a Base/Overlay camera
stack under URP, saved in `Assets/Resources/Player.prefab`. Carrying clears tool
depth; scooping temporarily draws HeldTools through the world camera so the pile
occludes the inserted shovel. Projection and renderer are synchronized; the HDRP
custom pass is disabled only under URP. Loaded instances were configured without
saving unrelated scene edits. Compilation, rendered overlay/scooping occlusion,
return-to-carry and disable/reenable checks passed. The actual player-camera
capture was visually inspected. Evidence: `output/Rendering/URPHeldCamera`.

2026-11-11 appearance repair supersedes the earlier compatibility-only result:
URP pipeline tags did not prove shader compilation or correct texture bindings.
The Toon shaders' hand-added HDRP subshaders polluted URP compilation with HDRP
LightData definitions; removed those extensions, preserving the native URP
grass, vegetation, toon and water implementations. Generic Lit conversion had
discarded the custom tree atlas and procedural grain properties. Restored 12
grain materials and two tree cutout materials from the pre-conversion backup,
using dedicated URP grain/tree shaders. Corrected the vegetation atlas alpha
cutoff from zero to 0.5 so transparent leaf cards are clipped at every LOD.
The converter now preserves custom grain/tree properties when switching pipeline.

Validation: all six repaired shaders compile without warnings/errors; all five
loaded piles use the URP grain shader; 16 converter checks pass, including crop
weights and tree atlas/cutout preservation. Rendered and visually inspected grain,
trees, all four LODs and the surrounding environment. Evidence is in
`output/Rendering/URPAppearance`. These are Editor checks, not a player build.
Separate legacy Polygonmaker HDRP shader errors and the existing punctual-light
shadow atlas reduction warning remain. Backups are under
`MaterialConversionBackups/URP-Appearance-20261111-103029`,
`URP-CustomMaterialRestore-20261111-103343`, and `URP-LeafCutoff`.

Open **Tools > Rendering > Material Pipeline Converter**. Choose URP or HDRP,
optionally select assets/folders in the Project window, then **Scan / Preview**.
Review the list and click **Convert**. Closing the window or pressing Stop ends
the batch after the current material; completed materials remain converted.

The tool converts standalone `.mat` assets under Assets in place, preserving
their GUIDs and renderer references. It never changes pipeline settings,
scenes, shader source, or packages. Both render pipeline packages must remain
installed because this project references their material validation APIs.

Supported mappings include Lit/Unlit base texture/tint, texture transforms,
tangent-space normals, metallic/specular workflow, smoothness, occlusion,
emission, transparency, alpha clipping, and double-sided rendering. Mask maps
are baked as linear PNGs under `Assets/MaterialConversionGenerated` (maximum
4096 pixels per dimension). HDRP remaps are baked into channel values.

Custom/built-in shaders are approximated using common property names when the
checkbox is enabled. This does not port shader code: wind, particles' vertex
colors/flipbooks, water, layered materials, custom grain rendering, displacement,
detail maps, subsurface scattering, special UV modes, independent custom map UVs,
and custom passes need manual work. URP Unlit has no separate emission map.
Render queues are normalized to opaque/cutout/transparent. Already-target shaders
are retained. Missing shaders, UI/TMP, outlines, sky, terrain, decals, hidden
shaders, material variants, read-only assets and embedded materials are skipped.
Extract embedded materials through the model importer before converting them.

For the current URP project, **Tools > Rendering > Repair Remaining URP Materials
and Terrain** also handles terrain templates referenced by scenes/prefabs and
embedded model materials (converted copies are remapped through the importer).
It aligns the default pipeline with the selected URP quality pipeline and keeps
backups under `MaterialConversionBackups/URP-Coverage-<timestamp>`. Terrain uses
`Universal Render Pipeline/Terrain/Lit`; terrain data, layers, paint, and scene
references remain intact. Shader Graph preview subassets are not scene materials
and are excluded. **Audit Remaining URP Materials** writes
`Docs/AI/UrpCoverageAudit.txt`, including loaded scene renderer mismatches.

2026-11-11 follow-up: repaired the shared farm HDRP terrain template and the Oasis
template previously using ordinary URP/Lit. The post-repair audit found zero
remaining asset pipeline mismatches and no loaded renderer mismatches. Existing
model materials already resolved to compatible URP shaders. This is compatibility
validation; custom shader appearance still requires visual review.

Each run saves the original material and its meta file under
`MaterialConversionBackups/<timestamp>/Assets/...`, with a `report.tsv` of
results. Use Edit > Undo immediately to undo the material changes, then save
the project. Generated textures remain after Undo. For restoration after an
Editor restart, close Unity and copy the backed-up material files over the same
relative paths in the project; preserve their original meta files. Backups are
outside Assets and must be retained until the conversion has been reviewed.
Conversion is lossy; use the backup for exact restoration instead of converting
back. Failed conversions retain the original material; unused generated textures
can remain after failures or cancellation.

**Tools > Rendering > Validate Material Pipeline Converter** runs isolated
checks with temporary materials and textures, then removes its test assets.
Visual review under the chosen active pipeline is still required.
