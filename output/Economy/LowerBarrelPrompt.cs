using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class LowerBarrelPrompt
{
    static void SetOffset(GrainContainer container)
    {
        Undo.RecordObject(container,"Lower barrel interaction prompt");
        using(var so=new SerializedObject(container))
        {
            so.FindProperty("promptOffset").vector3Value=new Vector3(0,-.2f,0);
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(container);
    }
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        const string path="Assets/Prefabs/Gameplay/Containers/Barrel.prefab";
        const string backup="output/Economy/Backups/Barrel.before-prompt-offset.prefab";
        if(!File.Exists(backup))File.Copy(path,backup);
        var prefab=PrefabUtility.LoadPrefabContents(path);
        try { SetOffset(prefab.GetComponent<GrainContainer>());PrefabUtility.SaveAsPrefabAsset(prefab,path); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")return new{prefab=path,sceneChanged=false};
        string sceneBackup="output/Economy/Backups/Gameplay.before-barrel-prompt.unity";
        if(!File.Exists(sceneBackup))EditorSceneManager.SaveScene(scene,sceneBackup,true);
        var barrels=UnityEngine.Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.gameObject.scene==scene&&(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject)==path||c.name=="Barrel")).ToArray();
        foreach(var barrel in barrels){SetOffset(barrel);PrefabUtility.RecordPrefabInstancePropertyModifications(barrel);}
        if(barrels.Length>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        return new{prefab=path,barrels=barrels.Select(b=>new{b.name,top=b.WorldBounds.max.y,promptY=b.PromptPosition.y,loweredBy=.32f}).ToArray()};
    }
}
