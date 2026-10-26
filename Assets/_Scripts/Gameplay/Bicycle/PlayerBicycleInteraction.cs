using UnityEngine;

/// <summary>Mounting, first-person view and rear rack interaction for the existing player.</summary>
[DisallowMultipleComponent]
public sealed class PlayerBicycleInteraction : MonoBehaviour
{
    public PlayerController player;
    public BicycleController bicycle;
    public Transform instrument;
    public float interactionDistance = 2.5f;
    [Min(0)] public float boostStaminaPerSecond = 12f;
    public bool IsBoosting { get; private set; }
    StaminaManager stamina;
    bool boostExhausted;
    public bool IsRiding => riding;
    public bool IsAdjustingRidingPosition { get; private set; }
    Vector3 adjustmentStartOffset;
    float adjustmentStartPitch;
    Rect AdjustButtonRect => new Rect(Screen.width - 230, 20, 210, 36);
    public bool BlocksCursorCapture => riding && (IsAdjustingRidingPosition ||
        (Cursor.lockState != CursorLockMode.Locked && AdjustButtonRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))));
    bool riding, instrumentActive;
    Camera view;
    BagInteraction bags;
    Transform cameraParent;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float yaw, pitch;
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
        if (!isActiveAndEnabled || bicycle == null || view == null) return false;
        if (riding)
        {
            if (Application.isFocused && Time.timeScale > 0 && Input.GetKeyDown(KeyCode.V))
            {
                if (IsAdjustingRidingPosition) EndRidingPositionAdjustment(true);
                else BeginRidingPositionAdjustment();
            }
            if (IsAdjustingRidingPosition)
            {
                DriveRiding(0, 0, true, false, Time.deltaTime);
                ApplyRidingView();
                hint = "Adjust your riding view, then Save & ride";
                return true;
            }
            if (input && Input.GetKeyDown(KeyCode.E)) { TryDismount(); return true; }
            DriveRiding(input ? Input.GetAxisRaw("Vertical") : 0, input ? Input.GetAxisRaw("Horizontal") : 0,
                !input || Input.GetKey(KeyCode.Space), input && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)), Time.deltaTime);
            if(input) { yaw = Mathf.Clamp(yaw + Input.GetAxisRaw("Mouse X")*2,-85,85); pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-65,65); }
            ApplyRidingView();
            hint="W/S Pedal   A/D Steer   Shift Faster   Space Brake   E Get off   V Adjust view";
            return true;
        }
        hint="";
        if (!input || Time.timeScale <= 0) return false;
        if (Vector3.Distance(view.transform.position,bicycle.transform.position)>interactionDistance+1) return false;
        if(!Physics.Raycast(view.transform.position,view.transform.forward,out var hit,interactionDistance,~0,QueryTriggerInteraction.Ignore)) return false;
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
                hint=bicycle.FreeCargoSlot()!=null ? "E Secure bag on rear rack" : "Rear rack is full";
                if (Input.GetKeyDown(KeyCode.E)) { TryLoadBag(); return true; }
                return false;
            }
            // Aiming at an attached bag uses the existing bag interaction to take it back.
            if (hit.collider.GetComponentInParent<GrainContainer>() != null) return false;
            hint="E Ride bicycle";
            if(Input.GetKeyDown(KeyCode.E))return TryMount();
        }
        else
        {
            var box=hit.collider.GetComponentInParent<BicycleCargoBox>();
            if(box!=null && Vector3.Distance(box.transform.position,bicycle.transform.position)<2.5f)
            {
                hint="E Secure box on rear rack";
                if(Input.GetKeyDown(KeyCode.E))return box.Load(bicycle.FreeCargoSlot());
            }
        }
        return false;
    }
    public void DriveRiding(float throttle, float turn, bool brake, bool requestBoost, float dt)
    {
        if (!riding || dt <= 0 || !float.IsFinite(dt)) return;
        if (stamina == null) stamina = player.GetComponent<StaminaManager>();
        if (stamina != null && boostExhausted && (!requestBoost || stamina.NormalizedStamina >= .2f)) boostExhausted = false;
        bool boost = requestBoost && throttle > .01f && !brake && !IsAdjustingRidingPosition &&
            stamina != null && stamina.isActiveAndEnabled && !stamina.IsExhausted && !boostExhausted;
        bicycle.Drive(throttle, turn, brake, dt, boost);
        IsBoosting = boost && bicycle.Speed > .2f;
        if (stamina != null)
        {
            stamina.Tick(IsBoosting, dt, false, boostStaminaPerSecond);
            if (stamina.IsExhausted) { boostExhausted = true; IsBoosting = false; }
        }
    }
    void ApplyRidingView()
    {
        view.transform.localPosition = bicycle.RidingCameraOffset;
        view.transform.localRotation = Quaternion.Euler(pitch + bicycle.RidingCameraPitch, yaw, 0);
    }
    public bool BeginRidingPositionAdjustment()
    {
        if (!riding || IsAdjustingRidingPosition) return false;
        adjustmentStartOffset = bicycle.RidingCameraOffset; adjustmentStartPitch = bicycle.RidingCameraPitch;
        IsAdjustingRidingPosition = true; yaw = pitch = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        ApplyRidingView(); return true;
    }
    public void EndRidingPositionAdjustment(bool save)
    {
        if (!IsAdjustingRidingPosition) return;
        if (save) bicycle.SaveRidingCamera();
        else bicycle.SetRidingCamera(adjustmentStartOffset, adjustmentStartPitch);
        IsAdjustingRidingPosition = false;
        ApplyRidingView();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    public bool TryUnloadBox(BicycleCargoBox box)
    {
        if(box == null || !box.IsLoaded || riding) return false;
        for(int side=0;side<2;side++)
        {
            Vector3 point=bicycle.transform.position+bicycle.transform.right*(side==0?1.1f:-1.1f);
            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out var ground,5,~0,QueryTriggerInteraction.Ignore) || ground.normal.y<.65f)continue;
            var bounds=box.GetComponentInChildren<Renderer>().bounds;
            Vector3 center=ground.point+Vector3.up*(bounds.extents.y+.08f);
            if(Physics.CheckBox(center,bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
            return box.Unload(center);
        }
        hint="Make room to unload";return false;
    }
    public bool TryLoadBag() => !riding && bags != null && bags.TryStow(bicycle.FreeCargoSlot());
    public bool TryMount()
    {
        CacheReferences();
        if(view == null || player == null || player.characterController == null || riding || bicycle==null || (bags!=null && bags.IsCarrying) || Vector3.Distance(transform.position,bicycle.transform.position)>interactionDistance) return false;
        cameraParent=view.transform.parent;cameraPosition=view.transform.localPosition;cameraRotation=view.transform.localRotation;
        player.characterController.enabled=false;
        if(bags!=null)bags.enabled=false;
        instrumentActive=instrument!=null && instrument.gameObject.activeSelf;
        if(instrument!=null)instrument.gameObject.SetActive(false);
        view.transform.SetParent(bicycle.riderView,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;
        yaw=pitch=0;riding=true;bicycle.SetRidden(true);ApplyRidingView();return true;
    }
    public bool TryDismount()
    {
        if(!riding)return false;
        Vector3 exit=default;bool clear=false;
        for(int side=0;side<4;side++)
        {
            Vector3 offset=side<2 ? bicycle.transform.right*(side==0?-1.1f:1.1f) : bicycle.transform.forward*(side==2?-1.5f:1.5f);
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
        if (IsAdjustingRidingPosition) EndRidingPositionAdjustment(false);
        riding=false;IsBoosting=false;boostExhausted=false;bicycle.SetRidden(false);
        view.transform.SetParent(cameraParent,false);view.transform.localPosition=cameraPosition;view.transform.localRotation=cameraRotation;
        transform.SetPositionAndRotation(exit,Quaternion.Euler(0,bicycle.transform.eulerAngles.y+yaw,0));
        player.ResetLook();player.characterController.enabled=true;
        if(bags!=null)bags.enabled=true;
        if(instrument!=null)instrument.gameObject.SetActive(instrumentActive);
    }
    bool applicationQuitting;
    void OnApplicationQuit() => applicationQuitting = true;
    void OnDisable() { if(!applicationQuitting && riding) { if(!TryDismount())Restore(bicycle.transform.position+Vector3.up*1.5f); } }
    float ViewSlider(string label, float value, float min, float max)
    {
        GUILayout.Label(label + "   " + value.ToString("0.00"));
        return GUILayout.HorizontalSlider(value, min, max);
    }
    void OnGUI()
    {
        if (riding && !IsAdjustingRidingPosition && GUI.Button(AdjustButtonRect, "Adjust riding position [V]"))
        {
            BeginRidingPositionAdjustment(); GUIUtility.ExitGUI();
        }
        if (riding && IsAdjustingRidingPosition)
        {
            float width = Mathf.Min(360, Screen.width - 24);
            GUILayout.BeginArea(new Rect(Screen.width - width - 12, 16, width, 355), GUI.skin.box);
            GUILayout.Label("Riding position — live camera preview");
            Vector3 offset = bicycle.RidingCameraOffset;
            offset.x = ViewSlider("Sideways (m)", offset.x, -.5f, .5f);
            offset.y = ViewSlider("Height (m)", offset.y, -.5f, .7f);
            offset.z = ViewSlider("Forward / back (m)", offset.z, -.6f, .6f);
            float angle = ViewSlider("Look up / down (degrees)", bicycle.RidingCameraPitch, -35, 35);
            bicycle.SetRidingCamera(offset, angle);
            if (GUILayout.Button("Reset view")) bicycle.SetRidingCamera(Vector3.zero, 0);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & ride")) { EndRidingPositionAdjustment(true); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("Cancel")) { EndRidingPositionAdjustment(false); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Bike brakes while adjusting. V also saves and closes.");
            GUILayout.EndArea();
        }
        if(string.IsNullOrEmpty(hint) || Cursor.lockState!=CursorLockMode.Locked)return;
        GUI.Box(new Rect(Mathf.Max(8, Screen.width/2-340),Screen.height-85,Mathf.Min(680, Screen.width-16),36),hint);
    }
}
