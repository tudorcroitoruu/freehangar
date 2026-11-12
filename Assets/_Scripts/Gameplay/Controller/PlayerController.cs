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
    [SerializeField, Range(.1f, 1f), Tooltip("Movement speed multiplier while carrying a bag.")] private float bagCarrySpeedMultiplier = .6f;
    [SerializeField, Min(.1f), Tooltip("Grain weight at which the maximum bag movement penalty applies.")] private float heavyBagWeightKg = 10f;
    [SerializeField, Min(0f)] private float jumpForce = 2.5f;
    [SerializeField, Min(0f)] private float gravity = 9.81f;

    [Header("Movement audio")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Min(.1f)] private float walkingStepDistance = 1.4f;
    [SerializeField, Min(.1f)] private float sprintingStepDistance = 1.8f;
    private float distanceSinceStep;
    private float airborneDuration;
    private int previousFootstep = -1;

    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraSensitivity = 2f;
    [SerializeField] private float cameraMinAngle = -70f;
    [SerializeField] private float cameraMaxAngle = 80f;
    [SerializeField, Min(0.001f)] private float lookSmoothTime = 0.06f;
    [SerializeField, Range(.05f, .4f)] private float grainCameraSmoothTime = .2f;
    [SerializeField, Range(.3f, .7f)] private float grainStepHeight = .55f;
    [SerializeField, Range(45f, 75f)] private float grainSlopeLimit = 65f;
    private float grainCameraOffset, grainCameraVelocity;
    private readonly RaycastHit[] grainGroundHits = new RaycastHit[32];

    [Header("Nintendo aim assist")]
    [SerializeField] private bool nintendoAimAssist = true;
    [SerializeField, Min(0f)] private float aimAssistRange = 6f;
    [SerializeField, Range(1f, 25f)] private float aimAssistAngle = 10f;
    [SerializeField, Min(0f), Tooltip("Maximum automatic turn speed in degrees per second while looking with the stick.")]
    private float aimAssistSpeed = 30f;
    private readonly Collider[] aimAssistCandidates = new Collider[128];
    private readonly RaycastHit[] aimAssistSightHits = new RaycastHit[32];

    [Header("Sprint camera motion")]
    [SerializeField, Range(0f, .04f)] private float sprintCameraHeight = .004166667f;
    [SerializeField, Range(0f, 2f)] private float sprintCameraPitch = .15625f;
    [SerializeField, Range(1f, 4f)] private float sprintStepFrequency = 2.6f;
    private float sprintCameraPhase, sprintCameraBlend;
    private bool sprintMoving, sprintExhausted;

    [Header("Crouch (hold Ctrl or C)")]
    [SerializeField, Min(0.1f)] private float crouchHeight = 1.55f;
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
    private Vector2 shovelSway;
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
        grainCameraOffset = grainCameraVelocity = 0;
        if (shovelTool != null) shovelTool.GrainScooped += HandleGrainScooped;
        verticalVelocity = 0f;
        distanceSinceStep = airborneDuration = 0f;
        SetCursorLocked();
    }

    private void OnDisable()
    {
        padSprint = false;
        if (shovelTool != null) shovelTool.GrainScooped -= HandleGrainScooped;
        if (shovelTool != null) shovelTool.CancelAction();
        SetCursorLocked();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked();
    }

    private void Update()
    {
        if (LoadingScreen.IsLoading)
        {
            lookInput = Vector2.zero;
            if (shovelTool != null) shovelTool.CancelAction();
            return;
        }
        if (Time.timeScale <= 0f || (Pause.Instance != null && Pause.Instance.paused) ||
            UpgradeManager.BlocksGameplayInput)
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
        if (FarmInput.UsingPad && Application.isFocused && !UIPopups.BasePopupUI.HasActivePopup &&
            Cursor.lockState != CursorLockMode.Locked) SetCursorLocked();
        bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked();
        else if (Input.GetMouseButtonDown(0) && !UIPopups.BasePopupUI.HasActivePopup)
            SetCursorLocked();

        bool bicycleInput = Application.isFocused && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup;
        if (bicycleInput && interactor != null && interactor.TryAdditionalInput()) return;
        if (bicycleInteraction != null && bicycleInteraction.Tick(bicycleInput)) return;

        if (!characterController.enabled || Time.deltaTime <= 0f)
            return;

        bool acceptInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup;
        lookInput = Vector2.zero;
        if (acceptInput)
            HandleCamera();

        HandleCrouch(acceptInput);
        HandleMovement(acceptInput);
        if (combatTools != null) combatTools.Tick(acceptInput && !IsPushing, wasLocked);
        bool interactionAction = acceptInput && interactor != null && FarmInput.Down("Interact") && interactor.TryInteract();
        if (IsShovelEquipped && !interactionAction && !IsCarrying)
        {
            if (!acceptInput) shovelTool.CancelAction();
            else
            {
                // The first click after Escape only captures the mouse.
                if (wasLocked && FarmInput.Down("Unload"))
                    shovelTool.TryDrop(characterCamera, transform);
                else if (wasLocked && (shovelTool.IsVacuum ? FarmInput.Held("Attack") : FarmInput.Down("Attack")))
                    shovelTool.TryUse(characterCamera, transform);
                shovelTool.Tick(Time.deltaTime);
            }
        }
        if (staminaManager != null)
        {
            var bag = bagInteraction != null ? bagInteraction.CarriedBag : null;
            staminaManager.TickBagCarry(bag != null ? bag.Kilograms : 0f, Time.deltaTime, sprintMoving);
            if (bag != null && bag.Kilograms > 0f && staminaManager.IsExhausted)
                bagInteraction.DropFromExhaustion();
        }
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

    public void ClearSprintCameraMotion()
    {
        grainCameraOffset = grainCameraVelocity = 0;
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
        if (combatTools != null) characterCamera.transform.localRotation *= combatTools.BroomCameraKick * combatTools.MeleeCameraKick;
        if (IsPushing) characterCamera.transform.localRotation *= interactor.PushedWheelbarrow.ImpactCameraKick;
    }

    private void HandleGrainScooped()
    {
        if (staminaManager != null) staminaManager.ConsumeShovelStamina(shovelTool != null ? shovelTool.WeightKg : 1f);
    }

    private bool padSprint;
    private void HandleMovement(bool acceptInput)
    {
        bool wasGrounded = characterController.isGrounded;
        Vector3 previousPosition = transform.position;
        Vector2 input = acceptInput
            ? FarmInput.Move
            : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        bool pushingWheelbarrow = interactor != null && interactor.PushedWheelbarrow != null;
        if (!acceptInput || !FarmInput.UsingPad || input.sqrMagnitude < .0001f) padSprint = false;
        else if (FarmInput.Down("Sprint")) padSprint = !padSprint;
        bool wantsSprint = acceptInput && (FarmInput.UsingPad ? padSprint : FarmInput.Held("Sprint"));
        if (!wantsSprint || staminaManager == null || staminaManager.NormalizedStamina >= .2f) sprintExhausted = false;
        if (staminaManager != null && staminaManager.IsExhausted) sprintExhausted = true;
        var carriedBag = bagInteraction != null ? bagInteraction.CarriedBag : null;
        float bagWeight = carriedBag != null ? Mathf.Clamp01(carriedBag.Kilograms / Mathf.Max(.1f, heavyBagWeightKg)) : 0f;
        bool canSprint = wantsSprint && !sprintExhausted && !isCrouching && bagWeight < .999f;
        sprintMoving = false;
        float currentSpeed = pushingWheelbarrow ? (canSprint ? Mathf.Min(sprintSpeed, 6f) : speed) : isCrouching ? crouchSpeed :
            canSprint ? Mathf.Lerp(sprintSpeed, speed, bagWeight) : speed;
        if (!pushingWheelbarrow && carriedBag != null)
        {
            currentSpeed *= Mathf.Lerp(1f, bagCarrySpeedMultiplier, bagWeight);
            if (carriedBag.Kilograms > .0001f) currentSpeed *= 1.3f;
            if (carriedBag.IsFinished) currentSpeed *= 1.3f;
        }
        if(!pushingWheelbarrow) currentSpeed *= PlayerPerks.MobilityMultiplier;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * currentSpeed;
        bool onGrain = HasGrainSupport(transform.position) ||
            (velocity.sqrMagnitude > .001f && HasGrainSupport(transform.position + velocity.normalized * .45f));

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        bool jumped = acceptInput && !pushingWheelbarrow && !isCrouching && wasGrounded &&
            FarmInput.Down("Jump") && staminaManager != null && staminaManager.TryConsumeJumpStamina();
        if (jumped)
        {
            verticalVelocity = jumpForce;
            PlayMovementClip(jumpClip);
        }

        verticalVelocity -= gravity * Time.deltaTime;
        float verticalSpeedBeforeMove = verticalVelocity;
        velocity.y = verticalVelocity;
        if (pushingWheelbarrow)
        {
            interactor.PushedWheelbarrow.Move(input, currentSpeed, verticalVelocity, Time.deltaTime);
            UpdateMovementAudio(wasGrounded, jumped, verticalSpeedBeforeMove, previousPosition, input, canSprint);
            targetYaw = transform.eulerAngles.y;
            sprintMoving = canSprint && Mathf.Abs(input.y) > .01f && characterController.velocity.sqrMagnitude > .25f;
            UpdateSprintCamera(sprintMoving, Time.deltaTime);
            SmoothGrainCamera(previousPosition.y, onGrain, jumped);
            return;
        }
        float normalStep = characterController.stepOffset;
        float normalSlope = characterController.slopeLimit;
        if (onGrain && !jumped && verticalVelocity <= 0)
        {
            characterController.stepOffset = Mathf.Min(characterController.height * .45f, Mathf.Max(normalStep, grainStepHeight));
            characterController.slopeLimit = Mathf.Max(normalSlope, grainSlopeLimit);
        }
        CollisionFlags collisions = characterController.Move(velocity * Time.deltaTime);
        characterController.stepOffset = normalStep;
        characterController.slopeLimit = normalSlope;
        UpdateMovementAudio(wasGrounded, jumped, verticalSpeedBeforeMove, previousPosition, input, canSprint);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -2f;
        Vector3 actualMovement = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up);
        sprintMoving = canSprint && input.sqrMagnitude > .01f && characterController.isGrounded && actualMovement.sqrMagnitude > .25f;
        UpdateSprintCamera(sprintMoving, Time.deltaTime);
        SmoothGrainCamera(previousPosition.y, onGrain, jumped);
    }

    private bool HasGrainSupport(Vector3 position)
    {
        float feet = characterController.bounds.min.y;
        Vector3 origin = new Vector3(position.x, feet + .7f, position.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, grainGroundHits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        if (count == grainGroundHits.Length) return false;
        Collider nearest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = grainGroundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
            nearest = hit.collider; distance = hit.distance;
        }
        return nearest != null && nearest.GetComponentInParent<GrainPile>() != null;
    }

    private void SmoothGrainCamera(float previousHeight, bool onGrain, bool jumped)
    {
        float rise = transform.position.y - previousHeight;
        if (jumped || Mathf.Abs(rise) > 1f)
        { grainCameraOffset = grainCameraVelocity = 0; return; }
        if (onGrain && verticalVelocity <= 0)
            grainCameraOffset = Mathf.Clamp(grainCameraOffset - rise, -.3f, .3f);
        grainCameraOffset = Mathf.SmoothDamp(grainCameraOffset, 0, ref grainCameraVelocity,
            grainCameraSmoothTime, Mathf.Infinity, Time.deltaTime);
        // Only smooth presentation. The capsule still contacts the real grain surface.
        characterCamera.transform.position += Vector3.up * grainCameraOffset;
    }

    private void UpdateMovementAudio(bool wasGrounded, bool jumped, float verticalSpeedBeforeMove,
        Vector3 previousPosition, Vector2 input, bool sprinting)
    {
        bool grounded = characterController.isGrounded;
        if (!grounded)
        {
            airborneDuration += Time.deltaTime;
            distanceSinceStep = 0f;
            return;
        }

        if (!wasGrounded && !jumped && airborneDuration >= .15f && verticalSpeedBeforeMove < -1f)
            PlayMovementClip(landClip);
        bool landed = !wasGrounded && airborneDuration >= .15f;
        airborneDuration = 0f;
        if (landed)
        {
            distanceSinceStep = 0f;
            return;
        }

        float horizontalDistance = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
        if (input.sqrMagnitude < .01f || horizontalDistance < .001f)
        {
            distanceSinceStep = 0f;
            return;
        }
        distanceSinceStep += horizontalDistance;
        float stepDistance = sprinting ? sprintingStepDistance : walkingStepDistance;
        if (distanceSinceStep < stepDistance) return;
        distanceSinceStep %= stepDistance;
        if (footstepClips == null || footstepClips.Length == 0 || movementAudioSource == null) return;
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == previousFootstep)
            index = (index + Random.Range(1, footstepClips.Length)) % footstepClips.Length;
        previousFootstep = index;
        PlayMovementClip(footstepClips[index]);
    }

    private void PlayMovementClip(AudioClip clip)
    {
        if (movementAudioSource != null && clip != null)
            movementAudioSource.PlayOneShot(clip);
    }

    public void PlayCutsceneFootstep(AudioSource source)
    {
        if (source == null || footstepClips == null || footstepClips.Length == 0 ||
            WorldSaveRuntime.IsSeeking || WorldSaveRuntime.IsRestoring) return;
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == previousFootstep)
            index = (index + Random.Range(1, footstepClips.Length)) % footstepClips.Length;
        previousFootstep = index;
        if (movementAudioSource != null)
        {
            source.outputAudioMixerGroup = SoundManager.CinematicOutput;
            source.volume = movementAudioSource.volume;
        }
        if (footstepClips[index] != null) source.PlayOneShot(footstepClips[index]);
    }

    private void HandleCamera()
    {
        ApplyLook(FarmInput.Look);
    }

    private void ApplyLook(Vector2 input)
    {
        lookInput = input * Mathf.Clamp(SaveSystem.settingsData?.aimSensitivity ?? 1f, .1f, 1.5f);
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
        ApplyNintendoAimAssist();
        cameraRotationX = Mathf.Clamp(cameraRotationX, cameraMinAngle, cameraMaxAngle);
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, lookSmoothTime);
        smoothedPitch = Mathf.SmoothDampAngle(smoothedPitch, cameraRotationX, ref pitchVelocity, lookSmoothTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        characterCamera.transform.localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f);
    }

    private void ApplyNintendoAimAssist()
    {
        if (!nintendoAimAssist || !FarmInput.LookUsesPad ||
            !MenuPadInput.UsesNintendoLayout(FarmInput.ActivePad) || lookInput.sqrMagnitude <= .000001f ||
            aimAssistRange <= 0f || aimAssistSpeed <= 0f) return;

        Vector3 origin = characterCamera.transform.position;
        Vector3 forward = Quaternion.Euler(cameraRotationX, targetYaw, 0f) * Vector3.forward;
        int count = Physics.OverlapSphereNonAlloc(origin, aimAssistRange, aimAssistCandidates,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        // A full buffer may omit a closer target; skip assistance rather than pick arbitrarily.
        if (count == aimAssistCandidates.Length) return;
        float bestAngle = aimAssistAngle;
        Vector3 bestDirection = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = aimAssistCandidates[i];
            if (candidate.transform.IsChildOf(transform)) continue;
            Vector3 direction = candidate.bounds.center - origin;
            float distance = direction.magnitude;
            if (distance < .01f || distance > aimAssistRange) continue;
            float angle = Vector3.Angle(forward, direction);
            if (angle >= bestAngle) continue;

            var chicken = candidate.GetComponentInParent<FarmChicken>();
            var pest = candidate.GetComponentInParent<GrainPest>();
            Transform enemy;
            if (chicken != null && chicken.isActiveAndEnabled && !chicken.IsDead)
                enemy = chicken.transform;
            else if (pest != null && pest.isActiveAndEnabled && pest.Target != null)
                enemy = pest.transform;
            else continue;

            int hits = Physics.RaycastNonAlloc(origin, direction / distance, aimAssistSightHits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool blocked = hits == aimAssistSightHits.Length;
            for (int j = 0; j < hits && !blocked; j++)
            {
                Transform obstacle = aimAssistSightHits[j].collider.transform;
                if (!obstacle.IsChildOf(transform) && !obstacle.IsChildOf(enemy)) blocked = true;
            }
            if (blocked) continue;
            bestAngle = angle;
            bestDirection = direction;
        }
        if (bestDirection.sqrMagnitude <= .0001f) return;

        float yaw = Mathf.Atan2(bestDirection.x, bestDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(bestDirection.y,
            new Vector2(bestDirection.x, bestDirection.z).magnitude) * Mathf.Rad2Deg;
        Vector2 correction = new Vector2(Mathf.DeltaAngle(targetYaw, yaw),
            Mathf.Clamp(pitch, cameraMinAngle, cameraMaxAngle) - cameraRotationX);
        // Ease into the target and cap the total correction so deliberate stick turns can escape.
        correction *= 1f - Mathf.Exp(-5f * Time.deltaTime);
        correction = Vector2.ClampMagnitude(correction, aimAssistSpeed * Time.deltaTime);
        targetYaw += correction.x;
        cameraRotationX += correction.y;
    }

    private void HandleCrouch(bool acceptInput)
    {
        bool requested = acceptInput && !IsPushing && FarmInput.Held("Crouch");
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
        {
            shovelSway = Vector2.zero;
            turnRoll = 0f;
            if (characterCamera != null) previousPresentationCamera = characterCamera.transform.rotation;
            return;
        }

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
            // Input is a per-frame mouse delta (or an already time-scaled stick).
            // Normalize to the authored 60 Hz feel, then smooth only cosmetic sway.
            // Smoothing the final pose would delay blade contact and pouring too.
            Vector2 targetSway = Vector2.ClampMagnitude(lookInput / (60f * Time.deltaTime), 5f) * instrumentSway;
            float swayBlend = instrumentSmoothness > 0f
                ? 1f - Mathf.Exp(-instrumentSmoothness * Time.deltaTime) : 1f;
            shovelSway = Vector2.Lerp(shovelSway, targetSway, swayBlend);
            shovelTool.ComposePresentation(Time.deltaTime, shovelSway, turnRoll, idleOffset, out Vector3 offset, out Quaternion pose);
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

    private static void SetCursorLocked()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
