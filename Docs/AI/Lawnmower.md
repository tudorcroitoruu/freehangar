# Lawnmower and tow trailer

Gameplay contains `Lawnmower with trailer - driveable`, saved as `Assets/Models/Lawnmower/Driveable/Lawnmower_With_Trailer.prefab`. The original Lawnmower hierarchy (including Tow_Trailer) is retained inactive. The prepared assembly preserves its placement, materials and LODs. Mower forward is +Z; existing wheel geometry is carried by centered steering/spin pivots.

Controls use PlayerBicycleInteraction: aim and E to mount, W/S drive/reverse, A/D steer, Space brake, mouse look, E to dismount. MotorollerController/BicycleController supply upright arcade movement: 5 m/s forward, 1.5 m/s reverse, 1.8 m/s² acceleration, 6 m/s² braking. There is no stamina boost. Driver view is adjustable in the BicycleController Inspector.

LawnmowerController animates the remaining wheels and steering wheel, updates a kinematic hitch in FixedUpdate, and checks trailer clearance before driving. Tow_Trailer has a separate 100 kg Rigidbody, compound bed/drawbar colliders and two suspended WheelColliders. A ConfigurableJoint locks translation while permitting yaw ±70°, pitch ±20° and roll ±15°. The support leg is hidden while attached. Mower/trailer self-collisions are excluded, while environment contacts remain enabled. Trailer obstruction checks stop the kinematic mower from forcing the trailer through walls. The joint, trailer and hitch are siblings under the stationary prefab assembly; do not parent the dynamic trailer under the moving mower.

Validation in Play Mode: 13 towing/physics checks (forward, turning, reverse, brakes, wheel support, hitch retention and trailer wall blocking), 11 targeting/mounting/steering/dismount checks, and an actual road-incline traversal passed. Driver and exterior renders inspected. Existing bicycle 22, Motoroller 27 and Trike 29 regression checks passed. Final passing runs introduced no runtime errors. An earlier failed fixture assertion produced a camera-parent cleanup error; the assertion was corrected to compare local axle position and fixture cleanup now dismounts before destruction. Tests/captures: output/Lawnmower. Source scene backup: output/Lawnmower/Backups/Gameplay.before-mower.unity.

Scope is driving and attached trailer mechanics; mowing grass, trailer cargo slots, detach controls, fuel and audio are not implemented.

