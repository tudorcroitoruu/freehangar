# Scoop/pour CPU hitch investigation

The reported symptom begins with scooping/filling and persists despite a high FPS average. Two repeatable CPU costs were isolated in native Unity Editor measurements on the same machine:

| Work | Before mean | After mean |
|---|---:|---:|
| FreeBag surface mesh bake + sampling, 75% full | 8.864 ms | 0.629 ms |
| Moving that bag without changing fill/closure | previously repeated bake/sample path | 0.030 ms; zero rebakes |
| Loaded shovel simulation, steady 3 m/s walking | 1.609 ms | 0.013 ms |

Each scoop/fill can trigger expensive work on an individual frame; an FPS average can conceal these spikes. The bag sampler previously tested every surface triangle for each kernel sample. It now bins triangles spatially, preserving the same barycentric sampling and highest-surface result. Bag translation/rotation only transforms cached kernel matrices; actual morph or scale changes still resample. This also removes 20 Hz movement updates of kernels on carried bags.

The shovel solver previously counted any walking velocity as a disturbance. It therefore never slept while walking, running all kernel pairs and changing the combined visible mesh continuously. Constant velocity now permits sleeping; acceleration, rotation, teleportation and fill changes still wake the load. Visible counts and cosmetic streams are unchanged. Kilograms and scoop/pour gameplay were not changed.

The isolated height field settled three scoops in seven steps (~0.49 ms total), so its behavior was left unchanged.

Six native Unity tests pass: accelerated sampling matches exhaustive sampling over the bag footprint at three fill amounts; rigid movement keeps the kernels attached without baking; scaling resamples density; uniform walking sleeps without altering the load and acceleration/turning wake it. Measured steady paths allocate 0 managed bytes per call after warmup.

These are Editor CPU microbenchmarks, not a before/after standalone FPS or frame-time capture. Actual standalone frame pacing and GPU timings have not been recorded. Rebuild the player to include the fix. Raw measurements and test results are beside this report.

Manual tools: Tools > Grain > Measure interaction CPU work; Tools > Grain > Validate interaction performance behavior. Neither runs automatically on project opening.
