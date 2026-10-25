# Terrain missing after checkout on another PC

Investigated on 2026-10-25 at commit a4284c8e, Unity 6000.3.20f1.

## Evidence

Gameplay's enabled Terrain and TerrainCollider both reference GUID
`e3b2978158a33bf47a8b20d736b31df4`, which resolves to
`Assets/NewMap/Terrain/NewMapTerrain.asset`. The asset and its meta are tracked.
The local Unity Editor reports that this asset cannot be loaded.

The previous `.gitattributes` rule forced `*.asset text`. TerrainData is a
binary serialized file even when Unity's Asset Serialization mode is Force Text.
Git text normalization can remove CR bytes from binary CRLF sequences during
staging. A binary probe demonstrated that the old rule changed its Git object
hash; with the corrected attributes, filtered and raw hashes match.

The local terrain bytes match the committed bytes. The binary header declares
11,737,792 bytes, but the committed file contains 11,737,715 bytes (77 fewer).
Both terrain backups are also shorter than their declared sizes. This is
consistent with the destructive Git text conversion and explains why pulling
the same committed assets on another PC fails. A working Editor on the source
PC may have intact source bytes or a previously imported cached copy; verify
the source before relying on it.

## Prevention

`.gitattributes` now detects text versus binary for general `.asset` files and
explicitly treats terrain and lighting assets as binary. YAML assets retain
their existing merge handling. The LFS setup is unchanged.

This prevents further conversion; it cannot restore bytes already removed.
Do not recreate the terrain, change its GUID, or replace it with a flat terrain.

## Recovery on the PC with the working terrain

1. Before pulling, reimporting, or closing the working Editor, copy the entire
   `Assets/NewMap/Terrain` folder outside the repository. Preserve its `.meta`
   files. If the on-disk source is already corrupt but the terrain remains
   loaded, recover the loaded TerrainData through the Editor to a separate
   asset first; do not discard that Editor session or its Library cache.
2. Bring in the corrected `.gitattributes`. Verify that the original terrain
   file is intact and loads in Unity. Restore it from the external backup if
   necessary, keeping the existing `.meta` GUID.
3. Re-stage the intact binary assets under the corrected attributes. For the
   active terrain and its two authoring backups, run from the project root:

   ```powershell
   git add --renormalize -- Assets/NewMap/Terrain/*.asset
   git diff --cached --stat
   ```

   Include only recovered assets in the repair commit. A file appearing clean
   under the old attributes did not prove its committed bytes were intact.
4. Commit and push the recovered assets. On another PC, pull and reopen
   Gameplay. Verify that the TerrainData loads, the terrain renders, and the
   TerrainCollider works. A clean checkout must be checked before declaring
   the recovery complete.

## Other affected binary assets

The size audit found these additional mismatches. Recover original bytes from
the working PC or original asset package; do not guess the missing bytes.

| Asset | Declared bytes | Actual bytes |
| --- | ---: | ---: |
| `Assets/NewMap/Terrain/Before road surface undulations.asset` | 11,737,808 | 11,737,730 |
| `Assets/NewMap/Terrain/Before variable road width.asset` | 11,737,804 | 11,737,726 |
| `Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Oasis Dig 2.asset` | 35,259,168 | 35,259,144 |
| `Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Oasis Dig 3.asset` | 57,629,176 | 57,629,078 |
| `Assets/Imported/BK/PureNature_Oasis/Models/Terrains/Terrain_Oasis.asset` | 56,684,848 | 56,683,625 |
| `Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Gardens.asset` | 6,154,140 | 6,154,067 |
| `Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Hay_Fields.asset` | 1,552,444 | 1,552,423 |
| `Assets/Imported/Toon Series/Toon Farm Pack/Terrain/TFP_Terrain_Data_Ranch.asset` | 4,349,260 | 4,349,257 |

Nine of the 17 tracked binary `.asset` files had a header/file-size mismatch.
Matching size alone does not prove full integrity. No terrain bytes or scenes
were modified during this investigation; visual recovery remains unverified
until intact source data is restored.
