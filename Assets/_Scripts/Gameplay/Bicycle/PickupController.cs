using UnityEngine;

/// <summary>Four-wheel and steering-wheel animation on the shared motor vehicle rig.</summary>
[RequireComponent(typeof(MotorollerController))]
public sealed class PickupController : MonoBehaviour
{
    public Transform frontRightSteering, frontRightWheel, rearRightWheel, steeringWheel;
    BicycleController movement;
    Quaternion steeringRest;
    void Awake()
    {
        movement = GetComponent<BicycleController>();
        steeringRest = steeringWheel.localRotation;
    }
    void LateUpdate() => RefreshWheels();
    public void RefreshWheels()
    {
        frontRightSteering.localRotation = Quaternion.Euler(0, movement.CurrentSteeringAngle, 0);
        frontRightWheel.localRotation = movement.frontWheel.localRotation;
        rearRightWheel.localRotation = movement.rearWheel.localRotation;
        // These imported wheels lie in local YZ; local X follows the steering column.
        steeringWheel.localRotation = steeringRest * Quaternion.AngleAxis(-movement.CurrentSteeringAngle * 2.5f, Vector3.right);
    }
}
