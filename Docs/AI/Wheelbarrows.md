# Wheelbarrows

Gameplay has three usable wheelbarrows. The small barrow starts in the original hangar; the medium is inside Hangar 2 and the large is inside Hangar 4. The latter two are discoveries behind those hangars' existing doors.

Finish and carry a filled sack, aim at a wheelbarrow, and press E to load it. With empty hands, aim at the barrow and press E to push it; walking moves it in front of the player. Press E again to release it. Aim at a loaded sack and press E to take it, then press E to drop it in a clear spot or load it onto a vehicle using the existing cargo interaction.

Capacity is based on the closed bag's physical box volume, independent of its world rotation. The configured limits are 0.4, 0.8, and 1.6 m³, enough for approximately one, two, and four of the current 50 kg sacks. The prompt shows occupied and total volume. Loaded bags retain their grain contents and their colliders. Pushing is limited to walking speed and stops when the barrow is blocked or the player moves too far away.

The barrows currently use transform following rather than wheel physics; this is intended for easy on-foot transport. Their positions and loaded cargo are scene state only and are not yet serialized in SaveSystem.

While pushing, press U to buy one capacity upgrade for that barrow. The upgrade adds 0.4 m³ (approximately one 50 kg sack) and is saved. A hidden free upgrade voucher is used before cash; otherwise the small, medium, and large upgrades cost $250, $500, and $1,000.
