using UnityEngine;

/// <summary>Returns a vehicle or wheelbarrow to the pose where it was placed.</summary>
[DisallowMultipleComponent]
public sealed class VehicleReset : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float holdSeconds = 1.5f;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 resetPosition;
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
        if (!TryFindResetPosition(out resetPosition)) { CancelHold(); requireRelease = true; return false; }
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
        if (vehicle != null) vehicle.ResetToPose(resetPosition, transitionRotation);
        else if (wheelbarrow != null) wheelbarrow.ResetToPose(resetPosition, transitionRotation);
        else transform.SetPositionAndRotation(resetPosition, transitionRotation);
    }

    private bool TryFindResetPosition(out Vector3 position)
    {
        Physics.SyncTransforms();
        position = transform.position;
        Bounds bounds = default;
        bool found = false;
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        if (!found) return false;
        Vector3 offset = bounds.center - transform.position;
        Vector3 extents = bounds.extents + new Vector3(.5f, 0f, .5f);
        for (int source = 0; source < 2; source++)
        for (int ring = 0; ring <= 20; ring++)
        {
            int samples = ring == 0 ? 1 : ring * 8;
            for (int i = 0; i < samples; i++)
            {
                float angle = i * Mathf.PI * 2f / samples;
                Vector3 center = (source == 0 ? transform.position : homePosition) + offset +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                if (!TryResetGround(center, out float ground)) continue;
                center.y = ground + extents.y + .1f;
                bool clear = true;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 probe = center + new Vector3((corner & 1) == 0 ? -extents.x : extents.x,
                        0f, (corner & 2) == 0 ? -extents.z : extents.z);
                    if (!TryResetGround(probe, out float height) || Mathf.Abs(height - ground) > .15f)
                    { clear = false; break; }
                }
                if (!clear) continue;
                foreach (var obstacle in Physics.OverlapBox(center, extents, Quaternion.identity,
                             ~0, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.transform.IsChildOf(transform)) continue;
                    clear = false; break;
                }
                if (!clear) continue;
                position = center - offset;
                return true;
            }
        }
        return false;
    }

    private bool TryResetGround(Vector3 point, out float height)
    {
        height = float.NaN;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 8f,
                     ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            height = hit.normal.y >= .9f ? hit.point.y : float.NaN;
        }
        return !float.IsNaN(height);
    }
    private void OnDisable() { CancelHold(); FinishReset(); }
}

