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
    public bool IsRiding => riding;
    bool riding, instrumentActive;
    Camera view;
    BagInteraction bags;
    Transform cameraParent;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float yaw, pitch;
    float lookIdleTime, yawReturnVelocity, pitchReturnVelocity;
    string hint = "";
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
            if (input && Input.GetKeyDown(KeyCode.E)) { TryDismount(); return true; }
            DriveRiding(input ? Input.GetAxisRaw("Vertical") : 0, input ? Input.GetAxisRaw("Horizontal") : 0,
                !input || Input.GetKey(KeyCode.Space), input && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)), Time.deltaTime);
            if(input) UpdateRidingLook(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), Time.deltaTime);
            ApplyRidingView();
            hint=motoroller != null ? "W/S Drive / Reverse   A/D Steer   Space Brake   E Get off" : "W/S Pedal   A/D Steer   Shift Faster   Space Brake   E Get off";
            return true;
        }
        hint="";
        if (!input || Time.timeScale <= 0) return false;
        if(!Physics.Raycast(view.transform.position,view.transform.forward,out var hit,interactionDistance,~0,QueryTriggerInteraction.Ignore)) return false;
        // Select the aimed vehicle, keeping the existing serialized bicycle as the default.
        var aimedVehicle = hit.collider.GetComponentInParent<BicycleController>();
        if (aimedVehicle != null) bicycle = aimedVehicle;
        if (bicycle == null || !bicycle.isActiveAndEnabled) return false;
        if (Vector3.Distance(view.transform.position,bicycle.transform.position)>interactionDistance+1) return false;
        var cargoBox = hit.collider.GetComponentInParent<BicycleCargoBox>();
        if(cargoBox != null && cargoBox.IsLoaded)
        {
            hint = "E Unload box";
            if(Input.GetKeyDown(KeyCode.E)) { TryUnloadBox(cargoBox); return true; }
            return false;
        }
        if(hit.transform.IsChildOf(bicycle.transform))
        {
            if (bags != null && bags.IsCarrying)
            {
                hint=bicycle.FreeBagCargoSlot()!=null ? "E Secure bag on " + bicycle.cargoLocationName : bicycle.cargoLocationName + " is full";
                if (Input.GetKeyDown(KeyCode.E)) { TryLoadBag(); return true; }
                return false;
            }
            // Aiming at an attached bag uses the existing bag interaction to take it back.
            if (hit.collider.GetComponentInParent<GrainContainer>() != null) return false;
            var engine = bicycle.GetComponent<MotorollerController>();
            hint=engine != null ? "E Drive " + engine.VehicleName : "E Ride bicycle";
            if(Input.GetKeyDown(KeyCode.E))return TryMount();
        }
        else
        {
            var box=hit.collider.GetComponentInParent<BicycleCargoBox>();
            if(box!=null && Vector3.Distance(box.transform.position,bicycle.transform.position)<2.5f)
            {
                hint="E Secure box on " + bicycle.cargoLocationName;
                if(Input.GetKeyDown(KeyCode.E))return box.Load(bicycle.FreeCargoSlot());
            }
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
    public bool TryUnloadBox(BicycleCargoBox box)
    {
        if(box == null || !box.IsLoaded || riding) return false;
        for(int side=0;side<2;side++)
        {
            Vector3 point=bicycle.transform.position+bicycle.transform.right*(side==0?bicycle.dismountSideDistance:-bicycle.dismountSideDistance);
            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out var ground,5,~0,QueryTriggerInteraction.Ignore) || ground.normal.y<.65f)continue;
            var bounds=box.GetComponentInChildren<Renderer>().bounds;
            Vector3 center=ground.point+Vector3.up*(bounds.extents.y+.08f);
            if(Physics.CheckBox(center,bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
            return box.Unload(center);
        }
        hint="Make room to unload";return false;
    }
    public bool TryLoadBag() => !riding && bags != null && bicycle != null && bags.TryStow(bicycle.FreeBagCargoSlot());
    public bool TryMount()
    {
        CacheReferences();
        if(view == null || player == null || player.characterController == null || riding || bicycle==null || !bicycle.isActiveAndEnabled || bicycle.IsRidden || bicycle.riderView==null || (bags!=null && bags.IsCarrying) ||
            GetComponent<PlayerInteractor>()?.PushedWheelbarrow != null || Vector3.Distance(transform.position,bicycle.transform.position)>interactionDistance) return false;
        motoroller = bicycle.GetComponent<MotorollerController>();
        pickup = bicycle.GetComponent<PickupController>();
        if (pickup != null) pickup.ResetRoadCamera();
        player.ClearSprintCameraMotion();
        cameraParent=view.transform.parent;cameraPosition=view.transform.localPosition;cameraRotation=view.transform.localRotation;
        player.characterController.enabled=false;
        if(bags!=null)bags.enabled=false;
        instrumentActive=instrument!=null && instrument.gameObject.activeSelf;
        if(instrument!=null)instrument.gameObject.SetActive(false);
        view.transform.SetParent(bicycle.riderView,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;
        yaw=pitch=lookIdleTime=yawReturnVelocity=pitchReturnVelocity=0;riding=true;bicycle.SetRidden(true);ApplyRidingView();return true;
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
        if(!clear){hint="Make room to get off";return false;}
        Restore(exit);return true;
    }
    void Restore(Vector3 exit)
    {
        riding=false;IsBoosting=false;boostExhausted=false;bicycle.SetRidden(false);
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
    void OnGUI()
    {
        if(string.IsNullOrEmpty(hint) || Cursor.lockState!=CursorLockMode.Locked)return;
        GUI.Box(new Rect(Mathf.Max(8, Screen.width/2-340),Screen.height-85,Mathf.Min(680, Screen.width-16),36),hint);
    }
}
