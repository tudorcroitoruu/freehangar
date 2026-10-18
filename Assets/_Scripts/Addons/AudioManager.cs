using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class AudioManager : MonoBehaviour
{
    private const float ShootVolumeScale = 1f / 3f / 1.4f;
    private const float PopVolumeScale = 1f / 1.5f;
    private const float ShootPlaybackInterval = 0.06f;
    private const float FailedPlaybackInterval = 0.15f;
    private const float PopPlaybackInterval = 0.1f;
    private const int MaximumQueuedPopSounds = 4;
    private const int TransientAudioPriority = 192;

    public bool inGame;
    public Coroutine coroutine;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioSource soundEffectsAudioSource;
    [SerializeField] private AudioSource otherAudioSource;

    [Header("UI Sound Effects")]
    [SerializeField] private AudioClip[] selectClips;
    [SerializeField] private AudioClip[] confirmClips;
    [SerializeField] private AudioClip[] cancelClips;

    [Header("Game Result Sound Effects")]
    [FormerlySerializedAs("levelLoseClip")]
    [SerializeField] private AudioClip loseClip;
    [FormerlySerializedAs("levelWinClip")]
    [SerializeField] private AudioClip winClip;

    [Header("Bee Jam Gameplay Sound Effects")]
    [SerializeField] private AudioClip beeClickClip;
    [SerializeField] private AudioClip beeGetOutClip;
    [SerializeField] private AudioClip beeWrongClip;
    [SerializeField] private AudioClip flowerPopClip;

    [Header("Toy Factory Gameplay Sound Effects")]
    [SerializeField] private AudioClip enterOnBoxClip;
    [SerializeField] private AudioClip lockedToyClip;
    [SerializeField] private AudioClip putOnConveyorClip;
    [SerializeField] private AudioClip revivePopupClip;
    [SerializeField] private AudioClip winToyPingClip;

    [Header("Color Sculpt Gameplay Sound Effects")]
    [SerializeField] private AudioClip failedClip;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip[] destroyCubeClips;
    [SerializeField] private AudioClip placeGunClip;
    [SerializeField] private AudioClip destroyGunClip;

    [Header("Color Lizard Gameplay Sound Effects")]
    [SerializeField] private AudioClip destroyObstacleClip;
    [SerializeField] private AudioClip freezeClip;
    [SerializeField] private AudioClip releaseLizardClip;
    [SerializeField] private AudioClip successLizardClip;
    [SerializeField] private AudioClip takeLizardClip;
    [SerializeField] private AudioClip timerClip;
    private AudioSource timerAudioSource;
    private bool applicationPaused;
    [SerializeField] private AudioClip[] lizardSolvePopClips;
    

    [Header("Music")]
    [SerializeField] private AudioClip[] musicClips;

    public static AudioManager Instance;

    private int lastMusicNumber = -1;
    private int lastDestroyCubeClipIndex = -1;
    private int lastLizardSolvePopClipIndex = -1;
    private int pendingDestroyCubeSounds;
    private readonly HashSet<Object> beaverFootstepOwners = new HashSet<Object>();
    private AudioSource destroyCubeAudioSource;
    private AudioSource shootAudioSource;
    private AudioSource beaverFootstepsAudioSource;
    private Coroutine destroyCubePlaybackCoroutine;
    private float lastShootPlaybackTime = float.NegativeInfinity;
    private float lastFailedPlaybackTime = float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        PreloadGameplayAudio();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (musicAudioSource != null && musicClips != null && musicClips.Length > 0)
            PlayNextRandomMusic();
    }

    public void PlayLose() => PlayGameplayClip(loseClip);
    public void PlayWin() => PlayGameplayClip(winClip);
    public void PlayBeeClick() => PlayGameplayClip(beeClickClip);
    public void PlayBeeGetOut() => PlayGameplayClip(beeGetOutClip);
    public void PlayBeeWrong() => PlayGameplayClip(beeWrongClip);
    public void PlayFlowerPop() => PlayGameplayClip(flowerPopClip);
    public void PlayEnterOnBox() => PlayGameplayClip(enterOnBoxClip);
    public void PlayLockedToy() => PlayGameplayClip(lockedToyClip);
    public void PlayPutOnConveyor() => PlayGameplayClip(putOnConveyorClip);
    public void PlayRevivePopup() => PlayGameplayClip(revivePopupClip);
    public void PlayWinToyPing() => PlayGameplayClip(winToyPingClip);
    public void PlayFailed()
    {
        if (Time.unscaledTime - lastFailedPlaybackTime < FailedPlaybackInterval)
            return;

        lastFailedPlaybackTime = Time.unscaledTime;
        PlayGameplayClip(failedClip);
    }
    public void PlayShoot()
    {
        if (shootClip == null ||
            Time.unscaledTime - lastShootPlaybackTime < ShootPlaybackInterval)
        {
            return;
        }

        shootAudioSource = EnsureTransientAudioSource(
            shootAudioSource,
            "Shoot Audio",
            ShootVolumeScale);
        if (shootAudioSource == null)
            return;

        lastShootPlaybackTime = Time.unscaledTime;
        shootAudioSource.clip = shootClip;
        shootAudioSource.Play();
    }
    public void PlayDestroyCube() => PlayPop();
    public void PlayPop()
    {
        if (!HasPlayableClip(destroyCubeClips))
            return;

        pendingDestroyCubeSounds = Mathf.Min(
            pendingDestroyCubeSounds + 1,
            MaximumQueuedPopSounds);
        if (destroyCubePlaybackCoroutine == null)
            destroyCubePlaybackCoroutine = StartCoroutine(PlayQueuedDestroyCubeSounds());
    }
    public void PlayPlaceGun() => PlayGameplayClip(placeGunClip);
    public void PlayDestroyGun() => PlayGameplayClip(destroyGunClip);
    public void PlayDestroyObstacle() => PlayGameplayClip(destroyObstacleClip);
    public void PlayFreeze() => PlayGameplayClip(freezeClip);
    public void PlayReleaseLizard() => PlayGameplayClip(releaseLizardClip);
    public void PlaySuccessLizard() => PlayGameplayClip(successLizardClip);
    // Hole-entry animation supplies the 0.1-second cadence and stops on exit.
    // Play directly so no queued pops continue after the lizard disappears.
    public void PlayLizardSolvePop() =>
        PlayRandomGameplayClip(destroyCubeClips, ref lastLizardSolvePopClipIndex);
    public void PlayTakeLizard() => PlayGameplayClip(takeLizardClip);

    public void SetTimerWarningActive(bool active)
    {
        if (!active || applicationPaused || timerClip == null || SaveSystem.settingsData == null || SaveSystem.settingsData.effectsVolume <= 0f)
        {
            if (timerAudioSource != null && timerAudioSource.isPlaying) timerAudioSource.Stop();
            return;
        }
        timerAudioSource = EnsureTransientAudioSource(timerAudioSource, "Timer Warning Audio", 1f);
        if (timerAudioSource.isPlaying) return;
        timerAudioSource.clip = timerClip;
        timerAudioSource.loop = true;
        timerAudioSource.spatialBlend = 0f;
        timerAudioSource.Play();
    }

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        if (paused) SetTimerWarningActive(false);
    }

    private void OnDisable() => SetTimerWarningActive(false);


    public void PlaySelectStandard(int index, bool randomClip = false)
    {
        PlayClip(soundEffectsAudioSource, selectClips, index, randomClip);
    }

    public void PlayConfirmStandart(int index, bool randomClip = false)
    {
        PlayClip(soundEffectsAudioSource, confirmClips, index, randomClip);
    }

    public void PlayCancelStandart(int index, bool randomClip = false)
    {
        PlayClip(soundEffectsAudioSource, cancelClips, index, randomClip);
    }

    public void PlayConfirm()
    {
        PlayClip(otherAudioSource, confirmClips, 0);
    }

    public void PlayCancel()
    {
        PlayClip(otherAudioSource, cancelClips, 0);
    }

    private void PreloadGameplayAudio()
    {
        PreloadAudioClip(loseClip);
        PreloadAudioClip(winClip);
        PreloadAudioClip(beeClickClip);
        PreloadAudioClip(beeGetOutClip);
        PreloadAudioClip(beeWrongClip);
        PreloadAudioClip(flowerPopClip);
        PreloadAudioClip(enterOnBoxClip);
        PreloadAudioClip(lockedToyClip);
        PreloadAudioClip(putOnConveyorClip);
        PreloadAudioClip(revivePopupClip);
        PreloadAudioClip(winToyPingClip);
        PreloadAudioClip(failedClip);
        PreloadAudioClip(shootClip);
        PreloadAudioClips(destroyCubeClips);
        PreloadAudioClip(placeGunClip);
        PreloadAudioClip(destroyGunClip);
        PreloadAudioClip(destroyObstacleClip);
        PreloadAudioClip(freezeClip);
        PreloadAudioClip(releaseLizardClip);
        PreloadAudioClip(successLizardClip);
        PreloadAudioClip(takeLizardClip);
        PreloadAudioClip(timerClip);
        PreloadAudioClips(lizardSolvePopClips);
    }

    private static void PreloadAudioClips(AudioClip[] clips)
    {
        if (clips == null)
            return;

        for (int index = 0; index < clips.Length; index++)
            PreloadAudioClip(clips[index]);
    }

    private static void PreloadAudioClip(AudioClip clip)
    {
        if (clip != null)
            clip.LoadAudioData();
    }

    private void PlayGameplayClip(AudioClip clip, float volumeScale = 1f)
    {
        AudioSource source = otherAudioSource != null ? otherAudioSource : soundEffectsAudioSource;
        if (source != null && clip != null)
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void PlayRandomGameplayClip(AudioClip[] clips, ref int lastClipIndex)
    {
        AudioClip clip = GetRandomClipNotSameAsLast(clips, ref lastClipIndex);
        PlayGameplayClip(clip);
    }



    private IEnumerator PlayQueuedDestroyCubeSounds()
    {
        AudioSource source = EnsureDestroyCubeAudioSource();
        while (pendingDestroyCubeSounds > 0)
        {
            pendingDestroyCubeSounds--;
            AudioClip clip = GetNextDestroyCubeClip();
            if (source != null && clip != null)
            {
                source.clip = clip;
                source.Play();
            }

            yield return new WaitForSecondsRealtime(PopPlaybackInterval);
        }

        destroyCubePlaybackCoroutine = null;
    }

    private AudioSource EnsureDestroyCubeAudioSource()
    {
        destroyCubeAudioSource = EnsureTransientAudioSource(
            destroyCubeAudioSource,
            "Pop Audio",
            PopVolumeScale);
        return destroyCubeAudioSource;
    }

    private AudioSource EnsureTransientAudioSource(
        AudioSource source,
        string sourceName,
        float volumeScale)
    {
        if (source != null)
            return source;

        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;

        AudioSource template = otherAudioSource != null ? otherAudioSource : soundEffectsAudioSource;
        if (template != null)
        {
            source.outputAudioMixerGroup = template.outputAudioMixerGroup;
            source.volume = template.volume * Mathf.Clamp01(volumeScale);
            source.mute = template.mute;
            source.priority = Mathf.Max(template.priority, TransientAudioPriority);
            source.panStereo = template.panStereo;
            source.spatialBlend = template.spatialBlend;
            source.reverbZoneMix = template.reverbZoneMix;
        }

        return source;
    }

    private AudioClip GetNextDestroyCubeClip()
    {
        int selectedIndex = -1;
        int candidateCount = 0;
        for (int index = 0; index < destroyCubeClips.Length; index++)
        {
            if (destroyCubeClips[index] == null || index == lastDestroyCubeClipIndex)
                continue;

            candidateCount++;
            if (Random.Range(0, candidateCount) == 0)
                selectedIndex = index;
        }

        if (selectedIndex < 0)
        {
            for (int index = 0; index < destroyCubeClips.Length; index++)
            {
                if (destroyCubeClips[index] == null)
                    continue;
                selectedIndex = index;
                break;
            }
        }

        if (selectedIndex < 0)
            return null;

        lastDestroyCubeClipIndex = selectedIndex;
        return destroyCubeClips[selectedIndex];
    }

    private static AudioClip GetRandomClipNotSameAsLast(
        AudioClip[] clips,
        ref int lastClipIndex)
    {
        if (!HasPlayableClip(clips))
            return null;

        int selectedIndex = -1;
        int candidateCount = 0;
        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] == null || index == lastClipIndex)
                continue;

            candidateCount++;
            if (Random.Range(0, candidateCount) == 0)
                selectedIndex = index;
        }

        if (selectedIndex < 0)
        {
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                    continue;
                selectedIndex = index;
                break;
            }
        }

        lastClipIndex = selectedIndex;
        return selectedIndex >= 0 ? clips[selectedIndex] : null;
    }

    private static bool HasPlayableClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] != null)
                return true;
        }
        return false;
    }

    private static void PlayClip(AudioSource source, AudioClip[] clips, int index, bool randomClip = false)
    {
        if (source == null || clips == null || clips.Length == 0)
            return;

        if (randomClip)
            index = Random.Range(Mathf.Clamp(index, 0, clips.Length - 1), clips.Length);

        if (index >= 0 && index < clips.Length && clips[index] != null)
            source.PlayOneShot(clips[index]);
    }

    private void PlayNextRandomMusic()
    {
        if (musicAudioSource == null || musicClips == null || musicClips.Length == 0)
            return;

        int nextIndex = GetRandomMusicIndexNotSameAsLast();
        lastMusicNumber = nextIndex;
        StartPlayingRandomMusic(musicClips[nextIndex]);
    }

    private int GetRandomMusicIndexNotSameAsLast()
    {
        if (musicClips.Length <= 1)
            return 0;

        int index = Random.Range(0, musicClips.Length);
        return index == lastMusicNumber ? (index + 1) % musicClips.Length : index;
    }

    private async void StartPlayingRandomMusic(AudioClip musicClip)
    {
        StartNewMusicCoroutine();
        float targetVolume = musicAudioSource.volume;
        musicAudioSource.volume = 0f;
        musicAudioSource.Stop();
        musicAudioSource.clip = musicClip;
        musicAudioSource.Play();

        for (int i = 0; i < 10; i++)
        {
            if (musicAudioSource == null)
                return;
            musicAudioSource.volume = targetVolume * ((i + 1) / 10f);
            await Task.Delay(100);
        }
        if (musicAudioSource != null)
            musicAudioSource.volume = targetVolume;
    }

    private IEnumerator PlayRandomMusicCoroutine()
    {
        while (musicAudioSource != null && musicAudioSource.isPlaying)
            yield return null;

        yield return null;
        PlayNextRandomMusic();
    }

    private void StartNewMusicCoroutine()
    {
        if (coroutine != null)
            StopCoroutine(coroutine);

        coroutine = StartCoroutine(PlayRandomMusicCoroutine());
    }
}
