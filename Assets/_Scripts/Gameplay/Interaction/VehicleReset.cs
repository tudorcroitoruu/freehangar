using UnityEngine;

/// <summary>Returns a vehicle or wheelbarrow to the pose where it was placed.</summary>
[DisallowMultipleComponent]
public sealed class VehicleReset : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float holdSeconds = 2f;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float heldSeconds;
    private bool requireRelease;
    private BicycleController vehicle;
    private WheelbarrowCargo wheelbarrow;

    public float HoldSeconds => Mathf.Max(.1f, holdSeconds);
    public float HeldSeconds => heldSeconds;
    public float Progress => Mathf.Clamp01(heldSeconds / HoldSeconds);
    public bool CanReset
    {
        get
        {
            if (vehicle == null) vehicle = GetComponent<BicycleController>();
            if (wheelbarrow == null) wheelbarrow = GetComponent<WheelbarrowCargo>();
            return (vehicle != null && vehicle.isActiveAndEnabled) ||
                (wheelbarrow != null && wheelbarrow.isActiveAndEnabled && wheelbarrow.IsBeingPushed);
        }
    }

    private void Awake()
    {
        vehicle = GetComponent<BicycleController>();
        wheelbarrow = GetComponent<WheelbarrowCargo>();
        SetHomePose();
    }

    public void SetHomePose()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
        heldSeconds = 0f;
        requireRelease = false;
    }

    public void CancelHold() => heldSeconds = 0f;

    public bool TickHold(bool held, float deltaTime)
    {
        if (!held)
        {
            requireRelease = false;
            CancelHold();
            return false;
        }
        if (!isActiveAndEnabled || !CanReset || Time.timeScale <= 0f || deltaTime <= 0f || requireRelease)
        {
            CancelHold();
            return false;
        }
        heldSeconds = Mathf.Min(HoldSeconds, heldSeconds + deltaTime);
        if (heldSeconds < HoldSeconds) return false;
        ResetToHome();
        heldSeconds = 0f;
        requireRelease = true;
        return true;
    }

    private void Update()
    {
        if (!CanReset || Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup) CancelHold();
    }

    private void ResetToHome()
    {
        if (vehicle != null) vehicle.ResetToPose(homePosition, homeRotation);
        else if (wheelbarrow != null) wheelbarrow.ResetToPose(homePosition, homeRotation);
        else transform.SetPositionAndRotation(homePosition, homeRotation);
    }

    private void OnDisable() => CancelHold();
}
