using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public AudioMixerGroup audioMixerMaster;
    public AudioMixerGroup audioMixerMusic;
    public AudioMixerGroup audioMixerEffects;

    public static SoundManager instance;
    private AudioMixerGroup cinematicOutput;
    private float effectsVolumeDb;
    private float outsideGain = 1f;
    private Coroutine outsideFade;
    public static AudioMixerGroup CinematicOutput => instance != null ? instance.cinematicOutput : EffectsOutput;

    public void BeginLaunchAudio()
    {
        if (outsideFade != null) StopCoroutine(outsideFade);
        outsideFade = StartCoroutine(FadeOutside());
    }

    private IEnumerator FadeOutside()
    {
        float initial = outsideGain;
        for (float elapsed = 0; elapsed < .8f; elapsed += Time.deltaTime)
        {
            outsideGain = Mathf.Lerp(initial, 0f, Mathf.SmoothStep(0, 1, elapsed / .8f));
            VolumeEffects(effectsVolumeDb);
            yield return null;
        }
        outsideGain = 0f;
        VolumeEffects(effectsVolumeDb);
        outsideFade = null;
    }

    public static AudioMixerGroup EffectsOutput
    {
        get
        {
            // Sources can be created before this manager's Awake during scene loading.
            var manager = instance != null ? instance : FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
            return manager != null ? manager.audioMixerEffects : null;
        }
    }

    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += RouteSceneEffects;
    private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= RouteSceneEffects;

    private void RouteSceneEffects(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (instance != this || audioMixerEffects == null) return;
        if (scene.name == "Menu" || scene.name == "Gameplay")
        {
            if (outsideFade != null) StopCoroutine(outsideFade);
            outsideFade = null;
            outsideGain = 1f;
            VolumeEffects(effectsVolumeDb);
        }
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = audioMixerEffects;
    }

    public static float ToDecibels(float normalizedVolume) =>
        normalizedVolume <= 0f ? -80f : Mathf.Log10(Mathf.Clamp01(normalizedVolume)) * 20f;

    public void ApplySavedVolumes(SettingsData settings)
    {
        if (settings == null) return;
        VolumeMaster(ToDecibels(settings.masterVolume));
        VolumeMusic(ToDecibels(settings.musicVolume));
        VolumeEffects(ToDecibels(settings.effectsVolume));
    }

    private void Awake()
    {
        if (!instance)
        { 
            instance = this;
            if (audioMixerEffects != null)
            {
                var groups = audioMixerEffects.audioMixer.FindMatchingGroups("Cinematic");
                cinematicOutput = groups.Length > 0 ? groups[0] : audioMixerEffects;
            }
        }
        else
        { 
            Destroy(this.gameObject);
        }
    }

    public void VolumeMusic(float value)
    {
        //    audioMixerDialog.audioMixer.SetFloat("Dialog Volume", MathF.Log10(value)*20);
        audioMixerMusic.audioMixer.SetFloat("Music", value);
        
        if (value <= -80f)
        {
            audioMixerMusic.audioMixer.SetFloat("Music", -80f);
        }
    }

    public void VolumeEffects(float value)
    {
        effectsVolumeDb = Mathf.Max(-80f, value);
        audioMixerEffects.audioMixer.SetFloat("Effects", Mathf.Max(-80f, effectsVolumeDb + ToDecibels(outsideGain)));
        audioMixerEffects.audioMixer.SetFloat("Cinematic", effectsVolumeDb);
    }

    public void VolumeMaster(float value)
    {

        //   audioMixerDialog.audioMixer.SetFloat("Master Volume", MathF.Log10(value)*20);
        audioMixerMaster.audioMixer.SetFloat("Master", value);

        if (value <= -80f)
        {
            audioMixerMaster.audioMixer.SetFloat("Master", -80f);
        }
    }
}
