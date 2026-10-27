using UnityEngine;

/// <summary>Third-wheel presentation for the shared motor-driven vehicle rig.</summary>
[RequireComponent(typeof(MotorollerController)), DisallowMultipleComponent]
public sealed class TrikeController : MonoBehaviour
{
    [SerializeField] private Transform rearRightWheel;
    private BicycleController movement;
    private Quaternion drivenRest, rightRest;
    private bool initialized;
    public Transform RearRightWheel => rearRightWheel;
    public BicycleController Movement => movement != null ? movement : movement = GetComponent<BicycleController>();
    public void Initialize()
    {
        if (initialized) return;
        movement = GetComponent<BicycleController>();
        drivenRest = movement.rearWheel.localRotation;
        rightRest = rearRightWheel.localRotation;
        initialized = true;
    }
    private void Awake() => Initialize();
    private void LateUpdate() => RefreshWheels();
    public void RefreshWheels()
    {
        Initialize();
        rearRightWheel.localRotation = rightRest * Quaternion.Inverse(drivenRest) * movement.rearWheel.localRotation;
    }
}
