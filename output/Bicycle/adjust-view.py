from pathlib import Path
p=Path('Assets/_Scripts/Gameplay/Bicycle/BicycleController.cs');s=p.read_text()
s=s.replace('    public bool IsRidden', '''    [Header("Steering")]
    [Range(.02f, .5f)] public float steeringSmoothTime = .14f;
    [Range(5f, 45f)] public float maximumSteeringAngle = 30f;
    [Header("Riding camera (relative to rider view)")]
    [SerializeField] private Vector3 ridingCameraOffset;
    [SerializeField, Range(-35, 35)] private float ridingCameraPitch;
    public Vector3 RidingCameraOffset => ridingCameraOffset;
    public float RidingCameraPitch => ridingCameraPitch;
    public float CurrentSteeringAngle => currentSteering;
    float currentSteering, steeringVelocity;
    string CameraSettingsKey => "Bicycle.View." + gameObject.scene.name + "." + gameObject.name;
    [System.Serializable] sealed class ViewSettings { public Vector3 offset; public float pitch; }
    public void SetRidingCamera(Vector3 offset, float pitch)
    {
        ridingCameraOffset = new Vector3(Mathf.Clamp(offset.x, -.5f, .5f), Mathf.Clamp(offset.y, -.5f, .7f), Mathf.Clamp(offset.z, -.6f, .6f));
        ridingCameraPitch = Mathf.Clamp(pitch, -35, 35);
    }
    public void SaveRidingCamera()
    {
        PlayerPrefs.SetString(CameraSettingsKey, JsonUtility.ToJson(new ViewSettings { offset = ridingCameraOffset, pitch = ridingCameraPitch }));
        PlayerPrefs.Save();
    }
    public void ClearSavedRidingCamera() { PlayerPrefs.DeleteKey(CameraSettingsKey); PlayerPrefs.Save(); }
    void LoadRidingCamera()
    {
        if (!PlayerPrefs.HasKey(CameraSettingsKey)) return;
        try { var settings = JsonUtility.FromJson<ViewSettings>(PlayerPrefs.GetString(CameraSettingsKey)); if (settings != null) SetRidingCamera(settings.offset, settings.pitch); }
        catch (System.ArgumentException) { /* Ignore a malformed local preference and retain the authored view. */ }
    }
    public bool IsRidden''',1)
s=s.replace('void Awake() => Initialize();','void Awake() { Initialize(); LoadRidingCamera(); }')
s=s.replace('IsRidden = value; Speed = 0; fallSpeed = 0;','IsRidden = value; Speed = 0; fallSpeed = 0; steeringVelocity = 0;')
s=s.replace('float steer = Mathf.Clamp(turn, -1, 1) * 30f;','''float targetSteering = Mathf.Clamp(turn, -1, 1) * maximumSteeringAngle;
        currentSteering = Mathf.SmoothDampAngle(currentSteering, targetSteering, ref steeringVelocity,
            Mathf.Max(.02f, steeringSmoothTime), Mathf.Infinity, dt);
        float steer = currentSteering;''')
p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/PlayerBicycleInteraction.cs');s=p.read_text()
s=s.replace('    bool riding, instrumentActive;','''    public bool IsAdjustingRidingPosition { get; private set; }
    Vector3 adjustmentStartOffset;
    float adjustmentStartPitch;
    Rect AdjustButtonRect => new Rect(Screen.width - 230, 20, 210, 36);
    public bool BlocksCursorCapture => riding && (IsAdjustingRidingPosition ||
        (Cursor.lockState != CursorLockMode.Locked && AdjustButtonRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))));
    bool riding, instrumentActive;''')
s=s.replace('''        if (riding)
        {
            if (input''','''        if (riding)
        {
            if (Application.isFocused && Time.timeScale > 0 && Input.GetKeyDown(KeyCode.V))
            {
                if (IsAdjustingRidingPosition) EndRidingPositionAdjustment(true);
                else BeginRidingPositionAdjustment();
            }
            if (IsAdjustingRidingPosition)
            {
                bicycle.Drive(0, 0, true, Time.deltaTime);
                ApplyRidingView();
                hint = "Adjust your riding view, then Save & ride";
                return true;
            }
            if (input''',1)
s=s.replace('''            view.transform.localRotation=Quaternion.Euler(pitch,yaw,0);
            hint="W/S Pedal   A/D Steer   Space Brake   E Get off";''','''            ApplyRidingView();
            hint="W/S Pedal   A/D Steer   Space Brake   E Get off   V Adjust view";''')
s=s.replace('''    public bool TryUnloadBox''','''    void ApplyRidingView()
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
    public bool TryUnloadBox''',1)
s=s.replace('''        yaw=pitch=0;riding=true;bicycle.SetRidden(true);return true;''','''        yaw=pitch=0;riding=true;bicycle.SetRidden(true);ApplyRidingView();return true;''')
s=s.replace('''        riding=false;bicycle.SetRidden(false);''','''        if (IsAdjustingRidingPosition) EndRidingPositionAdjustment(false);
        riding=false;bicycle.SetRidden(false);''')
s=s.replace('''    void OnGUI()
    {
        if(string.IsNullOrEmpty(hint)''','''    float ViewSlider(string label, float value, float min, float max)
    {
        GUILayout.Label(label + "   " + value.ToString("0.00"));
        return GUILayout.HorizontalSlider(value, min, max);
    }
    void OnGUI()
    {
        if (riding && !IsAdjustingRidingPosition && GUI.Button(AdjustButtonRect, "Adjust riding position [V]"))
            BeginRidingPositionAdjustment();
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
            if (GUILayout.Button("Save & ride")) EndRidingPositionAdjustment(true);
            if (GUILayout.Button("Cancel")) EndRidingPositionAdjustment(false);
            GUILayout.EndHorizontal();
            GUILayout.Label("Bike brakes while adjusting. V also saves and closes.");
            GUILayout.EndArea();
        }
        if(string.IsNullOrEmpty(hint)''',1)
p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Controller/PlayerController.cs');s=p.read_text();s=s.replace('else if (Input.GetMouseButtonDown(0))','else if (Input.GetMouseButtonDown(0) && (bicycleInteraction == null || !bicycleInteraction.BlocksCursorCapture))');p.write_text(s)
