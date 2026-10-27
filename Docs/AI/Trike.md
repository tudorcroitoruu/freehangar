# Driveable Trike — 2026-10-27

Gameplay now contains `Trike - driveable`, saved as `Assets/Models/3WheelCar/Driveable/Trike_Driveable.prefab`. The original Trike is inactive and its FBX/materials are preserved. Scene position, heading and 1.1 scale are retained; root is upright with +Z forward and ground-level origin. Ground placement is sampled from the active terrain.

The existing articulated FBX already had axle and steering pivots. Prepared Unity mesh copies bake imported axis transforms and recalculate bounds/tangents. No geometry decimation was performed: imported mesh copies total 116,814 triangles. Front fork, mudguard, handlebar and wheel share the inclined steering parent. All three wheels spin; the right rear wheel follows the driven rear axle delta through TrikeController. Doors and bed panels retain their separate meshes and hinge origins; no door/tailgate controls were added.

TrikeController composes the existing MotorollerController engine settings and BicycleController movement rig. Engine settings: 10 m/s forward, up to 3 m/s reverse, 2.5 m/s² acceleration, 8 m/s² braking, 0.28586 m wheel radius, 2.55731 m wheelbase. Root scale is accounted for. Body sweep is sized for the 3.8 m long vehicle. Walking exits and box unloading use the wider side clearance (1.65 m); longitudinal exits use 2.65 m. Movement is an upright arcade CharacterController, not a suspension simulation.

Controls: aim at the Trike and E to enter; W/S drive/reverse, A/D steer, Space brake, mouse look, E exit where clear. Escape releases the cursor and brakes. Engine driving uses no stamina. Driver View is adjustable through the Trike Inspector. Local copies of the source materials enable double-sided cabin rendering.

Four cargo bed anchors use the existing filled-bag and BicycleCargoBox flows. Carry a finished bag, aim at the Trike and E to stow; aim at it and use E to retrieve. Nearby marked boxes can be loaded; E on a loaded box unloads beside the vehicle. Bag species/mass are unchanged. No buying, fuel, engine audio, persistence, or gamepad bindings were added.

Validation: 29 Trike Play Mode checks passed (mounting, three wheels, steering, acceleration, reverse, brakes, no stamina cost, wall/ground collision and restoration). Eleven targeting/bag cargo checks and seven full-bed box/unloading checks passed. Existing Motoroller (27) and bicycle (22) regressions passed. Driver/exterior captures inspected; parking hull has no overlapping obstacles. No standalone build or hardware performance benchmark performed. Scripts, captures and test results: output/Trike/.
