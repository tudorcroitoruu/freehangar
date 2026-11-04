using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class FarmGamepadInputTests
{
    [TestCase("buttonNorth")]
    [TestCase("buttonSouth")]
    [TestCase("buttonWest")]
    [TestCase("buttonEast")]
    [TestCase("start")]
    [TestCase("select")]
    public void ButtonPromptRendersAsOneSprite(string control)
    {
        var root = new GameObject("Prompt rendering test", typeof(RectTransform), typeof(Canvas));
        var child = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        child.transform.SetParent(root.transform, false);
        try
        {
            var label = child.GetComponent<TMPro.TextMeshProUGUI>();
            label.font = TMPro.TMP_Settings.defaultFontAsset;
            label.richText = true;
            label.rectTransform.sizeDelta = new Vector2(32, 32);
            label.richText = false;
            GamepadPromptSprites.SetKeycap(label, GamepadPromptSprites.Label(control));
            label.ForceMeshUpdate(true, true);
            Assert.AreEqual(1, label.textInfo.characterCount, "Sprite tag was displayed as literal text.");
            Assert.AreEqual(TMPro.TMP_TextElementType.Sprite, label.textInfo.characterInfo[0].elementType);
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test]
    public void ControllerSticksAndButtonsReachTheGameplayActions()
    {
        var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            asset.devices = new InputDevice[] { pad };
            var map = asset.FindActionMap("Player", true);
            map.Enable();
            // Editor updates deliberately suppress gameplay action callbacks. The binding
            // checks run in either mode; Play Mode additionally exercises the device state.
            foreach (var action in map.actions)
            {
                Assert.AreEqual(1, action.controls.Count, action.name);
                Assert.AreSame(pad, action.controls[0].device, action.name);
            }
            void Binding(string name, InputControl control) => Assert.AreSame(control, map[name].controls[0], name);
            Binding("Move", pad.leftStick);
            Binding("Look", pad.rightStick);
            Binding("Attack", pad.rightTrigger);
            Binding("Unload", pad.leftTrigger);
            Binding("Interact", pad.buttonNorth);
            Binding("Secondary", pad.buttonWest);
            Binding("Jump", pad.buttonSouth);
            Binding("Crouch", pad.buttonEast);
            Binding("Cargo", pad.rightShoulder);
            Binding("ResetVehicle", pad.leftShoulder);
            Binding("Sprint", pad.leftStickButton);
            Binding("Previous", pad.dpad.up);
            Binding("Next", pad.dpad.right);
            Binding("HammerSlot", pad.dpad.down);
            Binding("UtilitySlot", pad.dpad.left);
            Binding("Pause", pad.startButton);
            if (!Application.isPlaying) return;
            void State(GamepadState state)
            {
                InputSystem.QueueStateEvent(pad, state);
                InputSystem.Update();
            }
            State(new GamepadState { leftStick = Vector2.right, rightStick = Vector2.up });
            Assert.AreEqual(Vector2.right, map["Move"].ReadValue<Vector2>());
            Assert.AreEqual(Vector2.up, map["Look"].ReadValue<Vector2>());
            void Press(string action, GamepadState state)
            {
                State(default);
                State(state);
                Assert.IsTrue(map[action].IsPressed(), action);
                foreach (var other in map.actions)
                    if (other != map[action] && other.type == InputActionType.Button)
                        Assert.IsFalse(other.IsPressed(), $"{action} also activated {other.name}");
            }
            Press("Interact", new GamepadState(GamepadButton.North));
            Press("Secondary", new GamepadState(GamepadButton.West));
            Press("Jump", new GamepadState(GamepadButton.South));
            Press("Crouch", new GamepadState(GamepadButton.East));
            Press("Attack", new GamepadState { rightTrigger = 1 });
            Press("Unload", new GamepadState { leftTrigger = 1 });
            Press("Sprint", new GamepadState(GamepadButton.LeftStick));
            Press("Cargo", new GamepadState(GamepadButton.RightShoulder));
            Press("ResetVehicle", new GamepadState(GamepadButton.LeftShoulder));
            Press("Previous", new GamepadState(GamepadButton.DpadUp));
            Press("Next", new GamepadState(GamepadButton.DpadRight));
            Press("HammerSlot", new GamepadState(GamepadButton.DpadDown));
            Press("UtilitySlot", new GamepadState(GamepadButton.DpadLeft));
            Press("Pause", new GamepadState(GamepadButton.Start));
        }
        finally
        {
            asset.Disable();
            InputSystem.RemoveDevice(pad);
            Object.DestroyImmediate(asset);
        }
    }
}
