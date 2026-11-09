using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public class GameSettingsNavigationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private GameSettingsManager manager, previousManager;
    private SettingsData previousSettings;
    private bool previousLoaded;
    private Gamepad pad;
    private SelectedUI[] video, audio;
    private GameObject[] videoMarkers, audioMarkers, sections;
    private Slider master, music, sfx, sensitivity;
    private int previousVSync, previousFrameRate;

    [SetUp]
    public void SetUp()
    {
        previousManager = GameSettingsManager.Instance;
        typeof(GameSettingsManager).GetProperty("Instance").SetValue(null, null);
        previousSettings = SaveSystem.settingsData;
        previousLoaded = SaveSystem.savesLoaded;
        SaveSystem.savesLoaded = false;
        SaveSystem.settingsData = new SettingsData { masterVolume = .5f, musicVolume = .5f, effectsVolume = .5f };
        previousVSync = QualitySettings.vSyncCount;
        previousFrameRate = Application.targetFrameRate;
        root = new GameObject("Settings navigation test"); root.SetActive(false);
        manager = root.AddComponent<GameSettingsManager>();
        sections = new GameObject[3];
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i] = new GameObject("Section " + i);
            sections[i].transform.SetParent(root.transform);
        }
        video = MakeRows(sections[0], 6, out videoMarkers);
        audio = MakeRows(sections[1], 3, out audioMarkers);
        master = MakeSlider(sections[1]); music = MakeSlider(sections[1]); sfx = MakeSlider(sections[1]);
        sensitivity = MakeSlider(sections[2]);
        Set(manager, "sections", sections); Set(manager, "videoSettings", video); Set(manager, "audioSettings", audio);
        Set(manager, "masterSlider", master); Set(manager, "musicSlider", music); Set(manager, "sfxSlider", sfx);
        Set(manager, "mouseSensitivitySlider", sensitivity); Set(manager, "initialized", true);
        root.SetActive(true);
        pad = InputSystem.AddDevice<Gamepad>();
    }

    [TearDown]
    public void TearDown()
    {
        if (pad != null) InputSystem.RemoveDevice(pad);
        Object.DestroyImmediate(root);
        typeof(GameSettingsManager).GetProperty("Instance").SetValue(null, previousManager);
        SaveSystem.settingsData = previousSettings; SaveSystem.savesLoaded = previousLoaded;
        QualitySettings.vSyncCount = previousVSync; Application.targetFrameRate = previousFrameRate;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void VideoHoverAndVerticalNavigationShareOneSelectionAndVSyncUsesHorizontalInput(bool stick)
    {
        AssertSelection(videoMarkers, 0);
        video[2].OnPointerEnter(null);
        AssertSelection(videoMarkers, 2);
        Send(Direction(Vector2.down, stick));
        AssertSelection(videoMarkers, 3);
        Send(Direction(Vector2.right, stick));
        Assert.IsTrue(SaveSystem.settingsData.vSync);
        Send(Direction(Vector2.left, stick));
        Assert.IsFalse(SaveSystem.settingsData.vSync);
        Send(Direction(Vector2.up, stick));
        AssertSelection(videoMarkers, 2);
    }

    [Test]
    public void DefaultVideoSelectionRetriesAfterActivationAndPreservesSubsequentHover()
    {
        video[0].gameObject.SetActive(false);
        manager.ShowSection(0);
        foreach (var marker in videoMarkers) Assert.IsFalse(marker.activeSelf);
        video[0].gameObject.SetActive(true);
        Send(default(GamepadState));
        AssertSelection(videoMarkers, 0);
        video[2].OnPointerEnter(null);
        Send(default(GamepadState));
        AssertSelection(videoMarkers, 2);
        root.SetActive(false);
        root.SetActive(true);
        AssertSelection(videoMarkers, 0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DLSSRowFollowsVSyncAndOnlyEnablesOnSupportedHardware(bool stick)
    {
        manager.ShowSection(0); // EditMode does not invoke the runtime OnEnable lifecycle.
        for (int i = 0; i < 3; i++) Send(Direction(Vector2.down, stick));
        Send(Direction(Vector2.down, stick));
        AssertSelection(videoMarkers, 4);
        Send(Direction(Vector2.right, stick));
        Assert.AreEqual(GameSettingsManager.IsDLSSSupported, SaveSystem.settingsData.dlss);
        Send(Direction(Vector2.left, stick));
        Assert.IsFalse(SaveSystem.settingsData.dlss);
        Send(Direction(Vector2.down, stick));
        AssertSelection(videoMarkers, 5);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShouldersResetPageSelectionAndAudioRowsAdjustTheMatchingSliders(bool stick)
    {
        Send(new GamepadState().WithButton(GamepadButton.RightShoulder));
        Assert.IsTrue(sections[1].activeSelf);
        AssertSelection(audioMarkers, 0);
        Send(Direction(Vector2.right, stick));
        Assert.That(master.value, Is.EqualTo(.55f).Within(.001f));
        Assert.That(music.value, Is.EqualTo(.5f).Within(.001f));
        audio[2].OnPointerEnter(null);
        AssertSelection(audioMarkers, 2);
        Send(Direction(Vector2.left, stick));
        Assert.That(sfx.value, Is.EqualTo(.45f).Within(.001f));
        Send(Direction(Vector2.up, stick));
        AssertSelection(audioMarkers, 1);
        Send(Direction(Vector2.right, stick));
        Assert.That(music.value, Is.EqualTo(.55f).Within(.001f));
        Send(new GamepadState().WithButton(GamepadButton.RightShoulder));
        Assert.IsTrue(sections[2].activeSelf);
        Send(Direction(Vector2.right, stick));
        Assert.That(sensitivity.value, Is.EqualTo(1.1f).Within(.001f));
        Send(new GamepadState().WithButton(GamepadButton.LeftShoulder));
        AssertSelection(audioMarkers, 0);
        Send(new GamepadState().WithButton(GamepadButton.LeftShoulder));
        AssertSelection(videoMarkers, 0);
    }

    private void Send(GamepadState state)
    {
        Set(manager, "sectionOpenedFrame", -1);
        InputSystem.QueueStateEvent(pad, default(GamepadState)); InputSystem.Update();
        Assert.IsTrue(manager.HandleSettingsInput(pad));
        InputSystem.QueueStateEvent(pad, state); InputSystem.Update();
        Assert.IsTrue(manager.HandleSettingsInput(pad));
    }

    private static GamepadState Direction(Vector2 direction, bool stick)
    {
        if (stick) return new GamepadState { leftStick = direction };
        return new GamepadState().WithButton(direction.x > 0 ? GamepadButton.DpadRight : direction.x < 0 ? GamepadButton.DpadLeft :
            direction.y > 0 ? GamepadButton.DpadUp : GamepadButton.DpadDown);
    }

    private SelectedUI[] MakeRows(GameObject parent, int count, out GameObject[] markers)
    {
        var rows = new SelectedUI[count]; markers = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Row " + i); go.transform.SetParent(parent.transform);
            rows[i] = go.AddComponent<SelectedUI>();
            markers[i] = new GameObject("Selected"); markers[i].transform.SetParent(go.transform);
            Set(rows[i], "selectOnHover", true); Set(rows[i], "selectedImage", markers[i]);
            rows[i].Deselect();
        }
        return rows;
    }

    private Slider MakeSlider(GameObject parent)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider)); go.transform.SetParent(parent.transform);
        return go.GetComponent<Slider>();
    }

    private static void AssertSelection(GameObject[] markers, int index)
    {
        for (int i = 0; i < markers.Length; i++) Assert.AreEqual(i == index, markers[i].activeSelf);
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
}
