using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class MenuSessionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject root,menuObject,otherObject,main,settings,quit,newGame,continueButton;
    private string previousSave;
    private bool hadSave;
    private MenuManager menu,previousInstance;
    private PlayerData previousData;
    private bool previousLoaded,previousVisible,previousLoading;
    private CursorLockMode previousLock;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        hadSave=PlayerPrefs.HasKey("/save");previousSave=PlayerPrefs.GetString("/save");
        PlayerPrefs.SetString("/save","menu-test-placeholder");
        previousInstance=MenuManager.Instance;previousData=SaveSystem.Data;previousLoaded=SaveSystem.savesLoaded;
        previousTimeScale=Time.timeScale;previousLock=Cursor.lockState;previousVisible=Cursor.visible;
        previousLoading=LoadingScreen.isLoadingOn;LoadingScreen.isLoadingOn=false;
        root=new GameObject("Separate menu fixture");
        menuObject=Child("Menu");menuObject.SetActive(false);menu=menuObject.AddComponent<MenuManager>();
        otherObject=Child("Unrelated scene object");Set("menuCameras",new Camera[0]);
        main=Child("MenuScreen");main.transform.SetParent(menuObject.transform);
        settings=Child("SettingsScreen");settings.transform.SetParent(menuObject.transform);
        quit=Child("QuitGamePopup");quit.transform.SetParent(menuObject.transform);
        Set("menuScreen",main);Set("settingsScreen",settings);Set("quitGamePopup",quit);
        newGame=Child("NewGamePopup");Set("newGamePopup",newGame);
        continueButton=Child("Continue");Set("continueButton",continueButton);
        SaveSystem.Data=new PlayerData{cash=12345};SaveSystem.savesLoaded=true;
    }
    [TearDown]
    public void TearDown()
    {
        if(hadSave)PlayerPrefs.SetString("/save",previousSave);else PlayerPrefs.DeleteKey("/save");
        Object.DestroyImmediate(root);
        typeof(MenuManager).GetProperty("Instance").SetValue(null,previousInstance);
        SaveSystem.Data=previousData;SaveSystem.savesLoaded=previousLoaded;
        LoadingScreen.isLoadingOn=previousLoading;
        Time.timeScale=previousTimeScale;Cursor.lockState=previousLock;Cursor.visible=previousVisible;
    }

    [Test]
    public void OpeningMenuAlwaysStartsOnTheMainScreen()
    {
        settings.SetActive(true);quit.SetActive(true);Open();
        Assert.IsTrue(main.activeSelf);Assert.IsFalse(settings.activeSelf);Assert.IsFalse(quit.activeSelf);
    }

    [Test]
    public void SettingsQuitAndBackKeepTheCorrectPagesVisible()
    {
        Open();menu.ShowSettingsScreen();
        Assert.IsFalse(main.activeSelf);Assert.IsTrue(settings.activeSelf);Assert.IsFalse(quit.activeSelf);
        menu.ShowQuitGamePopup();
        Assert.IsTrue(main.activeSelf);Assert.IsFalse(settings.activeSelf);Assert.IsTrue(quit.activeSelf);
        menu.ShowMenuScreen();
        Assert.IsTrue(main.activeSelf);Assert.IsFalse(settings.activeSelf);Assert.IsFalse(quit.activeSelf);
    }

    [Test]
    public void LoadingBlocksMenuNavigation()
    {
        Open();LoadingScreen.isLoadingOn=true;menu.ShowSettingsScreen();menu.ShowQuitGamePopup();
        Assert.IsTrue(main.activeSelf);Assert.IsFalse(settings.activeSelf);Assert.IsFalse(quit.activeSelf);
    }

    [Test]
    public void OpeningSeparateMenuUnlocksCursorWithoutDisablingSceneObjectsOrPausingAnimations()
    {
        Time.timeScale=0;
        Open();
        Assert.IsTrue(MenuManager.IsMenuOpen);Assert.IsTrue(otherObject.activeSelf);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));Assert.IsTrue(Cursor.visible);
    }

    [Test]
    public void ContinueKeepsTheSaveAndSchedulesASceneLoadRatherThanActivatingGameplayObjects()
    {
        Open();var data=SaveSystem.Data;
        var routine=(IEnumerator)typeof(MenuManager).GetMethod("StartGame",Private).Invoke(menu,new object[]{false});
        Assert.IsTrue(routine.MoveNext());Assert.IsInstanceOf<IEnumerator>(routine.Current);
        Assert.IsTrue(menuObject.activeSelf);Assert.IsTrue(otherObject.activeSelf);
        Assert.AreSame(data,SaveSystem.Data);Assert.That(SaveSystem.Money,Is.EqualTo(12345));
        (routine as System.IDisposable)?.Dispose();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingGameplaySceneRejectsTheRequestBeforeChangingTheSave(bool newGame)
    {
        Open();var data=SaveSystem.Data;Set("gameplayScene","MissingSceneForMenuTest");
        LogAssert.Expect(LogType.Error,"Cannot load scene because it is missing from Build Settings: MissingSceneForMenuTest");
        if(newGame){menu.NewGame();menu.ConfirmNewGame();}else menu.ContinueGame();
        Assert.AreSame(data,SaveSystem.Data);Assert.That(SaveSystem.Money,Is.EqualTo(12345));
        Assert.IsFalse((bool)typeof(MenuManager).GetField("transitionRequested",Private).GetValue(menu));
    }

    [Test]
    public void LeavingMenuBeforeCameraInitializationStillDisablesEveryAssignedCamera()
    {
        var cameraObject=Child("Assigned menu camera");
        var camera=cameraObject.AddComponent<Camera>();
        var listener=cameraObject.AddComponent<AudioListener>();
        Set("menuCameras",new[]{camera});
        typeof(MenuManager).GetMethod("OnDisable",Private).Invoke(menu,null);
        Assert.IsFalse(camera.enabled);Assert.IsFalse(listener.enabled);Assert.IsFalse(cameraObject.activeSelf);
    }

    [Test]
    public void NoSaveHidesContinueAndRejectsContinueRequests()
    {
        PlayerPrefs.DeleteKey("/save");Open();
        Assert.IsTrue(continueButton.activeSelf);
        var data=SaveSystem.Data;menu.ContinueGame();
        Assert.AreSame(data,SaveSystem.Data);Assert.IsFalse(newGame.activeSelf);
    }

    [Test]
    public void ExistingSaveRequiresConfirmationAndCancelPreservesData()
    {
        Open();var data=SaveSystem.Data;
        Assert.IsTrue(continueButton.activeSelf);
        menu.NewGame();
        Assert.IsTrue(newGame.activeSelf);Assert.IsFalse(main.activeSelf);
        menu.ContinueGame();menu.NewGame();
        Assert.AreSame(data,SaveSystem.Data);
        menu.ShowMenuScreen();
        Assert.IsFalse(newGame.activeSelf);Assert.IsTrue(main.activeSelf);
        menu.ConfirmNewGame();Assert.AreSame(data,SaveSystem.Data);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ConfirmationSelectsArrayFirstNavigatesHorizontallyAndSubmitsSelectedButton(bool quitting, bool stick)
    {
        var popup = quitting ? quit : newGame;
        var second = PopupButton(popup, "Second", out var secondMarker);
        var first = PopupButton(popup, "First", out var firstMarker);
        Set(quitting ? "quitGamePopupButtons" : "newGamePopupButtons", new[] { first, second });
        int firstClicks = 0, secondClicks = 0;
        first.button.onClick.AddListener(() => firstClicks++);
        second.button.onClick.AddListener(() => secondClicks++);
        Open();
        if (quitting) menu.ShowQuitGamePopup(); else menu.NewGame();
        Assert.IsTrue(firstMarker.activeSelf);
        Assert.IsFalse(secondMarker.activeSelf);
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.AreEqual(0, firstClicks, "The opening press must not confirm the popup.");
            Set("popupOpenedFrame", -1);
            InputSystem.QueueStateEvent(pad, default(GamepadState));
            InputSystem.Update();
            var right = stick ? new GamepadState { leftStick = Vector2.right } : new GamepadState().WithButton(GamepadButton.DpadRight);
            InputSystem.QueueStateEvent(pad, right);
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsFalse(firstMarker.activeSelf);
            Assert.IsTrue(secondMarker.activeSelf);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.AreEqual(0, firstClicks);
            Assert.AreEqual(1, secondClicks);
            var left = stick ? new GamepadState { leftStick = Vector2.left } : new GamepadState().WithButton(GamepadButton.DpadLeft);
            InputSystem.QueueStateEvent(pad, left);
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            menu.ShowMenuScreen();
            if (quitting) menu.ShowQuitGamePopup(); else menu.NewGame();
            Assert.IsTrue(firstMarker.activeSelf, "Reopening must reset to the first array entry.");
            Assert.IsFalse(secondMarker.activeSelf);
        }
        finally { InputSystem.RemoveDevice(pad); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PopupHoverSharesGamepadSelectionAndNavigationClearsPreviousHover(bool quitting)
    {
        var popup = quitting ? quit : newGame;
        var first = PopupButton(popup, "First", out var firstMarker);
        var second = PopupButton(popup, "Second", out var secondMarker);
        typeof(SelectedUI).GetField("selectOnHover", Private).SetValue(second, true);
        Set(quitting ? "quitGamePopupButtons" : "newGamePopupButtons", new[] { first, second });
        int firstClicks = 0, secondClicks = 0;
        first.button.onClick.AddListener(() => firstClicks++);
        second.button.onClick.AddListener(() => secondClicks++);
        Open();
        if (quitting) menu.ShowQuitGamePopup(); else menu.NewGame();
        second.OnPointerEnter(null);
        Assert.IsFalse(firstMarker.activeSelf);
        Assert.IsTrue(secondMarker.activeSelf);
        Set("popupOpenedFrame", -1);
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.AreEqual(0, firstClicks);
            Assert.AreEqual(1, secondClicks);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf, "Stationary mouse hover must not leave a second highlight after gamepad navigation.");
        }
        finally { InputSystem.RemoveDevice(pad); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MainButtonsShareHoverSelectionWithVerticalGamepadNavigation(bool stick)
    {
        var first = PopupButton(main, "First", out var firstMarker);
        var second = PopupButton(main, "Second", out var secondMarker);
        typeof(SelectedUI).GetField("selectOnHover", Private).SetValue(second, true);
        Set("mainButtons", new[] { first, second });
        int secondClicks = 0;
        second.button.onClick.AddListener(() => secondClicks++);
        Open();
        Assert.IsTrue(firstMarker.activeSelf);
        Assert.IsFalse(secondMarker.activeSelf);
        second.OnPointerEnter(null);
        Assert.IsFalse(firstMarker.activeSelf);
        Assert.IsTrue(secondMarker.activeSelf);
        Set("popupOpenedFrame", -1);
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.AreEqual(1, secondClicks);
            var up = stick ? new GamepadState { leftStick = Vector2.up } : new GamepadState().WithButton(GamepadButton.DpadUp);
            InputSystem.QueueStateEvent(pad, up);
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            var down = stick ? new GamepadState { leftStick = Vector2.down } : new GamepadState().WithButton(GamepadButton.DpadDown);
            InputSystem.QueueStateEvent(pad, down);
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsFalse(firstMarker.activeSelf);
            Assert.IsTrue(secondMarker.activeSelf);
            menu.ShowQuitGamePopup();
            Assert.IsFalse(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
            menu.ShowMenuScreen();
            Assert.IsTrue(firstMarker.activeSelf);
            Assert.IsFalse(secondMarker.activeSelf);
        }
        finally { InputSystem.RemoveDevice(pad); }
    }

    [Test]
    public void NoSaveDisablesContinueHoverAndGamepadSelectionAndSaveRestoresIt()
    {
        var first = PopupButton(main, "Continue", out var firstMarker);
        var second = PopupButton(main, "New game", out var secondMarker);
        typeof(SelectedUI).GetField("selectOnHover", Private).SetValue(first, true);
        Set("mainButtons", new[] { first, second });
        PlayerPrefs.DeleteKey("/save");
        Open();
        Assert.IsTrue(first.gameObject.activeSelf);
        Assert.IsFalse(first.button.interactable);
        Assert.IsFalse(first.enabled);
        Assert.IsFalse(firstMarker.activeSelf);
        Assert.IsTrue(secondMarker.activeSelf);
        first.OnPointerEnter(null);
        Assert.IsFalse(firstMarker.activeSelf);
        Assert.IsTrue(secondMarker.activeSelf);
        Set("popupOpenedFrame", -1);
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
            InputSystem.Update();
            menu.HandleConfirmationInput(pad);
            Assert.IsFalse(firstMarker.activeSelf);
            Assert.IsTrue(secondMarker.activeSelf);
        }
        finally { InputSystem.RemoveDevice(pad); }
        PlayerPrefs.SetString("/save", "menu-test-placeholder");
        menu.ShowMenuScreen();
        Assert.IsTrue(first.button.interactable);
        Assert.IsTrue(first.enabled);
        Assert.IsTrue(firstMarker.activeSelf);
        Assert.IsFalse(secondMarker.activeSelf);
    }

    private SelectedUI PopupButton(GameObject popup, string name, out GameObject marker)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(SelectedUI));
        go.transform.SetParent(popup.transform);
        var selected = go.GetComponent<SelectedUI>();
        selected.button = go.GetComponent<Button>();
        marker = new GameObject("Selection");
        marker.transform.SetParent(go.transform);
        typeof(SelectedUI).GetField("selectedImage", Private).SetValue(selected, marker);
        selected.Deselect();
        return selected;
    }

    private void Open(){menuObject.SetActive(true);typeof(MenuManager).GetMethod("OnEnable",Private).Invoke(menu,null);}
    private GameObject Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go;}
    private void Set(string field,object value)=>typeof(MenuManager).GetField(field,Private).SetValue(menu,value);
}
