using UnityEngine;
using UnityEngine.InputSystem;

// The settings screen edits a separate asset. Gameplay reads the same binding overrides
// without enabling the actions that are waiting for an interactive rebind.
public static class FarmInput
{
    private static InputActionAsset actions;
    private static InputActionMap player;
    private static bool padLast;
    private static Gamepad activePad;
    public static Gamepad ActivePad => activePad != null && activePad.added ? activePad : null;
    public static bool UsingPad => padLast && ActivePad != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        InputSystem.onActionChange -= TrackDevice; InputSystem.onAfterUpdate -= TrackDesktop;
        GameSettingsManager.InputBindingsChanged -= RefreshBindings;
        if (actions) { actions.Disable(); Object.Destroy(actions); }
        actions = null;
        player = null;
        padLast = false;
        activePad = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (actions || !InputSystem.actions) return;
        actions = Object.Instantiate(InputSystem.actions);
        player = actions.FindActionMap("Player", true);
        RefreshBindings();
        player.Enable();
        activePad = Gamepad.current;
        padLast = activePad != null;
        InputSystem.onActionChange += TrackDevice; InputSystem.onAfterUpdate += TrackDesktop;
        GameSettingsManager.InputBindingsChanged += RefreshBindings;
    }

    internal static void RefreshBindings()
    {
        if (actions) GameSettingsManager.ApplySavedInputBindings(actions);
    }

    private static void TrackDevice(object changed, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed || !(changed is InputAction action)) return;
        var device = action.activeControl?.device;
        if (device is Gamepad pad && action.activeControl.EvaluateMagnitude() > .2f)
        {
            // Keep ownership while a controller is in use; emulated duplicates can also emit events.
            if (ActivePad == null || pad == activePad || !padLast ||
                (activePad.leftStick.ReadValue().sqrMagnitude < .01f && activePad.rightStick.ReadValue().sqrMagnitude < .01f))
                activePad = pad;
            padLast = true;
        }
    }
    private static void TrackDesktop()
    {
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) padLast = false;
        foreach (var device in InputSystem.devices)
            if (device is Mouse mouse && mouse.layout != "VirtualMouse" && mouse.name != "VirtualMouse" &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame ||
                 mouse.backButton.wasPressedThisFrame)) padLast = false;
    }
    private static InputAction Action(string name)
    {
        if (player == null) Initialize();
        return player?.FindAction(name);
    }
    public static bool Down(string name) => Action(name)?.WasPressedThisFrame() == true;
    public static bool Held(string name) => Action(name)?.IsPressed() == true;
    public static Vector2 Move => Action("Move")?.ReadValue<Vector2>() ?? Vector2.zero;
    public static Vector2 Look
    {
        get
        {
            var action = Action("Look");
            if (action == null) return Vector2.zero;
            // Mouse delta is per frame. A stick specifies an angular speed.
            return action.ReadValue<Vector2>() * (action.activeControl?.device is Gamepad
                ? 90f * Time.deltaTime : .1f);
        }
    }
    public static bool Back => Input.GetKeyDown(KeyCode.Escape) ||
        (Gamepad.current?.buttonEast.wasPressedThisFrame == true && PadMenuCursor.DropdownClosedFrame != Time.frameCount);
    public static bool ContextDown(KeyCode key) => key switch
    {
        KeyCode.F => Down("Secondary"), KeyCode.Q => Down("Cargo"),
        KeyCode.E => Down("Interact"), KeyCode.R => Down("ResetVehicle"),
        _ => Input.GetKeyDown(key)
    };
    public static string Label(string action, string keyboard)
    {
        if (!UsingPad) return keyboard;
        var a = Action(action);
        if (a == null) return keyboard;
        foreach (var control in a.controls)
            if (control.device == ActivePad) return GamepadPromptSprites.Label(control.name) ?? (control.name switch
            {
                "leftTrigger" => "L2", "rightTrigger" => "R2",
                "leftShoulder" => "L1", "rightShoulder" => "R1",
                "leftStickPress" => "L3", "rightStickPress" => "R3",
                "leftStick" => "LS", "rightStick" => "RS",
                _ => control.shortDisplayName ?? control.displayName
            });
        return keyboard;
    }
    public static string KeyLabel(KeyCode key) => key switch
    {
        KeyCode.None or KeyCode.E => Label("Interact","E"), KeyCode.F => Label("Secondary","F"),
        KeyCode.Q => Label("Cargo","Q"), KeyCode.R => Label("ResetVehicle","R"),
        KeyCode.Alpha1 => Label("Previous","1"), KeyCode.Alpha2 => Label("Next","2"),
        KeyCode.Alpha3 => Label("HammerSlot","3"), KeyCode.Alpha4 => Label("UtilitySlot","4"),
        _ => key.ToString()
    };
}
