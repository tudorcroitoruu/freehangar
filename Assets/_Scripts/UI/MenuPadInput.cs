using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Switch;

// UI semantics differ from physical gameplay button positions on Nintendo pads.
public static class MenuPadInput
{
    public static bool UsesNintendoLayout(Gamepad pad)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        return true;
#else
        return pad is SwitchProController;
#endif
    }
    public static ButtonControl ConfirmButton(Gamepad pad) => pad == null ? null :
        UsesNintendoLayout(pad) ? pad.buttonEast : pad.buttonSouth;
    public static ButtonControl CancelButton(Gamepad pad) => pad == null ? null :
        UsesNintendoLayout(pad) ? pad.buttonSouth : pad.buttonEast;
    public static bool ConfirmPressed(Gamepad pad) => ConfirmButton(pad)?.wasPressedThisFrame == true;
    public static bool CancelPressed(Gamepad pad) => CancelButton(pad)?.wasPressedThisFrame == true;
}
