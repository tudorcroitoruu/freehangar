using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MenuSessionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject root,menuObject,otherObject,main,settings,quit;
    private MenuManager menu,previousInstance;
    private PlayerData previousData;
    private bool previousLoaded,previousVisible,previousLoading;
    private CursorLockMode previousLock;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
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
        SaveSystem.Data=new PlayerData{cash=12345};SaveSystem.savesLoaded=true;
    }
    [TearDown]
    public void TearDown()
    {
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
        if(newGame)menu.NewGame();else menu.ContinueGame();
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

    private void Open(){menuObject.SetActive(true);typeof(MenuManager).GetMethod("OnEnable",Private).Invoke(menu,null);}
    private GameObject Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go;}
    private void Set(string field,object value)=>typeof(MenuManager).GetField(field,Private).SetValue(menu,value);
}
