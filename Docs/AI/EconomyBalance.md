# Central grain economy

Current configuration: `Assets/Resources/Economy/GrainEconomySettings.asset`.

New games start with **$0**. Existing saved wallets are retained. The unlimited 10 kg free-bag inventory dispenser lets a player earn the first money without buying a container.

Economy totals and displayed kg use invariant accounting grouping (`12,500`), without decimal places. Unit selling prices display exactly two decimal places (`$2.60/kg`) in the selling screen and report. Sale proceeds are truncated to whole dollars separately for each crop, then those exact displayed amounts are added for the wallet payout. Currency multiplication removes binary float noise, so 50 kg at $2.6 pays $130. Physical grain kg keep their internal precision.

Accounting verification: six new cases pass (Romanian/German/French locales, invoice totals, invalid/overflow quotes and the sale row). The suite is 200/207 with the same seven existing interaction/carry failures; see `output/Economy/accounting-regression-tests.xml`. Subsequent bag/HUD formatting changes also compile successfully.

## Control and forecasts in one Inspector

Select the economy asset. It contains:

- `grainConsumeMultiplier`, initially 200.
- Selling price and sprite for each of the ten crops. SellingZone uses these entries directly.
- Container product IDs, prefab references and purchase prices. ShopManager supplies these prices to ShopProduct prompts, affordability checks and purchases. Capacities are read from the container prefabs.
- References to ShovelUpgradeSettings, StaminaUpgradeSettings and VehicleUpgradeSettings. Their prices are summed directly; the starting vehicle is excluded. The attached SOs can be edited in foldouts in this same Inspector. Runtime upgrade screens, tool/stamina statistics and vehicle upgrades resolve these shared references.
- Rat-group and bird feeding amounts and the feeding interval. GrainPestDirector passes the economy to spawned pests, which use these values for actual damage.
- The five starting-stock snapshots, per-hangar rat/bird loss estimates, and purchase targets for the stage budget.
- A configurable total-loss stress scenario, initially 20%.

The calculated section is read-only and updates immediately when inputs change. It shows revenue before/after pests, each upgrade category and its combined total, estimated container spending, and remaining cash. Every crop has its own revenue row and expandable physical kg, collectible kg, rat loss, bird loss and saleable kg. The stage view shows expected and stress-case cash after each hangar.

Use **Read starting stock from the five open-scene piles** after changing pile shape, scale, crop regions or layers. A project asset cannot reliably store references to scene objects, so it stores their captured starting kg by crop instead. Capturing includes inactive closed hangars and retains edited loss estimates and purchase targets. Reset cleared-pile previews before capturing. Changing multiplier or prices needs no new capture: those are calculated live from the same physical stock.

**Write this economy's full report**, or **Tools > Grain > Write Economy Report**, exports `output/Economy/current-balance.md`. This report also uses the asset's snapshots and price references, so it can be generated with the scene closed.

## Consumption and losses

Physical `GrainPile.Kilograms` still drives clearance and the final launch. `CollectibleKilograms` is physical kg divided by the multiplier. At multiplier 10, collecting 10 kg consumes 100 authored kg. Scoop geometry scales to excavate the extra authored volume. Dropped piles recover cargo at 1:1; returning cargo to an authored pile restores equivalent collectible mass.

Expected rat and bird losses are separately configurable per hangar. They are additive shares of collectible kg, capped at 100%. They are **planning assumptions about exposure during play**, not a second automatic removal of grain. Actual losses depend on attacks, exposed cargo and how quickly the player scares pests away.

Current assumptions: no pests in hangars 1–2, rats 5% in hangar 3, rats 5% plus birds 5% in hangars 4–5. Actual damage retains the previous baseline: one kg per rat-group bite every three seconds; a single attacking bird eats one kg per bite, while the three introductory birds share that one kg. Rats divide the group amount between the attacking group members.

Change multiplier, feeding amounts and balance inputs before a new run. Editing the multiplier during play revalues remaining stock. Forecasts apply the configured per-crop loss share uniformly within each hangar; actual attacks may eat more of one crop than another.

## Current sale and container prices

| Crop | $/kg |
|---|---:|
| Corn | 2.00 |
| Wheat | 2.40 |
| Barley | 2.20 |
| Oats | 2.60 |
| Rye | 2.00 |
| Rice | 3.40 |
| Peas | 3.60 |
| Soybeans | 4.00 |
| Sunflower seeds | 4.20 |
| Buckwheat | 3.80 |

| Container | Capacity kg | Purchase $ | Availability |
|---|---:|---:|---|
| Free bag | 10 | 0 | Unlimited inventory dispenser |
| Small bag | 25 | 4 | Hangar 1 half cleared |
| Large bag | 50 | 7 | Hangar 2 opened |
| Crate | 80 | 10 | Hangar 3 opened |
| Barrel | 120 | 14 | Hangar 4 opened |
| Big bag | 500 | 45 | Hangar 5 opened |

Containers are destroyed on sale, so the budget pays for new containers repeatedly. Container allowances round up separately for each crop and hangar, using the configured stage container and its actual prefab capacity. Allowances use the full original collectible mass even if pests reduce sales, which keeps this expense conservative. The first hangar is charged Small Bags for all grain despite free bags being available. Repeatedly selling tiny loads or abandoning containers can exceed this allowance. Whole-dollar sale rounding and optional consumables are excluded.

Only `WeightZone_2`'s SellingZone / SellingScreen flow pays cash. GrainWeighStation is enabled as an instruction display only: park the vehicle, step off, load grain and press the red weigh button on the left. It has no automatic sale, payout, cargo animation or container removal. Scene goal cash rewards are zero, and seven cash chest/cache objects are disabled. Other keys, tools, stamina finds and vouchers remain; vouchers and pre-existing sample containers are not needed by this budget.

## Upgrade prices and route

| Tool | Purchase 1 | Purchase 2 | Purchase 3 | Purchase 4 | Total $ |
|---|---:|---:|---:|---:|---:|
| Basic Shovel | 50 | 75 | 100 | 150 | 375 |
| Medium Shovel | 150 | 200 | 250 | 350 | 950 |
| Improved Shovel | 450 | 650 | 850 | 1,100 | 3,050 |
| Expert Shovel | 1,400 | 1,800 | 2,300 | 3,000 | 8,500 |
| Grain Vacuum | 3,500 | 4,000 | 5,000 | 6,500 | 19,000 |
| High Capacity Vacuum | 12,000 | 15,000 | 19,000 | 24,000 | 70,000 |

Stamina: **75 / 400 / 1,800 / 6,000** = **$8,275**.

Vehicles after the free bicycle: **Motoroller 200 / Trike 600 / Lawnmower 1,500 / Pickup 4,500 / Pickup Bigger 9,000 / Pickup Cargo 18,000** = **$33,800**. The nested mower/trailer prefab is supported by vehicle replacement.

Each destination vehicle can independently require a collected key in addition to its price: set `requiresKey`, `keyId` and `keySprite` in VehicleUpgradeSettings. Empty key IDs use `vehicle.key.<vehicle id>`; possession is checked in PlayerData.foundSecrets, and keys are not consumed. A voucher covers the price but never bypasses the key. UpgradeVehicle shows `keyParent` whenever the next vehicle requires a key, and assigns that destination vehicle's sprite to `keyImage`. The UI clears the image and hides the holder for upgrades without keys, no target and maximum level. The six matching existing vehicle key sprites are assigned; existing requiresKey choices are retained.

All 24 tool purchases cost **$101,875**; all upgrade categories together cost **$143,950**. These are calculated from referenced SO prices, not copied totals.

| Hangar | Collectible kg | Gross $ | Expected after pests $ | Containers $ | New upgrades $ | Expected cash left $ | Cash at 20% total loss $ |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | 585 | 1,170 | 1,170 | 96 | 650 | 424 | 190 |
| 2 | 10,825 | 22,949 | 22,949 | 1,519 | 15,000 | 6,853 | 2,030 |
| 3 | 10,831 | 31,626 | 30,044 | 1,400 | 25,300 | 10,198 | 630 |
| 4 | 27,576 | 79,696 | 71,726 | 3,234 | 42,000 | 36,690 | 19,153 |
| 5 | 27,593 | 88,573 | 79,715 | 2,655 | 61,000 | 52,750 | 26,356 |

Totals: **77,409 collectible kg**, **$224,012 gross**, **$205,604 expected after pests**, **$8,904 containers**, **$52,750 expected left after every upgrade**. The 20% stress budget is also positive at every stage. If configured pest losses exceed the stress percentage, the worse forecast is used; losses are never counted twice.

Cumulative stage targets are editable in the economy asset:

1. Hangar 1: shovel level 4, stamina 1, Motoroller.
2. Hangar 2: shovel level 16 (first vacuum), stamina 2, Lawnmower after Trike.
3. Hangar 3: shovel level 20 (second vacuum), stamina 3, Pickup.
4. Hangar 4: shovel level 22, stamina 4, Pickup Bigger.
5. Hangar 5: shovel level 24, stamina 4, Pickup Cargo.

These describe an affordable route rather than restricting spending. Tutorial fill/load targets use 10/20 kg and hangar 1 delivery/moved targets use 200/400 kg.

## Verification and limits

Targeted tests cover zero starting cash, per-crop math and separate pest losses, price edits updating sale quotes and ShopManager prices, upgrade totals reading the attached SOs, container capacities, arbitrary stage targets, capped/invalid loss inputs and real rat/bird bites using configured damage and interval. Existing multiplier/recovery tests remain in GrainEconomyTests.

`output/Economy/ValidateBalance.cs` checks central price wiring, snapshots, upgrade totals and affordability against the actual Gameplay scene. Test result files in the same folder record the latest checks.

Latest verification: compilation succeeded; all **93 relevant tests** passed, including ten central-economy cases and fifteen multiplier/recovery cases. The full first-party Edit Mode suite is **194/201 passed**. The same seven existing PlayerInteractorTests / PlayerItemCarryTests failures remain; the before/after failure names are unchanged. Results: `output/Economy/central-regression-results.json` and `central-regression-tests.xml`.

The 4–6 hour completion target remains provisional pending a timed new-game playtest. Existing stock/cargo persistence remains session-only: loading a scene can restore authored grain while cash/upgrades remain saved. Persistent stock/cargo saving is a separate requirement before claiming a complete campaign economy. Existing interaction/carry test failures and end-to-end tutorial event wiring are also tracked separately.

`CentralizeEconomy.cs` records the migration into the central SO. Old `ApplyBalance.cs` is historical baseline authoring; use the SO Inspector for ongoing tuning. Backups are in `output/Economy/Backups`.
