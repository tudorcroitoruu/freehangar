using UnityEngine;

/// <summary>Mounting, first-person view and rear rack interaction for the existing player.</summary>
[DisallowMultipleComponent]
public sealed class PlayerBicycleInteraction : MonoBehaviour
{
    public PlayerController player;
    public BicycleController bicycle;
    public Transform instrument;
    public float interactionDistance = 2.5f;
    [Min(0)] public float cameraReturnDelay = 3f;
    [Min(.05f)] public float cameraReturnSmoothTime = .6f;
    [Min(0)] public float boostStaminaPerSecond = 12f;
    public bool IsBoosting { get; private set; }
    StaminaManager stamina;
    bool boostExhausted;
    MotorollerController motoroller;
    PickupController pickup;
    VehicleReset vehicleReset;
    public bool IsRiding => riding;
    bool riding, instrumentActive;
    Camera view;
    BagInteraction bags;
    Transform cameraParent;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float yaw, pitch;
    float lookIdleTime, yawReturnVelocity, pitchReturnVelocity;
    readonly Collider[] hits = new Collider[32];
    void Awake() => CacheReferences();
    void CacheReferences()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (view == null && player != null) view = player.characterCamera != null ? player.characterCamera : player.GetComponentInChildren<Camera>();
        if (bags == null) bags = GetComponent<BagInteraction>();
    }
    public bool Tick(bool input)
    {
        if (!isActiveAndEnabled || view == null) return false;
        if (riding)
        {
            var forklift = bicycle != null ? bicycle.GetComponent<ForkliftController>() : null;
            if (input && FarmInput.Down("Interact")) { TryDismount(); return true; }
            if (input && forklift != null && FarmInput.Down("Cargo")) forklift.DropCargo();
            if (forklift != null && forklift.IsTransferring) input = false;
            if (vehicleReset != null) vehicleReset.CancelHold();
            DriveRiding(input ? FarmInput.Move.y : 0,
                input ? FarmInput.Move.x : 0,
                !input || FarmInput.Held("Jump"),
                input && (FarmInput.Held("Sprint") || Input.GetKey(KeyCode.RightShift)), Time.deltaTime);
            if (input) { var look = FarmInput.Look; UpdateRidingLook(look.x, look.y, Time.deltaTime); }
            ApplyRidingView();
            return true;
        }

        if (player != null && player.IsCarrying && (bags == null || !bags.IsCarrying)) return false;
        if (!input || Time.timeScale <= 0) return false;
        if(!Physics.Raycast(view.transform.position,view.transform.forward,out var hit,interactionDistance,~0,QueryTriggerInteraction.Ignore)) return false;
        // Select the aimed vehicle, keeping the existing serialized bicycle as the default.
        var aimedVehicle = VehicleCargoOwner.Resolve(hit.collider);
        if (aimedVehicle != null) bicycle = aimedVehicle;
        if (bicycle == null || !bicycle.isActiveAndEnabled) return false;
        if ((bags == null || !bags.IsCarrying || aimedVehicle != bicycle) && Vector3.Distance(view.transform.position,bicycle.transform.position)>interactionDistance+1) return false;
        if(hit.transform.IsChildOf(bicycle.transform) || aimedVehicle == bicycle)
        {
            if (bags != null && bags.IsCarrying)
            {
                if (FarmInput.Down("Interact")) { TryLoadBag(); return true; }
                return false;
            }
            // Aiming at an attached bag uses the existing bag interaction to take it back.
            if (hit.collider.GetComponentInParent<GrainContainer>() != null) return false;
            // Body actions and their key prompts are handled by PlayerInteractor.
            return false;
        }
        return false;
    }
    public void DriveRiding(float throttle, float turn, bool brake, bool requestBoost, float dt)
    {
        if (!riding || dt <= 0 || !float.IsFinite(dt)) return;
        if (stamina == null) stamina = player.Stamina;
        if (motoroller != null)
        {
            motoroller.Drive(throttle, turn, brake, dt);
            if (pickup != null) pickup.UpdateRoadCamera(dt);
            IsBoosting = false;
            if (stamina != null) stamina.Tick(false, dt, false);
            return;
        }
        if (stamina != null && boostExhausted && (!requestBoost || stamina.NormalizedStamina >= .2f)) boostExhausted = false;
        bool boost = requestBoost && throttle > .01f && !brake &&
            stamina != null && stamina.isActiveAndEnabled && !stamina.IsExhausted && !boostExhausted;
        bicycle.Drive(throttle, turn, brake, dt, boost);
        IsBoosting = boost && bicycle.Speed > .2f;
        if (stamina != null)
        {
            stamina.Tick(IsBoosting, dt, false, boostStaminaPerSecond);
            if (stamina.IsExhausted) { boostExhausted = true; IsBoosting = false; }
        }
    }
    public void UpdateRidingLook(float mouseX, float mouseY, float dt)
    {
        if (!riding || dt <= 0 || !float.IsFinite(dt) || !float.IsFinite(mouseX) || !float.IsFinite(mouseY)) return;
        if (Mathf.Abs(mouseX) > .0001f || Mathf.Abs(mouseY) > .0001f)
        {
            lookIdleTime = yawReturnVelocity = pitchReturnVelocity = 0;
            yaw = Mathf.Clamp(yaw + mouseX * 2, -85, 85);
            pitch = Mathf.Clamp(pitch - mouseY * 2, -65, 65);
        }
        else
        {
            lookIdleTime += dt;
            if (lookIdleTime > cameraReturnDelay)
            {
                float returnDt = Mathf.Min(dt, lookIdleTime - cameraReturnDelay);
                yaw = Mathf.SmoothDampAngle(yaw, 0, ref yawReturnVelocity, Mathf.Max(.05f, cameraReturnSmoothTime), Mathf.Infinity, returnDt);
                pitch = Mathf.SmoothDampAngle(pitch, 0, ref pitchReturnVelocity, Mathf.Max(.05f, cameraReturnSmoothTime), Mathf.Infinity, returnDt);
            }
        }
        ApplyRidingView();
    }
    void ApplyRidingView()
    {
        Vector3 roadOffset = pickup != null ? pickup.RoadCameraOffset : Vector3.zero;
        Vector3 roadAngles = pickup != null ? pickup.RoadCameraAngles : Vector3.zero;
        view.transform.localPosition = bicycle.RidingCameraOffset + roadOffset;
        view.transform.localRotation = Quaternion.Euler(pitch + bicycle.RidingCameraPitch + bicycle.PedalingCameraPitch, yaw, 0) * Quaternion.Euler(roadAngles);
    }

    public Vector2 SavedLook => new Vector2(yaw, pitch);
    public void RestoreSavedLook(Vector2 look)
    {
        if (!riding) return;
        yaw = Mathf.Clamp(look.x, -85, 85); pitch = Mathf.Clamp(look.y, -65, 65);
        ApplyRidingView();
    }
    public bool TryLoadBag() => !riding && bags != null && bicycle != null && bags.TryStow(bicycle.FreeBagCargoSlot(bags.CarriedBag));
    public bool TryMount()
    {
        CacheReferences();
        if (bicycle != null && bicycle.TryGetComponent<VehicleUpgrade>(out var upgrades) &&
            upgrades.IsConfigured && !upgrades.IsUnlocked) return false;
        if(view == null || player == null || player.characterController == null || riding || bicycle==null || !bicycle.isActiveAndEnabled || bicycle.IsRidden || bicycle.riderView==null || player.IsCarrying ||
            GetComponent<PlayerInteractor>()?.PushedWheelbarrow != null || Vector3.Distance(transform.position,bicycle.transform.position)>interactionDistance) return false;
        motoroller = bicycle.GetComponent<MotorollerController>();
        pickup = bicycle.GetComponent<PickupController>();
        vehicleReset = bicycle.GetComponent<VehicleReset>();
        if (pickup != null) pickup.ResetRoadCamera();
        player.ClearSprintCameraMotion();
        cameraParent=view.transform.parent;cameraPosition=view.transform.localPosition;cameraRotation=view.transform.localRotation;
        player.characterController.enabled=false;
        if(bags!=null)bags.enabled=false;
        instrumentActive=instrument!=null && instrument.gameObject.activeSelf;
        if(instrument!=null)instrument.gameObject.SetActive(false);
        view.transform.SetParent(bicycle.riderView,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;
        yaw=pitch=lookIdleTime=yawReturnVelocity=pitchReturnVelocity=0;riding=true;bicycle.SetRidden(true);ApplyRidingView();
        GameplayCanvas.Instance?.SetDrivingHints(true, bicycle.GetComponent<ForkliftController>() != null);
        return true;
    }
    public bool TryDismount()
    {
        if(!riding)return false;
        Vector3 exit=default;bool clear=false;
        for(int side=0;side<4;side++)
        {
            Vector3 offset=side<2 ? bicycle.transform.right*(side==0?-bicycle.dismountSideDistance:bicycle.dismountSideDistance) : bicycle.transform.forward*(side==2?-bicycle.dismountLongitudinalDistance:bicycle.dismountLongitudinalDistance);
            Vector3 point=bicycle.transform.position+offset;
            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out var ground,5,~0,QueryTriggerInteraction.Ignore) || ground.normal.y<.65f)continue;
            Vector3 bottom=ground.point+Vector3.up*.35f,top=ground.point+Vector3.up*1.55f;
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,.3f,hits,~0,QueryTriggerInteraction.Ignore);
            clear=count<hits.Length;
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(transform))clear=false;
            if(clear){exit=ground.point+Vector3.up*.05f;break;}
        }
        if (!clear)
        {
            GameplayCanvas.Instance?.ShowWarning(WarningType.BlockVehicleExit);
            return false;
        }
        Restore(exit);return true;
    }
    void Restore(Vector3 exit)
    {
        if (vehicleReset != null) vehicleReset.CancelHold();
        riding=false;IsBoosting=false;boostExhausted=false;bicycle.SetRidden(false);
        GameplayCanvas.Instance?.SetDrivingHints(false);
        if (pickup != null) pickup.ResetRoadCamera();
        view.transform.SetParent(cameraParent,false);view.transform.localPosition=cameraPosition;view.transform.localRotation=cameraRotation;
        transform.SetPositionAndRotation(exit,Quaternion.Euler(0,bicycle.transform.eulerAngles.y+yaw,0));
        player.ResetLook();player.characterController.enabled=true;
        if(bags!=null)bags.enabled=true;
        if(instrument!=null)instrument.gameObject.SetActive(instrumentActive);
    }
    bool applicationQuitting;
    void OnApplicationQuit() => applicationQuitting = true;
    void OnDisable() { if(!applicationQuitting && riding) { if(!TryDismount())Restore(bicycle.transform.position+Vector3.up*1.5f); } }
}
