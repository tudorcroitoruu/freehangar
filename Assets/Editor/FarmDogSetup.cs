using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class FarmDogSetup
{
    private const string Folder = "Assets/Prefabs/Gameplay/Dog";
    [MenuItem("Tools/Animals/Set up farm dog")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay")
            throw new System.InvalidOperationException("Open Gameplay in Edit Mode first.");
        if (Object.FindFirstObjectByType<FarmDog>(FindObjectsInactive.Include))
            throw new System.InvalidOperationException("Farm dog is already configured.");
        var model = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(x => x.name == "TFP_Dog_01D");
        if (!NavMesh.SamplePosition(model.transform.position, out var nav, 2f, NavMesh.AllAreas))
            throw new System.InvalidOperationException("The dog needs ground navigation at its home.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Gameplay", "Dog");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/FarmDog.controller");
        var states = controller.layers[0].stateMachine;
        string[] names = { "Idle", "Run", "Attack", "Sleep", "Stroke" };
        string[] clips = { "Idle_01", "Galop", "Bark", "Sleep", "SitIdle_03" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/Imported/Toon Series/Toon Farm Pack/Animations/Animal Animations/Dog/TFP_Dog_01_" + clips[i] + "_Regular.controller";
            var source = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            var state = states.AddState(names[i], new Vector3(250, i * 65));
            state.motion = source.animationClips[0];
            if (names[i] == "Attack") state.speed = 2f;
            if (i == 0) states.defaultState = state;
        }
        var go = new GameObject("Farm dog companion");
        Undo.RegisterCreatedObjectUndo(go, "Set up farm dog");
        go.transform.SetParent(model.transform.parent, true);
        go.transform.SetPositionAndRotation(nav.position, model.transform.rotation);
        Undo.SetTransformParent(model.transform, go.transform, "Parent dog model");
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        var dog = Undo.AddComponent<FarmDog>(go);
        var modifier = Undo.AddComponent<Unity.AI.Navigation.NavMeshModifier>(go);
        modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
        var agent = go.GetComponent<NavMeshAgent>();
        agent.radius = .3f; agent.height = 1f; agent.speed = 5.5f;
        agent.angularSpeed = 540; agent.acceleration = 20; agent.stoppingDistance = .4f;
        var capsule = Undo.AddComponent<CapsuleCollider>(go);
        capsule.center = new Vector3(0, .45f, 0); capsule.height = .9f; capsule.radius = .35f;
        dog.visual = model.transform; dog.animator = model;
        model.runtimeAnimatorController = controller; model.applyRootMotion = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        var mouth = new GameObject("Carried chicken leg").transform;
        mouth.SetParent(model.transform, false); mouth.localPosition = new Vector3(0, .55f, .55f);
        var head = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Def_Head");
        if (head) mouth.SetParent(head, true);
        dog.mouth = mouth;
        var delivery = new GameObject("Dog reward drop point").transform;
        delivery.SetParent(go.transform.parent, true);
        delivery.position = nav.position + go.transform.right * .9f + Vector3.up * .2f;
        dog.deliveryPoint = delivery;
        dog.legPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Chickens/ChickenLeg.prefab");
        BindSleepVfx(dog);
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 1; audio.maxDistance = 12;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Gameplay/Animals/Dog/Dog Stroke.wav");
        var gameplayAudio = Object.FindFirstObjectByType<GameplayAudio>(FindObjectsInactive.Include);
        if (gameplayAudio)
            audio.outputAudioMixerGroup = new SerializedObject(gameplayAudio).FindProperty("effectsGroup").objectReferenceValue as UnityEngine.Audio.AudioMixerGroup;
        dog.strokeAudio = audio;
        dog.barkClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Gameplay/Animals/Dog/Bark.mp3");
        dog.chickenMarkerSettings = AssetDatabase.LoadAssetAtPath<SickscoreGames.HUDNavigationSystem.HUDNavigationElementSettings>("Assets/Sickscore Games/HUD-Navigation-System/FreeHangar/Settings_Crows.asset");
        var settings = ScriptableObject.CreateInstance<DogUpgradeSettings>();
        AssetDatabase.CreateAsset(settings, Folder + "/DogUpgradeSettings.asset");
        Undo.AddComponent<DogUpgradeInteractable>(go).settings = settings;
        EditorUtility.SetDirty(dog); EditorUtility.SetDirty(model);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene); AssetDatabase.SaveAssets();
    }

    public static void BindSleepVfx(FarmDog dog)
    {
        if (dog.sleepVfx) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Epic Toon FX/Prefabs/Misc/SleepingZzZ.prefab");
        var effect = (GameObject)PrefabUtility.InstantiatePrefab(prefab, dog.transform);
        Undo.RegisterCreatedObjectUndo(effect, "Assign dog sleeping VFX");
        effect.transform.localPosition = new Vector3(0, .75f, .3f);
        effect.transform.localScale = Vector3.one * .65f;
        dog.sleepVfx = effect.GetComponent<ParticleSystem>();
        var main = dog.sleepVfx.main; main.playOnAwake = false;
        dog.sleepVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        PrefabUtility.RecordPrefabInstancePropertyModifications(effect.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(dog.sleepVfx);
        EditorUtility.SetDirty(dog);
    }
}
