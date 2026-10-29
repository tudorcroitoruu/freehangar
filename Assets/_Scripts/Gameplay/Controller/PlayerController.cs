using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Camera characterCamera;
    public CharacterController characterController;
    [SerializeField] private Transform heldInstrument;
    [SerializeField] private ShovelTool shovelTool;
    [SerializeField] private BagInteraction bagInteraction;
    [SerializeField] private StaminaManager staminaManager;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 3f;
    [SerializeField, Min(0f)] private float sprintSpeed = 10f;
    [SerializeField, Min(0f)] private float jumpForce = 5f;
    [SerializeField, Min(0f)] private float gravity = 9.81f;

    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraSensitivity = 2f;
    [SerializeField] private float cameraMinAngle = -70f;
    [SerializeField] private float cameraMaxAngle = 80f;
    [SerializeField, Min(0.001f)] private float lookSmoothTime = 0.06f;

    [Header("Sprint camera motion")]
    [SerializeField, Range(0f, .04f)] private float sprintCameraHeight = .004166667f;
    [SerializeField, Range(0f, 2f)] private float sprintCameraPitch = .15625f;
    [SerializeField, Range(1f, 4f)] private float sprintStepFrequency = 2.6f;
    private float sprintCameraPhase, sprintCameraBlend;

    [Header("Crouch (hold Ctrl or C)")]
    [SerializeField, Min(0.1f)] private float crouchHeight = 1.2f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;
    [SerializeField, Min(0.1f)] private float crouchTransitionSpeed = 5f;
    [SerializeField] private LayerMask crouchObstructionMask = ~0;

    [Header("Held instrument animation")]
    [SerializeField, Min(0f)] private float instrumentSway = 1.5f;
    [SerializeField, Min(0f)] private float instrumentSmoothness = 12f;
    [SerializeField, Min(0f)] private float idleAmplitude = 0.008f;
    [SerializeField, Min(0f)] private float idleFrequency = 0.8f;

    private PlayerBicycleInteraction bicycleInteraction;
    private PlayerInteractor interactor;
    private PlayerCombatTools combatTools;
    private float verticalVelocity;
    private float cameraRotationX;
    private float targetYaw;
    private float pushLookYaw, pushLookTarget;
    private bool IsPushing => interactor != null && interactor.PushedWheelbarrow != null;
    private float yawVelocity;
    private float pitchVelocity;
    private float smoothedPitch;
    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 cameraRestPosition;
    private Vector3 instrumentRestPosition;
    private Quaternion instrumentRestRotation;
    private Vector2 lookInput;
    private float idleTime;
    private Quaternion previousPresentationCamera;
    private float turnRoll;
    public float InstrumentTurnRoll => turnRoll;
    private bool isCrouching;
    private readonly Collider[] crouchHits = new Collider[32];

    public bool IsCrouching => isCrouching;
    public StaminaManager Stamina => staminaManager;
    public Transform HeldInstrument => heldInstrument;
    public ShovelTool Shovel => shovelTool;
    public bool IsShovelEquipped => shovelTool != null && shovelTool.isActiveAndEnabled &&
        (heldInstrument == null || heldInstrument.gameObject.activeInHierarchy);
    public bool IsCarrying => (bagInteraction != null && bagInteraction.IsCarrying) ||
        (interactor != null && interactor.Items != null && interactor.Items.IsCarrying);

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        characterCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        bicycleInteraction = GetComponent<PlayerBicycleInteraction>();
        combatTools = GetComponent<PlayerCombatTools>();
        if (bagInteraction == null) bagInteraction = GetComponent<BagInteraction>();
        if (staminaManager == null && !TryGetComponent(out staminaManager))
            staminaManager = FindFirstObjectByType<StaminaManager>();
        if (staminaManager == null)
            staminaManager = gameObject.AddComponent<StaminaManager>();
        if (characterCamera == null)
            characterCamera = GetComponentInChildren<Camera>();

        if (characterCamera == null)
        {
            Debug.LogError("PlayerController requires a child camera.", this);
            enabled = false;
            return;
        }

        cameraRotationX = Mathf.DeltaAngle(0f, characterCamera.transform.localEulerAngles.x);
        smoothedPitch = cameraRotationX;
        targetYaw = transform.eulerAngles.y;
        standingHeight = characterController.height;
        standingCenter = characterController.center;
        cameraRestPosition = characterCamera.transform.localPosition;
        previousPresentationCamera = characterCamera.transform.rotation;
        if (!TryGetComponent<PlayerAim>(out _)) gameObject.AddComponent<PlayerAim>();
        if (!TryGetComponent(out interactor)) interactor = gameObject.AddComponent<PlayerInteractor>();
        if (heldInstrument != null)
        {
            instrumentRestPosition = heldInstrument.localPosition;
            instrumentRestRotation = heldInstrument.localRotation;
        }
    }

    private void OnEnable()
    {
        if (shovelTool != null) shovelTool.GrainScooped += HandleGrainScooped;
        verticalVelocity = 0f;
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (shovelTool != null) shovelTool.GrainScooped -= HandleGrainScooped;
        if (shovelTool != null) shovelTool.CancelAction();
        SetCursorLocked(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked(false);
    }

    private void Update()
    {
        if (UpgradeManager.BlocksGameplayInput)
        {
            if (shovelTool != null) shovelTool.CancelAction();
            if (characterController.enabled && Time.deltaTime > 0) HandleMovement(false);
            if (staminaManager != null) staminaManager.Tick(false, Time.deltaTime);
            return;
        }
        if (interactor != null && interactor.DiscoveryBusy)
        {
            if (characterController.enabled && Time.deltaTime > 0) HandleMovement(false);
            if (staminaManager != null) staminaManager.Tick(false, Time.deltaTime);
            return;
        }
        bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(false);
        else if (Input.GetMouseButtonDown(0) && !UIPopups.BasePopupUI.HasActivePopup)
            SetCursorLocked(true);

        bool bicycleInput = Application.isFocused && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup;
        if (bicycleInput && interactor != null && interactor.TryAdditionalInput()) return;
        if (bicycleInteraction != null && bicycleInteraction.Tick(bicycleInput)) return;

        if (!characterController.enabled || Time.deltaTime <= 0f)
            return;

        bool acceptInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup;
        if (acceptInput && Input.GetKeyDown(KeyCode.G)) ToggleShovel();
        lookInput = Vector2.zero;
        if (acceptInput)
            HandleCamera();

        HandleCrouch(acceptInput);
        HandleMovement(acceptInput);
        if (combatTools != null) combatTools.Tick(acceptInput && !IsCarrying && !IsPushing, wasLocked);
        bool interactionAction = acceptInput && interactor != null && Input.GetKeyDown(KeyCode.E) && interactor.TryInteract();
        if (IsShovelEquipped && !interactionAction && !IsCarrying)
        {
            if (!acceptInput) shovelTool.CancelAction();
            else
            {
                // The first click after Escape only captures the mouse.
                if (wasLocked && Input.GetMouseButtonDown(1))
                    shovelTool.TryDrop(characterCamera, transform);
                else if (wasLocked && Input.GetMouseButtonDown(0))
                    shovelTool.TryUse(characterCamera, transform);
                shovelTool.Tick(Time.deltaTime);
            }
        }
        bool isCarrying = IsCarrying;
        bool recoveryBlocked = shovelTool != null && (shovelTool.Kilograms > 0f || shovelTool.IsBusy);
        if (staminaManager != null) staminaManager.Tick(isCarrying, Time.deltaTime, recoveryBlocked);
    }

    public void ResetLook()
    {
        if (!IsPushing && Mathf.Abs(pushLookYaw) > .001f)
        {
            transform.rotation = Quaternion.Euler(0, characterCamera.transform.eulerAngles.y, 0);
            characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, 0, 0);
        }
        pushLookYaw = pushLookTarget = 0;
        targetYaw = transform.eulerAngles.y; yawVelocity = pitchVelocity = 0;
        cameraRotationX = smoothedPitch = Mathf.DeltaAngle(0, characterCamera.transform.localEulerAngles.x);
        verticalVelocity = 0;
        sprintCameraBlend = sprintCameraPhase = 0;
    }

    public bool ToggleShovel()
    {
        if (!isActiveAndEnabled || shovelTool == null || Time.timeScale <= 0f || UIPopups.BasePopupUI.HasActivePopup || UpgradeManager.BlocksGameplayInput ||
            (characterController != null && !characterController.enabled) ||
            (bicycleInteraction != null && bicycleInteraction.IsRiding) ||
            (interactor != null && interactor.PushedWheelbarrow != null)) return false;

        bool equip = IsCarrying || !IsShovelEquipped;
        if (equip)
        {
            if (bagInteraction != null && bagInteraction.IsCarrying && !bagInteraction.TryInteract()) return false;
            if (interactor != null && interactor.Items != null && interactor.Items.IsCarrying &&
                !interactor.Items.TryPlace()) return false;
            if (combatTools != null && !combatTools.TryEquip(PlayerCombatTools.Tool.Shovel)) return false;
        }
        if (!equip) shovelTool.CancelAction();
        if (heldInstrument != null) heldInstrument.gameObject.SetActive(equip);
        shovelTool.enabled = equip;
        return true;
    }

    public void ClearSprintCameraMotion()
    {
        sprintCameraBlend = sprintCameraPhase = 0;
        if (characterCamera == null || characterController == null) return;
        characterCamera.transform.localPosition = cameraRestPosition - Vector3.up * Mathf.Max(0, standingHeight - characterController.height);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, 0, 0);
    }

    private void UpdateSprintCamera(bool sprinting, float dt)
    {
        sprintCameraBlend = Mathf.Lerp(sprintCameraBlend, sprinting ? 1 : 0, 1f - Mathf.Exp(-8f * dt));
        if (sprintCameraBlend > .0001f)
            sprintCameraPhase = Mathf.Repeat(sprintCameraPhase + dt * sprintStepFrequency * Mathf.PI * 2, Mathf.PI * 2);
        Vector3 baseline = cameraRestPosition - Vector3.up * Mathf.Max(0, standingHeight - characterController.height);
        characterCamera.transform.localPosition = baseline + Vector3.up * (Mathf.Sin(sprintCameraPhase) * sprintCameraHeight * sprintCameraBlend);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch + Mathf.Cos(sprintCameraPhase) * sprintCameraPitch * sprintCameraBlend, IsPushing ? pushLookYaw : 0, 0);
    }

    private void HandleGrainScooped()
    {
        if (staminaManager != null) staminaManager.ConsumeShovelStamina(shovelTool != null ? shovelTool.WeightKg : 1f);
    }

    private void HandleMovement(bool acceptInput)
    {
        Vector2 input = acceptInput
            ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
            : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        bool pushingWheelbarrow = interactor != null && interactor.PushedWheelbarrow != null;
        float currentSpeed = pushingWheelbarrow ? (acceptInput && Input.GetKey(KeyCode.LeftShift) ? Mathf.Min(sprintSpeed, 6f) : speed) : isCrouching ? crouchSpeed :
            acceptInput && Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * currentSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (acceptInput && !pushingWheelbarrow && !isCrouching && characterController.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = jumpForce;

        verticalVelocity -= gravity * Time.deltaTime;
        velocity.y = verticalVelocity;
        if (pushingWheelbarrow)
        {
            interactor.PushedWheelbarrow.Move(input, currentSpeed, verticalVelocity, Time.deltaTime);
            targetYaw = transform.eulerAngles.y;
            UpdateSprintCamera(acceptInput && Input.GetKey(KeyCode.LeftShift) && Mathf.Abs(input.y) > .01f, Time.deltaTime);
            return;
        }
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
        Vector3 actualMovement = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up);
        bool sprinting = !pushingWheelbarrow && acceptInput && Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > .01f &&
            !isCrouching && characterController.isGrounded && actualMovement.sqrMagnitude > .25f;
        UpdateSprintCamera(sprinting, Time.deltaTime);
    }

    private void HandleCamera()
    {
        ApplyLook(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
    }

    private void ApplyLook(Vector2 input)
    {
        lookInput = input;
        if (IsPushing)
        {
            pushLookTarget = Mathf.Clamp(pushLookTarget + lookInput.x * cameraSensitivity, -65f, 65f);
            cameraRotationX = Mathf.Clamp(cameraRotationX - lookInput.y * cameraSensitivity, -50f, 65f);
            pushLookYaw = Mathf.SmoothDampAngle(pushLookYaw, pushLookTarget, ref yawVelocity, lookSmoothTime);
            smoothedPitch = Mathf.SmoothDampAngle(smoothedPitch, cameraRotationX, ref pitchVelocity, lookSmoothTime);
            characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, pushLookYaw, 0);
            return;
        }
        targetYaw += lookInput.x * cameraSensitivity;
        cameraRotationX -= lookInput.y * cameraSensitivity;
        cameraRotationX = Mathf.Clamp(cameraRotationX, cameraMinAngle, cameraMaxAngle);
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, lookSmoothTime);
        smoothedPitch = Mathf.SmoothDampAngle(smoothedPitch, cameraRotationX, ref pitchVelocity, lookSmoothTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f);
    }

    private void HandleCrouch(bool acceptInput)
    {
        bool requested = acceptInput && !IsPushing && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
        float minimumHeight = Mathf.Min(standingHeight, Mathf.Max(crouchHeight, characterController.radius * 2f));
        float nextHeight = Mathf.MoveTowards(characterController.height,
            requested ? minimumHeight : standingHeight, crouchTransitionSpeed * Time.deltaTime);
        if (nextHeight > characterController.height && !HasStandingClearance())
            nextHeight = characterController.height;

        isCrouching = requested || nextHeight < standingHeight - 0.001f;
        float heightOffset = standingHeight - nextHeight;
        characterController.height = nextHeight;
        characterController.center = standingCenter - Vector3.up * (heightOffset * 0.5f);
        characterCamera.transform.localPosition = cameraRestPosition - Vector3.up * heightOffset;
    }

    private bool HasStandingClearance()
    {
        // Query only the head space added when standing; the floor must not block uncrouching.
        float radius = Mathf.Max(characterController.radius, characterController.skinWidth);
        Vector3 bottom = standingCenter - Vector3.up * (standingHeight * 0.5f);
        Vector3 lower = transform.TransformPoint(bottom + Vector3.up * (characterController.height - radius));
        Vector3 upper = transform.TransformPoint(bottom + Vector3.up * (standingHeight - radius));
        float worldRadius = radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        int count = Physics.OverlapCapsuleNonAlloc(lower, upper, worldRadius, crouchHits,
            crouchObstructionMask, QueryTriggerInteraction.Ignore);
        if (count == crouchHits.Length)
            return false;
        for (int i = 0; i < count; i++)
            if (!crouchHits[i].transform.IsChildOf(transform))
                return false;
        return true;
    }

    private void LateUpdate()
    {
        if (heldInstrument == null || Time.deltaTime <= 0f || IsCarrying)
            return;

        idleTime += Time.deltaTime * idleFrequency * Mathf.PI * 2f;
        Vector3 idleOffset = new Vector3(Mathf.Sin(idleTime * 0.5f), Mathf.Sin(idleTime), 0f) * idleAmplitude;
        Vector2 sway = Vector2.ClampMagnitude(lookInput, 5f) * instrumentSway;
        Quaternion cameraDelta = characterCamera.transform.rotation * Quaternion.Inverse(previousPresentationCamera);
        cameraDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        float yawSpeed = Mathf.Abs(angle) < .001f || !float.IsFinite(axis.y) ? 0 : angle * axis.y / Mathf.Max(.001f, Time.deltaTime);
        previousPresentationCamera = characterCamera.transform.rotation;
        float rollTarget = Mathf.Clamp(-yawSpeed * .035f, -7f, 7f);
        turnRoll = Mathf.Lerp(turnRoll, rollTarget, 1f - Mathf.Exp(-10f * Time.deltaTime));
        if (shovelTool != null && shovelTool.isActiveAndEnabled)
        {
            shovelTool.ComposePresentation(Time.deltaTime, sway, turnRoll, idleOffset, out Vector3 offset, out Quaternion pose);
            heldInstrument.localPosition = instrumentRestPosition + offset;
            heldInstrument.localRotation = instrumentRestRotation * pose;
            return;
        }
        Quaternion rotation = instrumentRestRotation * Quaternion.Euler(sway.y, -sway.x, -sway.x * 0.5f);
        float blend = 1f - Mathf.Exp(-instrumentSmoothness * Time.deltaTime);
        Vector3 actionOffset = shovelTool != null && shovelTool.isActiveAndEnabled ? shovelTool.PositionOffset : Vector3.zero;
        if (shovelTool != null && shovelTool.IsBusy)
        {
            heldInstrument.localPosition = instrumentRestPosition + actionOffset;
            heldInstrument.localRotation = instrumentRestRotation * shovelTool.RotationOffset;
            return;
        }
        heldInstrument.localPosition = Vector3.Lerp(heldInstrument.localPosition, instrumentRestPosition + idleOffset + actionOffset, blend);
        heldInstrument.localRotation = Quaternion.Slerp(heldInstrument.localRotation, rotation, blend);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
