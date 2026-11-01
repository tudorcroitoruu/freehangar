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

        //   audioMixerDialog.audioMixer.SetFloat("Master Volume", MathF.Log10(value)*20);
        audioMixerEffects.audioMixer.SetFloat("Effects", value);

        if (value <= -80f)
        {
            audioMixerEffects.audioMixer.SetFloat("Effects", -80f);
        }
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
