# Weigh station — 2026-10-28

Gameplay's existing `New_Map/Weigh Station` now has a textured steel scale,
roof seams, hazard borders, inward arrows, floor instructions and a supported
status display. The original station transform and surrounding user edits were
preserved. A pre-edit scene copy is in `output/WeighStation/Before.unity`.

`GrainWeighStation` lives on the `Selling scale` child. It scans the entire
7 × 12 m platform (4 m high), while accepting unloaded GrainContainer sacks or
boxes wholly inside the 3.8 × 4.8 m central deck. Empty containers, held cargo,
vehicle-attached cargo and moving containers are not accepted. Decorative boxes
without GrainContainer contents have no sale value. Players, rideable vehicles,
wheelbarrows and unknown rigidbody cargo block selling. Saturated overlap queries
fail closed.

After 1.25 seconds clear, each container pops upward and shrinks to zero over
0.8 seconds. Entering during the animation cancels it, restores its transform
and physics, and awards nothing. Disabling the station also restores pending
cargo. The disabled receiver locks pickup/filling during sale. Payment uses
SaveSystem.AddMoney, including the existing money event and HUD refresh. Default
rate is 2 cash/kg, rounded down to whole cash; inspector adjustable. Missing
profile or overflow preserves cargo. Sale receipts appear on the status board.
Cash uses existing save behavior; this feature does not add world-container
persistence across scene reloads.

Validation: Unity compilation passed; 16 Play Mode checks passed, including
empty/held/outside cargo, player and actual vehicle blocking, mid-animation
cancellation, lift animation, exact payment, duplicate prevention, sequential
sales, missing profile, overflow, disable recovery and a real SmallBag prefab
on the authored station. Test saves and money were restored. The first test rig
used an unconfigured BicycleController and logged an initialization exception;
it was replaced with a configured vehicle instance and the complete rerun had
no new exceptions. Existing unreadable-mesh QuickOutline issue is unrelated;
the sack fixture omits Outline. No standalone build was run.

Authoring and repeatable validation scripts: `output/WeighStation/`.
Run through `unity command script run --file ... --entry Type.Run` (setup in
Edit Mode; validation in Play Mode). Visual captures are in the same folder.
