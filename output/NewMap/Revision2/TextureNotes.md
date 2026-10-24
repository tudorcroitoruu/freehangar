# Texture provenance — hill/surface revision

Created 2026-10-24 with the native image_gen tool. These are newly generated tile textures, not downloaded images or edits to the reference screenshots.

## Oak
Prompt intent: a flat seamless tile of warm weathered oak with fine vertical grain, restrained stylized farm-game realism, no plank divisions, no objects, no directional lighting or shadows.
Original output: C:/Users/TudorCroitoru/.codex/generated_images/01a16d88-e772-7a11-ba57-79c98124181c/exec-be11fd21-5860-442f-a62c-752ae6d7ce2f.png
Project file: Assets/NewMap/Textures/Oak_Albedo.png

## Packed earth
Prompt intent: a seamless pale ochre packed farm-road surface, fine dust and embedded tiny pebbles, low contrast, no grass, large rocks, dominant ruts or directional shadows.
Original output: C:/Users/TudorCroitoru/.codex/generated_images/01a16d88-e772-7a11-ba57-79c98124181c/exec-ef7ec145-4664-493b-b059-2d2f0c7f9df6.png
Project file: Assets/NewMap/Textures/PackedEarth_Albedo.png

Both delivered images are 1254 x 1254 pixels. Their importer maximum is 2048 with NPOT scaling disabled, so no detail is invented by upscaling. Albedos use mipmaps, trilinear filtering and 16x anisotropy. Normal variants are Unity height-to-normal conversions with a restrained amplitude.

FineCanvas.asset is a new 512 x 512 procedural fine weave made in Unity. The other NewMap texture PNGs are local filtered copies of the user's existing imported assets, flattened from authoring formats at their existing runtime resolution. The imported originals were not modified.
