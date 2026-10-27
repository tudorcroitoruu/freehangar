# Buried finds

Gameplay contains sixteen discoveries under the grain, grouped at `New_Map/15_Buried_Finds`:

1. Copper key beneath the starter pile.
2. Mystery chest beneath Hangar 2 grain; the copper key opens it for $500.
3. Iron key beneath Hangar 3 grain.
4. Mystery chest beneath Hangar 4 grain; the iron key opens it for $900.
5. Free upgrade voucher beneath Hangar 2 grain. It pays for the next wheelbarrow upgrade instead of taking cash.
6. Infinite stamina bottle beneath Hangar 5 grain. It grants 5 minutes of real-world unlimited stamina, including shovel work, bag carrying, and vehicle boost.
7. Starter pile cash cache ($120), around halfway down.
8. Starter pile stamina bottle (5 minutes), above the halfway point.
9. Hangar 2 cash cache ($180), below the halfway point.
10. Hangar 3 free upgrade voucher, around halfway down.
11. Hangar 3 cash cache ($250), below the halfway point.
12. Hangar 4 cash cache ($350), above the halfway point.
13. Hangar 4 stamina bottle (5 minutes), around halfway down.
14. Hangar 5 cash cache ($400), around halfway down.
15. Hammer in the starter pile, around 60% of the initial grain height.
16. Sledgehammer in Hangar 2, around 58% of the initial grain height.

Finds are scattered at different horizontal positions and burial depths, roughly 38–63% of each pile's initial height. Their locations are fixed so a saved discovery stays consistent across sessions. The objects' colliders and visuals stay disabled until the grain surface drops below their tops. Finds placed above the floor settle onto the cleared grain surface when exposed. A closed hangar temporarily disables its grain object; this does not reveal a find. Once uncovered, aim at a find and press E. Chest prompts name the required key. Both chest lids fit their bases and swing open for 1.1 seconds before the claimed chest disappears. Claimed finds are stored in `PlayerData.foundSecrets`; rewards and claimed flags save together. Gameplay launched directly in the Editor loads a profile when no GlobalManager is present.

Wheelbarrow upgrades are the first purchasable upgrades. Press U while pushing a wheelbarrow to buy its one-time capacity increase. The voucher is consumed first; without one, prices are $250, $500, and $1,000 for the small, medium, and large wheelbarrows. Upgrade levels, vouchers, and the stamina expiry are saved in PlayerData. Other vehicle upgrade keys remain a future feature because those vehicles do not yet have purchase flows.

## Buried barriers and tools

Four wall spans are under the grain in `New_Map/16_Buried_Barriers`: wooden walls in Hangars 2 and 4, and metal walls in Hangars 3 and 5. The spans contain 60 individual, roughly square 2.8 m panels (13 each in Hangars 2 and 3; 17 each in Hangars 4 and 5). Every square is one GameObject with one combined mesh, one renderer, and one collider; the wood and metal meshes are shared assets under `Assets/NewMap/FiveHangars/BarrierPanels`. Panels are 0.75 m thick, with visible front, back, and edge faces. Each panel has its own health and save ID, so destroying one removes the whole square and opens a local gap while its neighbors remain. The four original barrier IDs are retained on one panel of each span for existing saves.

A panel has no collision until grain drops below its top at any exposed face or end. Once uncovered it blocks the player and shovel line of sight, and can be struck from either side or either end. Wooden panels need three hits with either tool; metal panels need five sledgehammer hits. Destroyed panel IDs are saved in `PlayerData.foundSecrets` and stay gone after reload.

Collect the hammer or sledgehammer with E. Press 1 for the shovel, 2 for the hammer, or 3 for the sledgehammer; left click while aiming at an exposed wall to swing. The view shows the equipped tool and animates each strike. The player cannot switch away from a loaded or busy shovel, or attack while carrying a bag or pushing a wheelbarrow. Uncollected tools cannot be equipped. Barrier prompts show the required tool and remaining hits.
