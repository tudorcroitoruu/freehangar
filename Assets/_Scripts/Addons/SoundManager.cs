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
        
        if (value <= -30f)
        {
            audioMixerMusic.audioMixer.SetFloat("Music", -80f);
        }
    }

    public void VolumeEffects(float value)
    {

        //   audioMixerDialog.audioMixer.SetFloat("Master Volume", MathF.Log10(value)*20);
        audioMixerEffects.audioMixer.SetFloat("Effects", value);

        if (value <= -30f)
        {
            audioMixerEffects.audioMixer.SetFloat("Effects", -80f);
        }
    }

    public void VolumeMaster(float value)
    {

        //   audioMixerDialog.audioMixer.SetFloat("Master Volume", MathF.Log10(value)*20);
        audioMixerMaster.audioMixer.SetFloat("Master", value);

        if (value <= -30f)
        {
            audioMixerMaster.audioMixer.SetFloat("Master", -80f);
        }
    }
}