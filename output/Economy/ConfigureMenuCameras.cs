using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ConfigureMenuCameras
{
    public static object Main()
    {
        if(Application.isPlaying) throw new Exception("Configure menu cameras outside Play Mode.");
        var manager=UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        string[] names={"MenuCamera","MenuCamera (1)","MenuCamera (2)"};
        var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var selected=names.Select(name=>cameras.Single(c=>c.name==name && c.gameObject.scene==manager.gameObject.scene)).ToArray();
        Undo.RecordObject(manager,"Assign menu background cameras");
        var so=new SerializedObject(manager);
        var array=so.FindProperty("menuCameras");array.arraySize=3;
        for(int i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=selected[i];
        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        return new {scene=manager.gameObject.scene.path,cameras=selected.Select(c=>c.name).ToArray(),
            hold=so.FindProperty("cameraHoldSeconds").floatValue,fade=so.FindProperty("fadeSeconds").floatValue};
    }
}
