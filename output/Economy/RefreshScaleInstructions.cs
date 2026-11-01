using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
public static class RefreshScaleInstructions
{
    public static object Main()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Open Gameplay");
        var stations=UnityEngine.Object.FindObjectsByType<GrainWeighStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
        foreach(var station in stations)
        {
            using(var so=new SerializedObject(station))
            {
                var text=so.FindProperty("statusText").objectReferenceValue as TMP_Text;
                if(text!=null&&!Application.isPlaying)Undo.RecordObject(text,"Update containers-only weighing instructions");
                station.RefreshStatus();
                if(text!=null)
                {
                    text.ForceMeshUpdate(true,true);
                    if(!Application.isPlaying){PrefabUtility.RecordPrefabInstancePropertyModifications(text);EditorUtility.SetDirty(text);}
                }
            }
        }
        if(!Application.isPlaying){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        return stations.Select(s=>new{s.StatusMessage,textOverflow=s.GetComponentInChildren<TMP_Text>(true)?.isTextOverflowing}).ToArray();
    }
}
