from pathlib import Path
p=Path('Assets/Editor/BicycleControllerEditor.cs');s=p.read_text().replace('previewView.LookAtDirect(eye + rotation * Vector3.forward * 3, rotation, 3, false);','previewView.orthographic = false;\n        previewView.LookAtDirect(eye + rotation * Vector3.forward * 3, rotation, 3);').replace('previewView.LookAtDirect(previousPivot, previousRotation, previousSize, previousOrtho);','{ previewView.orthographic = previousOrtho; previewView.LookAtDirect(previousPivot, previousRotation, previousSize); }');p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/BicycleController.cs');s=p.read_text().replace('    [Header("Steering")]','    [Min(1)] public float boostSpeedMultiplier = 1.5f;\n    [Header("Steering")]');s=s.replace('bool brake, float dt)','bool brake, float dt, bool boost = false)');s=s.replace('Mathf.Clamp(throttle, -.3f, 1f) * maximumSpeed;','Mathf.Clamp(throttle, -.3f, 1f) * maximumSpeed * (boost && throttle > 0 && !brake ? Mathf.Max(1, boostSpeedMultiplier) : 1);');p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Controller/StaminaManager.cs');s=p.read_text().replace('bool recoveryBlocked = false)','bool recoveryBlocked = false, float consumptionPerSecond = -1f)');s=s.replace('CurrentStamina - workConsumptionPerSecond * deltaTime','CurrentStamina - (consumptionPerSecond >= 0f && float.IsFinite(consumptionPerSecond) ? consumptionPerSecond : workConsumptionPerSecond) * deltaTime');p.write_text(s)
p=Path('Assets/_Scripts/Gameplay/Bicycle/PlayerBicycleInteraction.cs');s=p.read_text().replace('    public bool IsRiding => riding;','''    [Min(0)] public float boostStaminaPerSecond = 12f;
    public bool IsBoosting { get; private set; }
    StaminaManager stamina;
    bool boostExhausted;
    public bool IsRiding => riding;''');s=s.replace('bicycle.Drive(0, 0, true, Time.deltaTime);','DriveRiding(0, 0, true, false, Time.deltaTime);');s=s.replace('bicycle.Drive(input ? Input.GetAxisRaw("Vertical")','DriveRiding(input ? Input.GetAxisRaw("Vertical")');s=s.replace('!input || Input.GetKey(KeyCode.Space), Time.deltaTime);','!input || Input.GetKey(KeyCode.Space), input && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)), Time.deltaTime);');s=s.replace('Space Brake   E Get off   V Adjust view','Shift Faster   Space Brake   E Get off   V Adjust view');s=s.replace('    void ApplyRidingView()','''    public void DriveRiding(float throttle, float turn, bool brake, bool requestBoost, float dt)
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
    void ApplyRidingView()''');s=s.replace('riding=false;bicycle.SetRidden(false);','riding=false;IsBoosting=false;boostExhausted=false;bicycle.SetRidden(false);');s=s.replace('''        if (riding && !IsAdjustingRidingPosition && GUI.Button(AdjustButtonRect, "Adjust riding position [V]"))
            BeginRidingPositionAdjustment();''','''        if (riding && !IsAdjustingRidingPosition && GUI.Button(AdjustButtonRect, "Adjust riding position [V]"))
        {
            BeginRidingPositionAdjustment(); GUIUtility.ExitGUI();
        }''');s=s.replace('if (GUILayout.Button("Save & ride")) EndRidingPositionAdjustment(true);','if (GUILayout.Button("Save & ride")) { EndRidingPositionAdjustment(true); GUIUtility.ExitGUI(); }').replace('if (GUILayout.Button("Cancel")) EndRidingPositionAdjustment(false);','if (GUILayout.Button("Cancel")) { EndRidingPositionAdjustment(false); GUIUtility.ExitGUI(); }');s=s.replace('new Rect(Screen.width/2-230,Screen.height-85,460,36)','new Rect(Mathf.Max(8, Screen.width/2-340),Screen.height-85,Mathf.Min(680, Screen.width-16),36)');p.write_text(s)
