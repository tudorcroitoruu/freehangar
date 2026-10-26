# Terrain missing after checkout on another PC

## Recovery on 2026-10-26

Rechecked at base commit `10f447f4` on the PC with the working terrain.
The previous prevention change was present, but the nine damaged blobs below
were still in Git. The local files have their full declared sizes and load as
TerrainData. Git's cached file status reported them clean even though their raw
bytes differed from HEAD; changing attributes alone had not re-staged them.

Re-staged those nine existing local binaries with `git add --renormalize` under
the corrected binary attributes. No terrain was regenerated and no GUIDs,
scenes, layers, textures, or gameplay code were changed. The active terrain is
now staged at 11,737,792 bytes instead of 11,737,715 bytes. Commit and push the
staged repair so collaborators receive the intact files.

Validation:

- The committed HEAD fails the serialized-header size check before import.
- All nine local files load as TerrainData. NewMap has a 513x513 heightmap,
  size 384x80x384, seven layers with no missing layer/diffuse references, and
  the active TerrainCollider references the same TerrainData.
- `git checkout-index` exports of all nine staged files match their local
  originals byte for byte (SHA-256), including with `core.autocrlf=true`.
- All nine staged blobs pass fresh Unity imports with new GUIDs; resolved
  dependencies and their metas are tracked. The final run took 11.2 seconds,
  with no new Console warnings or errors. Results are saved in
  `output/NewMap/TerrainRecovery/validation.txt`. This tests new terrain
  imports in this project, not a full project clone on another machine.
- `output/NewMap/Authoring/ValidateTerrainGit.cs` provides repeatable checks:
  `Local` inspects the working assets and scene binding; `Index` imports Git
  index blobs with fresh temporary GUIDs and checks dependencies; `Head` runs
  the same check against the latest commit. Temporary imported assets are
  removed in a `finally` block.

Run the staged-data check in the open Editor in Edit Mode:

```powershell
unity command script run --file output/NewMap/Authoring/ValidateTerrainGit.cs --entry ValidateTerrainGit.Index --timeout 300
```

The older imported `Oasis Dig 2.asset` has two missing layer/diffuse references
in the local baseline. This is separate from its binary corruption and does
not affect NewMap's seven valid layers. A second-machine gameplay run remains
the final collaborator-side check after committing, pushing, and pulling.

## Original investigation (historical)

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
