using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class CutscenePauseTests
{
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [Test]
    public void CutsceneHidesOnlyHudAndUnpauseDoesNotRevealIt()
    {
        var root = new GameObject("UI"); root.SetActive(false);
        var hud = new GameObject("HUD"); hud.transform.SetParent(root.transform);
        var menu = new GameObject("Pause"); menu.transform.SetParent(root.transform);
        var previous = Pause.Instance; Pause.Instance = null;
        try
        {
            var canvas = root.AddComponent<Canvas>();
            var gameplay = root.AddComponent<GameplayCanvas>(); Set(gameplay, "hudObject", hud);
            gameplay.SetCutsceneHudHidden(root, true);
            Assert.IsFalse(hud.activeSelf);
            Assert.IsTrue(canvas.enabled); Assert.IsTrue(menu.activeSelf);
            typeof(GameplayCanvas).GetMethod("RestoreHudAfterPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameplay, null);
            Assert.IsFalse(hud.activeSelf);
            gameplay.SetCutsceneHudHidden(root, false);
            Assert.IsTrue(hud.activeSelf);
        }
        finally { Object.DestroyImmediate(root); Pause.Instance = previous; }
    }

    [Test]
    public void PauseFreezesAudioAndRestoresPreviousState()
    {
        var root = new GameObject("Pause test"); root.SetActive(false);
        var panel = new GameObject("Panel"); panel.transform.SetParent(root.transform);
        var main = new GameObject("Main"); main.transform.SetParent(panel.transform);
        bool audio = AudioListener.pause; float scale = Time.timeScale;
        var lockMode = Cursor.lockState; bool visible = Cursor.visible;
        try
        {
            var pause = root.AddComponent<Pause>(); Set(pause, "pauseScreen", panel); Set(pause, "mainScreen", main);
            AudioListener.pause = false; Time.timeScale = .5f;
            pause.PauseOn();
            Assert.IsTrue(AudioListener.pause); Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(panel.activeSelf);
            pause.Unpause();
            Assert.IsFalse(AudioListener.pause); Assert.AreEqual(.5f, Time.timeScale);
        }
        finally
        {
            Object.DestroyImmediate(root); AudioListener.pause = audio; Time.timeScale = scale;
            Cursor.lockState = lockMode; Cursor.visible = visible;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PauseButtonsShareHoverWithVerticalNavigationWithoutColorTint(bool stick)
    {
        var root = new GameObject("Pause selection test"); root.SetActive(false);
        var panel = new GameObject("Panel"); panel.transform.SetParent(root.transform);
        var main = new GameObject("Main"); main.transform.SetParent(panel.transform);
        bool audio = AudioListener.pause; float scale = Time.timeScale;
        var lockMode = Cursor.lockState; bool visible = Cursor.visible;
        var previousPause = Pause.Instance;
        Gamepad pad = null;
        try
        {
            var pause = root.AddComponent<Pause>(); Set(pause, "pauseScreen", panel); Set(pause, "mainScreen", main);
            var first = MakePauseButton(main, "First", out var firstMarker);
            var second = MakePauseButton(main, "Second", out var secondMarker);
            Set(second, "selectOnHover", true);
            Set(pause, "mainScreenButtons", new[] { first, second });
            int secondClicks = 0;
            second.button.onClick.AddListener(() => secondClicks++);
            root.SetActive(true);
            pause.PauseOn();
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            Assert.AreEqual(Selectable.Transition.None, first.button.transition);
            Assert.AreEqual(Selectable.Transition.None, second.button.transition);
            second.OnPointerEnter(null);
            Assert.IsFalse(firstMarker.activeSelf);
            Assert.IsTrue(secondMarker.activeSelf);
            Set(pause, "mainOpenedFrame", -1);
            pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            InputSystem.Update();
            pause.HandleMainScreenInput(pad);
            Assert.AreEqual(1, secondClicks);
            var up = stick ? new GamepadState { leftStick = Vector2.up } : new GamepadState().WithButton(GamepadButton.DpadUp);
            InputSystem.QueueStateEvent(pad, up);
            InputSystem.Update();
            pause.HandleMainScreenInput(pad);
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            pause.Unpause();
            pause.PauseOn();
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            pause.Unpause();
        }
        finally
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            Object.DestroyImmediate(root); Pause.Instance = previousPause;
            AudioListener.pause = audio; Time.timeScale = scale;
            Cursor.lockState = lockMode; Cursor.visible = visible;
        }
    }

    private SelectedUI MakePauseButton(GameObject parent, string name, out GameObject marker)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(SelectedUI));
        go.transform.SetParent(parent.transform);
        var selected = go.GetComponent<SelectedUI>(); selected.button = go.GetComponent<Button>();
        marker = new GameObject("Selected"); marker.transform.SetParent(go.transform);
        Set(selected, "selectedImage", marker);
        selected.Deselect();
        return selected;
    }
}
