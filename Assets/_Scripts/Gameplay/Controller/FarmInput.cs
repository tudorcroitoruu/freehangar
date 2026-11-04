using UnityEngine;
using UnityEngine.InputSystem;

// The settings screen edits a separate asset. Gameplay reads the same binding overrides
// without enabling the actions that are waiting for an interactive rebind.
public static class FarmInput
{
    private static InputActionAsset actions;
    private static InputActionMap player;
    private static bool padLast;
    public static bool UsingPad => padLast && Gamepad.current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        InputSystem.onActionChange -= TrackDevice;
        GameSettingsManager.InputBindingsChanged -= RefreshBindings;
        if (actions) { actions.Disable(); Object.Destroy(actions); }
        actions = null;
        player = null;
        padLast = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (actions || !InputSystem.actions) return;
        actions = Object.Instantiate(InputSystem.actions);
        player = actions.FindActionMap("Player", true);
        RefreshBindings();
        player.Enable();
        padLast = Gamepad.current != null;
        InputSystem.onActionChange += TrackDevice;
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
        if (device is Gamepad) padLast = true;
        else if (device is Keyboard) padLast = false;
        else if (device is Mouse mouse && mouse.layout != "VirtualMouse" &&
            (mouse.delta.ReadValue().sqrMagnitude > .01f || mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            padLast = false;
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
            if (control.device == Gamepad.current) return control.shortDisplayName ?? control.displayName;
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
