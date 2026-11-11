using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Switch;

// This branch uses Nintendo confirm/cancel positions for every controller.
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
    public static ButtonControl ConfirmButton(Gamepad pad) => pad?.buttonEast;
    public static ButtonControl CancelButton(Gamepad pad) => pad?.buttonSouth;
    public static bool ConfirmPressed(Gamepad pad) => ConfirmButton(pad)?.wasPressedThisFrame == true;
    public static bool CancelPressed(Gamepad pad) => CancelButton(pad)?.wasPressedThisFrame == true;
}
