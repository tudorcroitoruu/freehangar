using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class RestoreSeparateGameplayScene
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before changing scene authoring.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Open Gameplay.");
        string[] names={"Player","GameCanvas","GameManager","Audio Game","Grain pests and broom tutorial"};
        var roots=scene.GetRootGameObjects();
        foreach(string name in names)
        {
            var go=roots.Single(x=>x.name==name);
            Undo.RecordObject(go,"Restore standalone gameplay scene");
            go.SetActive(true);Dirty(go);
        }
        foreach(var menu in UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene))
        {
            Undo.RecordObject(menu.gameObject,"Disable old shared-scene menu");menu.gameObject.SetActive(false);Dirty(menu.gameObject);
            var so=new SerializedObject(menu);var array=so.FindProperty("menuCameras");
            for(int i=0;i<array.arraySize;i++)
            {
                var camera=array.GetArrayElementAtIndex(i).objectReferenceValue as Camera;if(camera==null)continue;
                Undo.RecordObjects(new UnityEngine.Object[]{camera,camera.gameObject},"Disable copied menu cameras in gameplay");
                camera.enabled=false;camera.gameObject.SetActive(false);Dirty(camera);Dirty(camera.gameObject);
            }
        }
        // The user will author the separate menu scene at this existing path.
        const string menuPath="Assets/Scenes/Menu.unity";
        var builds=EditorBuildSettings.scenes.ToList();
        if(!builds.Any(x=>x.path==menuPath))builds.Add(new EditorBuildSettingsScene(menuPath,true));
        else foreach(var build in builds.Where(x=>x.path==menuPath))build.enabled=true;
        EditorBuildSettings.scenes=builds.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,restored=names,menuObjects="Retained inactive in Gameplay for copying into the separate menu scene",buildScenes=builds.Select(x=>x.path).ToArray()};
    }
    static void Dirty(UnityEngine.Object obj){EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
}
