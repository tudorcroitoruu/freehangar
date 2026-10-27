# Buried finds

Gameplay contains fourteen discoveries under the grain, grouped at `New_Map/15_Buried_Finds`:

1. Copper key beneath the starter pile.
2. Mystery chest beneath Hangar 2 grain; the copper key opens it for $500.
3. Iron key beneath Hangar 3 grain.
4. Mystery chest beneath Hangar 4 grain; the iron key opens it for $900.
5. Free upgrade voucher beneath Hangar 2 grain. It pays for the next wheelbarrow upgrade instead of taking cash.
6. Infinite stamina bottle beneath Hangar 5 grain. It grants 60 minutes of real-world unlimited stamina, including shovel work, bag carrying, and vehicle boost.
7. Starter pile cash cache ($120), around halfway down.
8. Starter pile stamina bottle (15 minutes), above the halfway point.
9. Hangar 2 cash cache ($180), below the halfway point.
10. Hangar 3 free upgrade voucher, around halfway down.
11. Hangar 3 cash cache ($250), below the halfway point.
12. Hangar 4 cash cache ($350), above the halfway point.
13. Hangar 4 stamina bottle (20 minutes), around halfway down.
14. Hangar 5 cash cache ($400), around halfway down.

Finds are scattered at different horizontal positions and burial depths, roughly 38–63% of each pile's initial height. Their locations are fixed so a saved discovery stays consistent across sessions. The objects' colliders and visuals stay disabled until the grain surface drops below their tops. Finds placed above the floor settle onto the cleared grain surface when exposed. A closed hangar temporarily disables its grain object; this does not reveal a find. Once uncovered, aim at a find and press E. Chest prompts name the required key. Both chest lids fit their bases and swing open for 1.1 seconds before the claimed chest disappears. Claimed finds are stored in `PlayerData.foundSecrets`; rewards and claimed flags save together. Gameplay launched directly in the Editor loads a profile when no GlobalManager is present.

Wheelbarrow upgrades are the first purchasable upgrades. Press U while pushing a wheelbarrow to buy its one-time capacity increase. The voucher is consumed first; without one, prices are $250, $500, and $1,000 for the small, medium, and large wheelbarrows. Upgrade levels, vouchers, and the stamina expiry are saved in PlayerData. Other vehicle upgrade keys remain a future feature because those vehicles do not yet have purchase flows.
