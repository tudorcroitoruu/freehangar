# Grain surface CPU spikes — 2026-11-01

The supplied Profiler capture identifies CornSurfaceKernels.LateUpdate at 64.2% of a slow frame. A repeated-excavation benchmark against the saved Gameplay Hangar 5 reproduces substantial work: 753,457 authored kernels, up to 81,600 updated per frame, mean 31.885 ms, p95 35.721 ms, max 35.724 ms. No managed allocations were measured.

Changed cells now enter a deduplicated bounded queue. Kernel transforms resume within cells across frames, with a 4,096-kernel ceiling and a 1 ms processing target checked every 64 kernels. Later edits requeue cells so partially processed work cannot miss newer heights. LateUpdate drains pending work even when the simulation revision does not change. Batch visibility uses counts instead of rescanning every matrix; conservative bounds cover the excavation envelope and expand on deposits.

The equivalent repeated-excavation workload now costs mean 1.090 ms, p95 1.104 ms, max 1.105 ms per update, up to 3,200 kernels per frame, zero managed allocations. Remaining edits finish in 26 additional update frames. Density remains 753,457 kernels; all five saved piles retain their original kernel counts. Visual changes are gradual across frames; grain quantities and collection are immediate and unchanged.

Unity EditMode validation: 31 tests passed, zero failed. Tests check bounded work, updates between simulation ticks, successive edits during partial updates, final height parity for every sampled kernel, cleared-batch invisibility, refill visibility and bounds, plus existing shovel/economy regressions. Editor compilation passes.

These are Editor CPU microbenchmarks, not a standalone gameplay FPS comparison. Other frame costs and OS scheduling can still produce spikes. Manual rerun: Tools > Grain > Measure hangar kernel updates; Tools > Grain > Validate interaction performance behavior. No automatic audit runs on opening the project.
