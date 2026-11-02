using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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
}
