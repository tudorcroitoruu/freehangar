using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
public static class ConfigureSeparateMenuScene
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Configure the menu outside Play Mode.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Menu.unity")throw new Exception("Open the new Menu scene.");
        var manager=UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene);
        var main=manager.transform.Find("MenuScreen")??manager.transform.Find("Menu");
        var settings=manager.transform.Find("SettingsScreen");
        var quit=manager.transform.Find("QuitGamePopup");
        if(main==null||settings==null||quit==null)throw new Exception("The three screen objects must be children of MenuCanvas.");
        Undo.RecordObject(main.gameObject,"Name main menu screen");main.name="MenuScreen";
        var global=UnityEngine.Object.FindObjectsByType<GlobalManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(x=>x.gameObject.scene==scene);
        if(global==null)
        {
            var sourceScene=SceneManager.GetSceneByPath("Assets/Scenes/Gameplay.unity");bool opened=!sourceScene.isLoaded;
            if(opened)sourceScene=EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity",OpenSceneMode.Additive);
            try
            {
                var source=UnityEngine.Object.FindObjectsByType<GlobalManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==sourceScene);
                var clone=UnityEngine.Object.Instantiate(source.transform.root.gameObject);
                clone.name=source.transform.root.name;
                SceneManager.MoveGameObjectToScene(clone,scene);
                Undo.RegisterCreatedObjectUndo(clone,"Add persistent loading and save services to menu");
                global=clone.GetComponentInChildren<GlobalManager>(true);
            }
            finally{if(opened)EditorSceneManager.CloseScene(sourceScene,true);SceneManager.SetActiveScene(scene);}
        }
        var loading=global.GetComponentInChildren<LoadingScreen>(true);
        if(loading==null)throw new Exception("Persistent bootstrap has no LoadingScreen.");
        Undo.RecordObject(global.gameObject,"Enable menu bootstrap");global.gameObject.SetActive(true);Dirty(global.gameObject);
        Undo.RecordObject(loading.gameObject,"Initialize menu loading curtain");loading.gameObject.SetActive(true);Dirty(loading.gameObject);
        var group=loading.GetComponent<CanvasGroup>();
        if(group!=null){Undo.RecordObject(group,"Hide loading curtain initially");group.alpha=0;group.interactable=false;group.blocksRaycasts=false;Dirty(group);}
        Undo.RecordObject(manager,"Connect separate menu screens");
        var so=new SerializedObject(manager);
        so.FindProperty("menuScreen").objectReferenceValue=main.gameObject;
        so.FindProperty("settingsScreen").objectReferenceValue=settings.gameObject;
        so.FindProperty("quitGamePopup").objectReferenceValue=quit.gameObject;
        so.FindProperty("loadingScreen").objectReferenceValue=loading;
        so.FindProperty("gameplayScene").stringValue="Gameplay";
        var cameraNames=new[]{"MenuCamera","MenuCamera (1)","MenuCamera (2)"};
        var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();
        var array=so.FindProperty("menuCameras");array.arraySize=3;
        for(int i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=cameras.Single(x=>x.name==cameraNames[i]);
        so.ApplyModifiedProperties();Dirty(manager);
        var buttons=main.GetComponentsInChildren<Button>(true);
        Wire(buttons.Single(x=>x.name=="Continue"),manager.ContinueGame);
        Wire(buttons.Single(x=>x.name=="NewGame"),manager.NewGame);
        Wire(buttons.Single(x=>x.name=="Settings"),manager.ShowSettingsScreen);
        Wire(buttons.Single(x=>x.name=="QuitGame"),manager.ShowQuitGamePopup);
        Wire(quit.GetComponentsInChildren<Button>(true).Single(x=>x.name=="ConfirmLeavingGame"),manager.QuitGame);
        Wire(quit.GetComponentsInChildren<Button>(true).Single(x=>x.name=="CancelLeavingGame"),manager.ShowMenuScreen);
        var back=settings.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name=="BackToMenu");
        if(back==null)
        {
            back=UnityEngine.Object.Instantiate(buttons.Single(x=>x.name=="Continue"),settings,false);
            back.name="BackToMenu";Undo.RegisterCreatedObjectUndo(back.gameObject,"Add settings back button");
            var rect=(RectTransform)back.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);
            rect.anchoredPosition=new Vector2(-40,-30);rect.sizeDelta=new Vector2(220,58);rect.localScale=Vector3.one;
            foreach(var label in back.GetComponentsInChildren<Text>(true))label.text="Back";
            foreach(var label in back.GetComponentsInChildren<TMP_Text>(true))label.text="Back";
        }
        Wire(back,manager.ShowMenuScreen);
        foreach(var pair in new[]{(main.gameObject,true),(settings.gameObject,false),(quit.gameObject,false)})
        {Undo.RecordObject(pair.Item1,"Set initial menu page");pair.Item1.SetActive(pair.Item2);Dirty(pair.Item1);}
        var buildScenes=EditorBuildSettings.scenes.Where(x=>x.path!=scene.path).ToList();
        buildScenes.Insert(0,new EditorBuildSettingsScene(scene.path,true));EditorBuildSettings.scenes=buildScenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,screens=new[]{main.name,settings.name,quit.name},backButton=back.name,bootstrap=global.name,loading=loading.name,cameras=cameraNames,buttons="Continue/New Game/Settings/Quit/Confirm/Cancel/Back connected",firstBuildScene=EditorBuildSettings.scenes[0].path};
    }
    static void Wire(Button button,UnityEngine.Events.UnityAction action)
    {
        Undo.RecordObject(button,"Wire menu navigation");
        while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);
        UnityEventTools.AddPersistentListener(button.onClick,action);Dirty(button);
    }
    static void Dirty(UnityEngine.Object obj){EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
}
