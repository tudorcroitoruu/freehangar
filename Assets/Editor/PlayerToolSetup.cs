using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerToolSetup
{
    [InitializeOnLoadMethod]
    private static void AfterImport() => EditorApplication.delayCall+=()=>
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode) Apply();
    };
    [MenuItem("Tools/Player/Connect discovered hand tools and audio")]
    public static void Apply()
    {
        var report=new System.Text.StringBuilder();
        foreach(var tools in Object.FindObjectsByType<PlayerCombatTools>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!tools.gameObject.scene.IsValid())continue;
            var data=new SerializedObject(tools);
            var camera=data.FindProperty("viewCamera").objectReferenceValue as Camera;
            if(camera==null)camera=tools.GetComponent<PlayerController>()?.characterCamera;
            if(camera==null)continue;
            data.FindProperty("viewCamera").objectReferenceValue=camera;
            foreach(var node in camera.GetComponentsInChildren<Transform>(true))
            {
                if(node.name=="Axe")data.FindProperty("heldAxe").objectReferenceValue=node;
                if(node.name=="Broom grip pivot")data.FindProperty("heldBroom").objectReferenceValue=node;
                if(node.name=="Held sledgehammer")data.FindProperty("heldSledgehammer").objectReferenceValue=node;
            }
            bool valid=data.FindProperty("heldAxe").objectReferenceValue!=null && data.FindProperty("heldBroom").objectReferenceValue!=null && data.FindProperty("heldSledgehammer").objectReferenceValue!=null;
            if(data.ApplyModifiedProperties())PrefabUtility.RecordPrefabInstancePropertyModifications(tools);
            report.AppendLine((valid?"PASS ":"MISSING ")+tools.name+": held axe, broom and hammer references.");
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab");
        var audio=prefab!=null?prefab.GetComponentInChildren<GameplayAudio>(true):null;
        if(audio!=null)
        {
            var data=new SerializedObject(audio);
            report.AppendLine("Tool audio: broom clips="+data.FindProperty("broomHits").arraySize+", axe clips="+data.FindProperty("axeWoodHits").arraySize+", metal impact="+(data.FindProperty("hammerMetal").objectReferenceValue!=null));
        }
        Directory.CreateDirectory("output/PlayerTools");File.WriteAllText("output/PlayerTools/bindings.txt",report.ToString());
        Debug.Log(report.ToString());
    }
}
