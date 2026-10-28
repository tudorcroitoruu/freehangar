# Shovel ground pouring

Right-click while holding a loaded shovel and aiming at a reachable ground surface or pile. The normal pour gesture transfers the complete mixed load at its release point. Existing grain within 0.45 m receives the load when unobstructed; otherwise a 1.5 m footprint mini pile uses the scene's grain materials and instanced kernels. It can be scooped and poured again.

Deposits add local top strata, preserve each crop's mass, update the mesh/collider, and wake local settling. Invalid targets, pause, and cancellation before release retain cargo. Bag pouring remains left-click. No Inspector wiring is required. Dropped piles have the same session-only persistence as excavated pile state.

Validation: Unity compilation; Play Mode isolated interaction tests for cancellation, new pile creation, mixed mass, repeated pouring into the existing pile, and empty-shovel rejection. Separate deposit/scoop tests confirm 9.5 kg recovered with original crop quantities. No new Console errors. Returned Editor to Edit Mode.

Usability revision: ground pouring now uses the full 4 m pour reach and falls back to ground 1.1 m ahead when looking forward. The selected landing point stays fixed during the gesture, with distance and obstruction checks at release. Small piles (up to 2 m footprint) accept aim misses within 0.45 m and use an assisted scrape, preserving reach, occlusion, and capacity. Surface targeting includes thin remnants below the previous 2.5 cm cutoff. Play Mode checks passed for a 50 g remnant, camera movement during pouring, wall rejection with a nonempty pile, and previous drop regressions.
