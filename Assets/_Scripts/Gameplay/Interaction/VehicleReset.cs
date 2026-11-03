using UnityEngine;

/// <summary>Returns a vehicle or wheelbarrow to the pose where it was placed.</summary>
[DisallowMultipleComponent]
public sealed class VehicleReset : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float holdSeconds = 1.5f;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float heldSeconds;
    private bool requireRelease;
    private Vector3 normalScale, transitionPosition;
    private Transform visualRoot;
    private Vector3 visualScale;
    private Quaternion transitionRotation;
    private float resetElapsed;
    private Collider[] resetColliders;
    private bool[] colliderStates;
    public bool IsResetting { get; private set; }
    private BicycleController vehicle;
    private WheelbarrowCargo wheelbarrow;

    public float HoldSeconds => Mathf.Max(.1f, holdSeconds);
    public float HeldSeconds => heldSeconds;
    public float Progress => Mathf.Clamp01(heldSeconds / HoldSeconds);
    public bool CanReset
    {
        get
        {
            if (IsResetting) return false;
            if (vehicle == null) vehicle = GetComponent<BicycleController>();
            if (wheelbarrow == null) wheelbarrow = GetComponent<WheelbarrowCargo>();
            return (vehicle != null && vehicle.isActiveAndEnabled && !vehicle.IsRidden) ||
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
        BeginReset();
        heldSeconds = 0f;
        requireRelease = true;
        return true;
    }

    private void Update()
    {
        if (!CanReset || Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup) CancelHold();
    }

    private void BeginReset()
    {
        normalScale = transform.localScale;
        transitionPosition = transform.position;
        transitionRotation = transform.rotation;
        // Stop coasting before hiding and preserve each collider's authored/runtime state.
        if (vehicle != null) vehicle.ResetToPose(transitionPosition, transitionRotation);
        var alignment = GetComponent<VehicleGroundAlignment>();
        visualRoot = alignment != null ? alignment.Pose : null;
        // Keep the movement/physics root at its authored scale throughout reset.
        if (visualRoot == null) visualRoot = transform;
        visualScale = visualRoot.localScale;
        resetColliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[resetColliders.Length];
        for (int i = 0; i < resetColliders.Length; i++)
        {
            colliderStates[i] = resetColliders[i].enabled;
            resetColliders[i].enabled = false;
        }
        resetElapsed = 0f;
        IsResetting = true;
    }

    private void LateUpdate()
    {
        if (IsResetting) AdvanceReset(Time.deltaTime);
    }

    private void AdvanceReset(float deltaTime)
    {
        const float shrinkSeconds = .25f, growSeconds = .3f;
        float previous = resetElapsed;
        resetElapsed += Mathf.Max(0f, deltaTime);
        if (previous < shrinkSeconds && resetElapsed >= shrinkSeconds)
        {
            ResetToHome();
            transitionPosition = transform.position;
            transitionRotation = transform.rotation;
        }
        float factor = resetElapsed < shrinkSeconds
            ? 1f - Mathf.SmoothStep(0f, 1f, resetElapsed / shrinkSeconds)
            : Mathf.SmoothStep(0f, 1f, (resetElapsed - shrinkSeconds) / growSeconds);
        transform.SetPositionAndRotation(transitionPosition, transitionRotation);
        // Keep physics transforms invertible while fully visually hidden.
        if (visualRoot != null) visualRoot.localScale = visualScale * Mathf.Max(.0001f, factor);
        if (resetElapsed >= shrinkSeconds + growSeconds)
        {
            FinishReset();
            GameplayAudio.Instance?.VehicleResetComplete();
        }
    }

    private void FinishReset()
    {
        if (!IsResetting) return;
        if (visualRoot != null) visualRoot.localScale = visualScale;
        transform.localScale = normalScale;
        for (int i = 0; i < resetColliders.Length; i++)
            if (resetColliders[i] != null) resetColliders[i].enabled = colliderStates[i];
        resetColliders = null; colliderStates = null;
        IsResetting = false;
        ResetToHome(); // Align once, after the visual hierarchy has its full scale again.
    }

    private void ResetToHome()
    {
        if (vehicle != null) vehicle.ResetToPose(homePosition, homeRotation);
        else if (wheelbarrow != null) wheelbarrow.ResetToPose(homePosition, homeRotation);
        else transform.SetPositionAndRotation(homePosition, homeRotation);
    }

    private void OnDisable() { CancelHold(); FinishReset(); }
}
