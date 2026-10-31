# Central grain economy forecast

Prices, upgrade references, loss estimates and starting-stock snapshots come from Assets/Resources/Economy/GrainEconomySettings.asset. Re-capture stock after changing scene geometry/crop layout. Starting cash: $0.

Pest percentages estimate exposure during play; actual losses come only from feeding bites. Rat and bird percentages are additive, capped at 100%. Totals/kg use comma thousands with no decimals; configured unit prices retain precision. Sales truncate to whole dollars per crop before summing; transaction rounding and optional consumables are not included in the forecast.

| Crop | Physical kg | Collectible kg | Rats kg | Birds kg | Saleable kg | $/kg | Gross $ | Expected $ |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Corn | 2,184,125 | 10,921 | 138 | 138 | 10,645 | 2.00 | 21,841 | 21,290 |
| Wheat | 2,461,704 | 12,309 | 453 | 345 | 11,511 | 2.40 | 29,540 | 27,626 |
| Barley | 1,812,213 | 9,061 | 453 | 345 | 8,263 | 2.20 | 19,934 | 18,179 |
| Oats | 1,812,376 | 9,062 | 453 | 345 | 8,264 | 2.60 | 23,561 | 21,486 |
| Rye | 551,854 | 2,759 | 138 | 138 | 2,483 | 2.00 | 5,519 | 4,967 |
| Rice | 2,915,922 | 14,580 | 729 | 621 | 13,230 | 3.40 | 49,571 | 44,982 |
| Peas | 551,854 | 2,759 | 138 | 138 | 2,483 | 3.60 | 9,933 | 8,940 |
| Soybeans | 2,364,068 | 11,820 | 591 | 483 | 10,747 | 4.00 | 47,281 | 42,986 |
| SunflowerSeeds | 551,854 | 2,759 | 138 | 138 | 2,483 | 4.20 | 11,589 | 10,430 |
| Buckwheat | 275,927 | 1,380 | 69 | 69 | 1,242 | 3.80 | 5,243 | 4,718 |

| Hangar | Gross $ | Expected after pests $ | Containers $ | Upgrades $ | Expected balance $ | Stress balance $ |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 1,170 | 1,170 | 96 | 650 | 424 | 190 |
| 2 | 22,949 | 22,949 | 1,519 | 15,000 | 6,853 | 2,030 |
| 3 | 31,626 | 30,044 | 1,400 | 25,300 | 10,198 | 630 |
| 4 | 79,696 | 71,726 | 3,234 | 42,000 | 36,690 | 19,153 |
| 5 | 88,573 | 79,715 | 2,655 | 61,000 | 52,750 | 26,356 |

Gross: $224,012. Expected after rats/birds: $205,604. Shovel: $101,875; stamina: $8,275; vehicles: $33,800; ALL upgrades: $143,950. Containers: $8,904. Final expected balance after ALL upgrades: $52,750.

Stress total-loss scenario: 20%. Actual feeding: rat group 1 kg/bite; bird 1 kg/bite (intro flock shares this amount); interval 3 seconds.
