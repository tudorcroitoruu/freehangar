using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
public static class ConfigureSharedSceneMenu
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Configure outside Play Mode.");
        var manager=UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var scene=manager.gameObject.scene;
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Open Gameplay.");
        var player=UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene).gameObject;
        var hud=UnityEngine.Object.FindObjectsByType<GameplayCanvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene).gameObject;
        var game=UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene).gameObject;
        var loading=UnityEngine.Object.FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene);
        var extras=scene.GetRootGameObjects().Where(x=>x.name=="Audio Game"||x.name=="Grain pests and broom tutorial").ToArray();
        Undo.RecordObject(manager,"Configure shared menu/gameplay");
        var so=new SerializedObject(manager);
        so.FindProperty("player").objectReferenceValue=player;
        so.FindProperty("gameplayCanvas").objectReferenceValue=hud;
        so.FindProperty("gameManager").objectReferenceValue=game;
        so.FindProperty("loadingScreen").objectReferenceValue=loading;
        var array=so.FindProperty("gameplayObjects");
        // Keep any objects the user has already configured.
        var extraList=Enumerable.Range(0,array.arraySize).Select(i=>array.GetArrayElementAtIndex(i).objectReferenceValue as GameObject).Where(x=>x!=null).Concat(extras).Distinct().ToArray();
        array.arraySize=extraList.Length;
        for(int i=0;i<extraList.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=extraList[i];
        so.ApplyModifiedProperties();
        var buttons=manager.GetComponentsInChildren<Button>(true);
        var continueButton=buttons.Single(b=>b.name=="Continue");
        var newButton=buttons.Single(b=>b.name=="NewGame");
        Clear(continueButton);UnityEventTools.AddPersistentListener(continueButton.onClick,manager.ContinueGame);Dirty(continueButton);
        Clear(newButton);UnityEventTools.AddPersistentListener(newButton.onClick,manager.NewGame);Dirty(newButton);
        var pause=UnityEngine.Object.FindObjectsByType<Pause>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.gameObject.scene==scene);
        var pauseSo=new SerializedObject(pause);
        var leaveButton=pauseSo.FindProperty("confirmLeaveGameButton").objectReferenceValue as Button;
        if(leaveButton!=null)
        {
            Undo.RecordObject(leaveButton,"Return to shared-scene menu");
            for(int i=leaveButton.onClick.GetPersistentEventCount()-1;i>=0;i--)
                if(leaveButton.onClick.GetPersistentTarget(i)==pause &&
                    (leaveButton.onClick.GetPersistentMethodName(i)=="QuitGame" || leaveButton.onClick.GetPersistentMethodName(i)=="GoToMenu"))
                    UnityEventTools.RemovePersistentListener(leaveButton.onClick,i);
            Dirty(leaveButton);
        }
        Undo.RecordObjects(new UnityEngine.Object[]{manager.gameObject,player,hud,game},"Start shared scene in menu");
        player.SetActive(false);hud.SetActive(false);game.SetActive(false);
        foreach(var item in extraList){Undo.RecordObject(item,"Hide gameplay object in menu");item.SetActive(false);Dirty(item);}
        manager.gameObject.SetActive(true);
        var bootstrap=loading.GetComponentInParent<GlobalManager>(true);
        if(bootstrap==null)throw new Exception("LoadingScreen needs the persistent GlobalManager root.");
        Undo.RecordObject(bootstrap.gameObject,"Enable persistent save and loading services");
        bootstrap.gameObject.SetActive(true);
        // Awake establishes the loading singleton; LoadingOff keeps its popup hidden.
        Undo.RecordObject(loading.gameObject,"Initialize persistent loading curtain");
        loading.gameObject.SetActive(true);
        foreach(var item in new UnityEngine.Object[]{manager,manager.gameObject,player,hud,game,bootstrap.gameObject,loading.gameObject})Dirty(item);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,player=player.name,hud=hud.name,gameManager=game.name,extras=extraList.Select(x=>x.name).ToArray(),loading=loading.name,buttons=new[]{continueButton.name,newButton.name}};
    }
    static void Clear(Button button){Undo.RecordObject(button,"Connect menu action");while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);}
    static void Dirty(UnityEngine.Object obj){EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
}
