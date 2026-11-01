using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AudioFlowValidation
{
    [MenuItem("Tools/Audio/Connect menu audio services")]
    public static void ConnectMenuServices()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Menu") throw new InvalidOperationException("Open Menu first.");
        var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuManager>(true)).Single();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GlobalManager.prefab").GetComponent<GlobalManager>();
        var data = new SerializedObject(menu);
        data.FindProperty("servicesPrefab").objectReferenceValue = prefab;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Menu.");
        Validate();
    }
    [MenuItem("Tools/Audio/Validate menu and loading audio")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate audio assets in Edit mode.");
        var report = new StringBuilder();
        GameObject root = null;
        var previousAudio = AudioManager.Instance;
        float listenerVolume = AudioListener.volume;
        try
        {
            root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GlobalManager.prefab");
            var audio = root.GetComponentInChildren<AudioManager>(true);
            Require(audio != null, "Persistent bootstrap contains AudioManager", report);
            var data = new SerializedObject(audio);
            var intro = data.FindProperty("introMusic").objectReferenceValue as AudioClip;
            var source = data.FindProperty("musicAudioSource").objectReferenceValue as AudioSource;
            Require(intro != null && source != null, "Intro clip and music source assigned", report);
            Require(data.FindProperty("musicClips").arraySize > 0, "Persistent manager inherits non-empty regular playlist", report);
            Require(root.GetComponent<GlobalManager>() != null, "Audio is under persistent GlobalManager", report);
            Require(source.outputAudioMixerGroup != null, "Music is routed through mixer", report);
            foreach (string field in new[] { "selectClips", "confirmClips", "cancelClips" })
                Require(data.FindProperty(field).arraySize > 0 && data.FindProperty(field).GetArrayElementAtIndex(0).objectReferenceValue != null, field + " connected", report);
            foreach (string field in new[] { "soundEffectsAudioSource", "otherAudioSource" })
            {
                var sfx = data.FindProperty(field).objectReferenceValue as AudioSource;
                Require(sfx != null && sfx.outputAudioMixerGroup != null && !sfx.mute, field + " assigned, unmuted and routed", report);
            }
            AudioManager.Instance = audio;
            audio.PlayLoadingMusic();
            Require(source.clip == intro && source.loop, "Loading starts looping intro when silent", report);
            Require(!(bool)typeof(AudioManager).GetField("introPlayed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio), "Loading does not consume gameplay intro cue", report);
            source.Stop();
            Require(SoundManager.ToDecibels(0) == -80f && Mathf.Abs(SoundManager.ToDecibels(1)) < .001f && Mathf.Abs(SoundManager.ToDecibels(.5f) + 6.0206f) < .001f,
                "Saved audio levels map to mute / 0 dB / -6 dB", report);
        }
        finally
        {
            AudioManager.Instance = previousAudio;
            AudioListener.volume = listenerVolume;
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
        ValidateScene("Assets/Scenes/Menu.unity", true, report);
        ValidateScene("Assets/Scenes/LoadingScene.unity", false, report);
        Directory.CreateDirectory("output/Audio");
        File.WriteAllText("output/Audio/menu-loading-validation.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    private static void ValidateScene(string path, bool menu, StringBuilder report)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var roots = scene.GetRootGameObjects();
            Require(roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Any(l => l.enabled && l.gameObject.activeInHierarchy), path + ": active listener present", report);
            if (!menu) return;
            var birds = roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true))
                .FirstOrDefault(s => s.clip != null && AssetDatabase.GetAssetPath(s.clip) == "Assets/Audio/Bird_Singing.wav");
            Require(birds != null && birds.gameObject.activeInHierarchy && birds.enabled && birds.playOnAwake && birds.loop && !birds.mute && birds.spatialBlend == 0,
                "Menu birds use gameplay clip, active 2D looping source", report);
            var manager = roots.SelectMany(r => r.GetComponentsInChildren<MenuManager>(true)).FirstOrDefault();
            Require(manager != null, "Menu navigation controller present", report);
            var pages = new SerializedObject(manager);
            foreach (var field in new[] { "menuScreen", "settingsScreen", "quitGamePopup", "servicesPrefab" })
                Require(pages.FindProperty(field).objectReferenceValue != null, "Menu page wired: " + field, report);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Require(bool condition, string message, StringBuilder report)
    {
        if (!condition) throw new InvalidOperationException("Audio validation failed: " + message);
        report.AppendLine("PASS " + message);
    }
}
