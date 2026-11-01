# Achievements and Steam setup

All eleven achievements are tracked locally in `PlayerData.achievementProgress`, independent of the current tutorial goal. Successful unlocks print a green `[Achievement DONE] [index] Name (ACH_index)` Console message once. Open **Tools > Freehangar > Achievements** for live counters and green DONE rows. **Tools > Freehangar > Validate achievements** runs the EditMode rule tests.

Stable indexes / Steam API names:

| Index | API name | Name | Requirement |
|---|---|---|---|
| 0 | ACH_0 | Grain Mountain | Collect 5,000 kg through actual shovel/vacuum collection. |
| 1 | ACH_1 | Packing Professional | Fill and close 50 distinct bags. |
| 2 | ACH_2 | Clean Sweep | Clear 2 distinct hangars. |
| 3 | ACH_3 | Master Cleaner | Clear half the authored hangars (rounded up). |
| 4 | ACH_4 | Key Collector | Collect half the authored vehicle keys (rounded up). |
| 5 | ACH_5 | Buried Treasure | Open half the authored chests (rounded up); cash caches are excluded. |
| 6 | ACH_6 | Crow Control | Scare away 25 crows with successful broom hits. |
| 7 | ACH_7 | Rat Patrol | Scare away 50 rats with successful broom hits. |
| 8 | ACH_8 | Heavy Lifter | Deliver 13 distinct full big bags by forklift into a selling/weigh zone. |
| 9 | ACH_9 | Grain Tycoon | Receive $100,000 total from successful grain sales. |
| 10 | ACH_10 | Electric | Find the electrical room switch and turn on its lights. |

## Enable Steam later

Steamworks.NET 2025.165.0 / SDK 1.65 is already installed. No second SDK or SteamManager is added. Steam syncing reuses the existing SteamManager callback loop.

1. In Steamworks App Admin, define and publish achievement API names **ACH_0** through **ACH_10** for the real game App ID. Configure their displayed names, descriptions and icons there. `achievement-definitions.csv` is a configuration reference; follow Steamworks' current import/export format if importing it.
2. Select `Assets/Resources/SteamAchievementSettings.asset` and set **App Id** to the real game App ID. Zero and test App ID 480 never submit these achievements. The API-name array may be adjusted if existing backend names differ; preserve index meaning.
3. For local development, replace the `480` in root `steam_appid.txt` with the real App ID and run Steam under an account licensed for that game. Remove the development `steam_appid.txt` from released Steam depots. The existing SteamManager also reads the configured App ID for RestartAppIfNecessary.
4. Editor Steam syncing is off by default. Enable **Sync In Editor** only when intentional live account unlocks are desired. Local debug counters and green completion messages work with this disabled.
5. Verify the overlay in an actual Steam-launched build. A matching running App ID is required; a mismatch is reported and no achievement is submitted.

SDK 1.65 preloads local stats/achievements before launch and removes the old `RequestCurrentStats` binding. The bridge reads `GetAchievement`, calls `SetAchievement` for completed local indexes, and uploads with `StoreStats`. Store callbacks acknowledge only the submitted batch; offline/failed uploads remain eligible for retry from saved local unlocks. Unpublished/missing API names are warned once and retried. The integration does not create/publish achievements in Steamworks automatically.

Existing saves gain empty counters without inventing historical grain totals, broom hits or sales. Already collected discovery IDs are recognized when the Gameplay catalog is scanned. Collection/all-hangar achievements require a nonempty catalog; inactive discoveries and piles are included. Completion is permanent, and depositing/re-collecting grain never removes an achievement. Counters are checkpointed every two seconds; unlocks and sales are saved immediately, and pending counters are flushed on pause/quit.

Valve reference: [ISteamUserStats](https://partner.steamgames.com/doc/api/ISteamUserStats).
