from pathlib import Path
p=Path('Assets/_Scripts/Gameplay/Bicycle/BicycleController.cs');s=p.read_text();s=s.replace('    public Vector3 RidingCameraOffset => ridingCameraOffset;','''    [Header("Pedaling camera motion")]
    [Tooltip("Subtle pitch in degrees, synchronized with the crank. Set to zero to disable.")]
    [SerializeField, Range(0f, 2f)] private float pedalingPitchAmplitude = .45f;
    [SerializeField, Min(.01f)] private float pedalingPitchSmoothTime = .09f;
    public float PedalingCameraPitch { get; private set; }
    public Vector3 RidingCameraOffset => ridingCameraOffset;''');s=s.replace('IsRidden = value; Speed = 0; fallSpeed = 0; steeringVelocity = 0;','IsRidden = value; Speed = 0; fallSpeed = 0; steeringVelocity = 0; PedalingCameraPitch = 0;');s=s.replace('''        Quaternion rotation = Quaternion.Euler(crankAngle,0,0);''','''        // Use the actual crank phase, so the rider motion follows the animated feet.
        bool pedaling = Mathf.Abs(throttle) > .01f && !brake && Mathf.Abs(Speed) > .1f;
        float pitchTarget = pedaling ? Mathf.Sin(crankAngle * Mathf.Deg2Rad) * pedalingPitchAmplitude *
            Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(.1f, maximumSpeed)) : 0f;
        PedalingCameraPitch = Mathf.Lerp(PedalingCameraPitch, pitchTarget,
            1f - Mathf.Exp(-dt / Mathf.Max(.01f, pedalingPitchSmoothTime)));
        Quaternion rotation = Quaternion.Euler(crankAngle,0,0);''');p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/PlayerBicycleInteraction.cs');s=p.read_text();s=s.replace('Quaternion.Euler(pitch + bicycle.RidingCameraPitch, yaw, 0)','Quaternion.Euler(pitch + bicycle.RidingCameraPitch + (IsAdjustingRidingPosition ? 0 : bicycle.PedalingCameraPitch), yaw, 0)');p.write_text(s)
