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
    private float verticalVelocity;
    private float cameraRotationX;
    private float targetYaw;
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

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        characterCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        bicycleInteraction = GetComponent<PlayerBicycleInteraction>();
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
        bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(false);
        else if (Input.GetMouseButtonDown(0) && (bicycleInteraction == null || !bicycleInteraction.BlocksCursorCapture))
            SetCursorLocked(true);

        bool bicycleInput = Application.isFocused && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked;
        if (bicycleInteraction != null && bicycleInteraction.Tick(bicycleInput)) return;

        if (!characterController.enabled || Time.deltaTime <= 0f)
            return;

        bool acceptInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        lookInput = Vector2.zero;
        if (acceptInput)
            HandleCamera();

        HandleCrouch(acceptInput);
        HandleMovement(acceptInput);
        bool bagAction = acceptInput && bagInteraction != null && Input.GetKeyDown(KeyCode.E) && bagInteraction.TryInteract();
        if (shovelTool != null && shovelTool.isActiveAndEnabled && !bagAction)
        {
            if (!acceptInput) shovelTool.CancelAction();
            else
            {
                // The first click after Escape only captures the mouse.
                if (wasLocked && Input.GetMouseButtonDown(0))
                    shovelTool.TryUse(characterCamera, transform);
                shovelTool.Tick(Time.deltaTime);
            }
        }
        bool isCarrying = bagInteraction != null && bagInteraction.IsCarrying;
        bool recoveryBlocked = shovelTool != null && (shovelTool.Kilograms > 0f || shovelTool.IsBusy);
        if (staminaManager != null) staminaManager.Tick(isCarrying, Time.deltaTime, recoveryBlocked);
    }

    public void ResetLook()
    {
        targetYaw = transform.eulerAngles.y; yawVelocity = pitchVelocity = 0;
        cameraRotationX = smoothedPitch = Mathf.DeltaAngle(0, characterCamera.transform.localEulerAngles.x);
        verticalVelocity = 0;
    }

    private void HandleGrainScooped()
    {
        if (staminaManager != null) staminaManager.ConsumeShovelStamina();
    }

    private void HandleMovement(bool acceptInput)
    {
        Vector2 input = acceptInput
            ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
            : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        float currentSpeed = isCrouching ? crouchSpeed :
            acceptInput && Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * currentSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (acceptInput && !isCrouching && characterController.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = jumpForce;

        verticalVelocity -= gravity * Time.deltaTime;
        velocity.y = verticalVelocity;
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
    }

    private void HandleCamera()
    {
        lookInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
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
        bool requested = acceptInput && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
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
        if (heldInstrument == null || Time.deltaTime <= 0f || (bagInteraction != null && bagInteraction.IsCarrying))
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
