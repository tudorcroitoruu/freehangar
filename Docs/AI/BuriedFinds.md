# Buried finds

Gameplay contains four discoveries under the grain, grouped at `New_Map/15_Buried_Finds`:

1. Copper key beneath the starter pile.
2. Mystery chest beneath Hangar 2 grain; the copper key opens it for $500.
3. Iron key beneath Hangar 3 grain.
4. Mystery chest beneath Hangar 4 grain; the iron key opens it for $900.

The objects' colliders and visuals stay disabled until the grain surface drops below their tops. A closed hangar temporarily disables its grain object; this does not reveal a find. Once uncovered, aim at a find and press E. Chest prompts name the required key. Both chest lids fit their bases and swing open for 1.1 seconds before the claimed chest disappears. Claimed keys and chests are stored in `PlayerData.foundSecrets`; chest cash and its claimed flag save together. Gameplay launched directly in the Editor loads a profile when no GlobalManager is present.

Vehicle upgrade keys and perk vouchers are not wired yet because there is no corresponding upgrade purchase flow for them to unlock.
