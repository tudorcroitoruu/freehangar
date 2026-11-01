using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
public static class SetupStationStatus
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Gameplay.unity")throw new Exception("Open Gameplay");
        const string backup="output/Economy/Backups/Gameplay.before-status-only-station.unity";
        if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        var stations=UnityEngine.Object.FindObjectsByType<GrainWeighStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
        foreach(var station in stations)
        {
            Undo.RecordObject(station,"Configure status-only weighing");
            using(var so=new SerializedObject(station))
            {
                so.FindProperty("sellingZone").objectReferenceValue=station.GetComponentInParent<SellingZone>();
                var text=so.FindProperty("statusText").objectReferenceValue as TMP_Text;
                if(text==null)text=station.GetComponentInChildren<TMP_Text>(true);
                so.FindProperty("statusText").objectReferenceValue=text;
                so.FindProperty("m_Enabled").boolValue=true;
                so.ApplyModifiedProperties();
                if(text!=null)
                {
                    Undo.RecordObject(text,"Fit weighing instructions");
                    float maximum=Mathf.Max(1,text.fontSize);
                    text.fontSizeMax=maximum;text.fontSizeMin=maximum*.3f;
                    text.enableAutoSizing=true;text.textWrappingMode=TextWrappingModes.Normal;text.lineSpacing=0;
                    station.RefreshStatus();text.ForceMeshUpdate(true,true);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);EditorUtility.SetDirty(text);
                }
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);EditorUtility.SetDirty(station);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return stations.Select(s=>new{name=s.name,s.enabled,s.StatusMessage,text=s.GetComponentInChildren<TMP_Text>(true)==null?null:new{size=s.GetComponentInChildren<TMP_Text>(true).fontSize,overflow=s.GetComponentInChildren<TMP_Text>(true).isTextOverflowing}}).ToArray();
    }
}
