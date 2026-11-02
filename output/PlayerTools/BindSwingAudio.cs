using UnityEditor;
using UnityEngine;
public static class BindSwingAudio
{
    public static string Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab");
        var audio = prefab.GetComponentInChildren<GameplayAudio>(true);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Gameplay/Controller/Hit Air.wav");
        if (clip == null) throw new System.Exception("Hit Air clip missing");
        var data = new SerializedObject(audio);
        data.FindProperty("hitAir").objectReferenceValue = clip;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
        return "Bound " + clip.name + " (" + clip.length + " seconds) to GameplayAudio.";
    }
}
