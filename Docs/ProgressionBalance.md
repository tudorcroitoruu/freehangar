# Progression balance: 3–5 hour first playthrough

This is an authored starting balance, not a measured completion time. A full fresh-save playtest is still needed. Travel routes, optional cleanup, pest management and container handling will determine the actual duration.

| Chapter | Target time | Main discoveries and purpose |
|---|---|---|
| Hangar 1 | 15–25 min | Early $75, stamina, copper chest key, axe around 68% of entrance-to-back stock, Hangar 2 key around 84%, optional final $75. Learn scooping, packing and selling; buy initial upgrades and the cash-only motoroller. |
| Hangar 2 | 45–65 min | Cash chest near entrance using the H1 key; trike key around 15%, broom 32%, lawnmower key 52%, voucher 59%, hammer 78%, next-door key 88%. Tools precede later barriers and pests. |
| Hangar 3 | 40–55 min | Mobility, iron chest key, pickup key around 36%, $500 cache, double-sale bottle, matching chest, next-stage iron key, next-door key 88%. Introduce heavier transport and rat management. |
| Hangar 4 | 45–65 min | Early iron chest, protection bottle, bigger-pickup key 27%, voucher 45%, stamina, $900 cache, final-pickup key 69%, optional copper chest, next-door key 89%. Prepare the transport needed for the final workload. |
| Hangar 5 | 45–70 min | $750 near front, iron key, mobility, double-sale bottle, stamina, $1,400 cache, optional iron chest and $1,500 finish cache. Keep discoveries through the last chapter; finish with the existing electrical-room switch sequence. |

Total target: 190–280 minutes, approximately 3.2–4.7 hours. These chapter estimates are design targets, not timings obtained from automated tests.

## Placement rules

44 discoveries are saved in Gameplay. Objects are spread laterally into pockets instead of a central line. Percentages describe approximate cumulative starting grain mass along the entrance-to-back direction; they do not lock pickups behind a global percentage. Players excavating a different route may discover them sooner or later.

Chest keys precede matching chests. Door keys sit in the preceding hangar, leaving optional cleanup behind them. The axe is found in H1 before H2 wood barriers; the hammer in H2 before H3 metal; the broom in H2 before H3 rats. Vehicle keys unlock buying the vehicle; they do not give it for free. The motoroller remains cash-only. Individual save IDs remain stable; a duplicate cash-bottle ID was repaired so both physical bottles can be collected independently.

All stamina and other perk bottles last five minutes, including the old bottle whose legacy save ID contains `stamina-hour`. Two guaranteed vouchers occur in H2 and H4. Chests retain their existing random pool: 45% cash, 15% free upgrade, 10% each of protection, mobility, infinite stamina and double-sale cash. Cash options increase through the chapters: $500 / $750 / $1,000 / $1,500 / $1,800 / $2,500. A chest's listed cash is an alternative to its other rewards, not an additional guaranteed payout.

## Economy and equipment

Prices, sale values, grain stock and the physical appearance of piles remain unchanged. The current forecast already funds the planned purchases without optional rewards. Expected grain revenue is about $205,604 after estimated pests, versus $143,950 for every shovel, stamina and vehicle upgrade and $8,904 container allowance. The modeled remaining balance is $52,750; all chapter balances remain positive even in the existing 20% loss stress scenario. This is a chapter-level solvency forecast, not a simulation of every purchase or trip.

Cash discoveries supplement selling instead of replacing it. Small early caches help the first purchases; later caches support containers and an upgrade gap. The $100,000 grain-sales achievement remains achievable through ordinary grain sales and does not count chest money.

The first vacuum now starts at 45 kg/s, adding 15 kg/s per upgrade dot. The second starts at 90 kg/s, adding 22.5 kg/s per dot (180 kg/s fully upgraded). This replaces the previous 540/1,080 kg/s starting rates, retaining the existing capacity and price progression while making suction take visible time. Manual shovel loads are now 3x larger (Basic starts at 7.5 kg) with wider/deeper excavation; vacuum rates and capacities retain their existing values. This speeds up manual excavation, so the chapter time targets need a fresh playtest. Fully upgrading is optional for completing the game.

## Authoring and checks

Use **Tools → Grain → Apply balanced 3-5 hour progression** to reapply this authored layout. It deliberately moves the planned finds, resets their reward amounts/durations and saves Gameplay plus the shovel settings asset. It does not run automatically on project opening. Use **Tools → Grain → Validate balanced progression** to check the current starting placement without moving it.

See `output/Progression/placement.md` for all find IDs and target/actual mass bands; `economy.md` for the full budget; and `validation.txt` for native scene checks. Existing saves retain their already-collected finds. Evaluate pacing with a fresh save without deleting the player's current save.
