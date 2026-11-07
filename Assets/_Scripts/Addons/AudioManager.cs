using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class AudioManager : MonoBehaviour
{
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
    [SerializeField] private AudioClip coinChangeClip;

    [Header("Game Result Sound Effects")]
    [FormerlySerializedAs("levelLoseClip")]
    [SerializeField] private AudioClip loseClip;
    [FormerlySerializedAs("levelWinClip")]
    [SerializeField] private AudioClip winClip;

    [Header("Music")]
    [SerializeField] private AudioClip[] musicClips;
    [SerializeField] private AudioClip introMusic;
    [SerializeField] private AudioClip attackMusic;
    [SerializeField] private AudioClip interstellarMusic;
    [SerializeField, Range(0f, 1f)] private float pauseChance = .35f;
    [SerializeField, Min(0f)] private float pauseSeconds = 15f;

    public static AudioManager Instance;
    private enum MusicMode { None, Regular, Intro, Attack, Ending, Menu }
    private MusicMode musicMode;
    private int lastMusicNumber = -1;
    private float musicVolume = 1f;
    private bool introPlayed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (soundEffectsAudioSource != null) soundEffectsAudioSource.ignoreListenerPause = true;
        if (otherAudioSource != null) otherAudioSource.ignoreListenerPause = true;
        if (musicAudioSource != null) musicVolume = musicAudioSource.volume;
        PreloadResultAudio();
    }

    private void OnEnable()
    {
        if (Instance == this) SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator Start()
    {
        while (GlobalManager.Instance != null && !SaveSystem.savesLoaded) yield return null;
        SoundManager.instance?.ApplySavedVolumes(SaveSystem.settingsData);
        if (musicMode != MusicMode.None) yield break;
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GlobalManager.Instance != null && !SaveSystem.savesLoaded) return;
        PlaySceneMusic(scene.name);
    }

    private void PlaySceneMusic(string sceneName)
    {
        if (sceneName == "Last Scene") PlayInterstellar();
        else if (sceneName == "Menu" || sceneName == "Bootstrap") PlayMenuMusic();
        else if (sceneName == "LoadingScene") PlayLoadingMusic();
        else if (sceneName == "Gameplay" && GameManager.Instance != null && GameManager.Instance.IntroductionPending) PlayMenuMusic();
        else if (musicMode == MusicMode.Ending || musicMode == MusicMode.None || musicMode == MusicMode.Menu) PlayRegularMusic();
    }

    public void PlayLoadingMusic()
    {
        // Keep the current track through scene unloading, including the menu's looping intro.
        // Fill a playlist pause immediately instead of carrying silence into loading.
        if (musicAudioSource != null && musicAudioSource.isPlaying &&
            (musicMode == MusicMode.Menu || musicMode == MusicMode.Intro || musicMode == MusicMode.Regular)) return;
        PlayMenuMusic();
    }

    private void PlayMenuMusic()
    {
        if (introMusic == null || musicAudioSource == null) { PlayRegularMusic(); return; }
        if (musicMode == MusicMode.Menu && musicAudioSource.isPlaying) return;
        StopMusic();
        musicMode = MusicMode.Menu;
        musicAudioSource.clip = introMusic;
        musicAudioSource.loop = true;
        musicAudioSource.Play();
        // Menu playback does not consume the gameplay introduction's once-only cue.
    }

    public void PlayIntroOnce(bool restart = false)
    {
        if (restart) introPlayed = false;
        if (introPlayed || introMusic == null || musicAudioSource == null) return;
        introPlayed = true;
        StopMusic();
        musicMode = MusicMode.Intro;
        musicAudioSource.clip = introMusic;
        musicAudioSource.Play();
        coroutine = StartCoroutine(ResumeRegularMusicAfterSpecial(MusicMode.Intro));
    }

    public void StopAttackMusic() { if(musicMode==MusicMode.Attack) PlayRegularMusic(); }

    public void SeekMusic(float seconds)
    {
        if (musicAudioSource != null && musicAudioSource.clip != null)
            musicAudioSource.time = Mathf.Clamp(seconds, 0, Mathf.Max(0, musicAudioSource.clip.length - .01f));
    }

    public void PlayAttackMusic()
    {
        if (attackMusic == null || musicAudioSource == null || musicMode == MusicMode.Ending) return;
        StopMusic();
        musicMode = MusicMode.Attack;
        musicAudioSource.clip = attackMusic;
        musicAudioSource.Play();
        coroutine = StartCoroutine(ResumeRegularMusicAfterSpecial(MusicMode.Attack));
    }

    private void PlayInterstellar()
    {
        StopMusic();
        musicMode = MusicMode.Ending;
        if (musicAudioSource == null || interstellarMusic == null) return;
        musicAudioSource.clip = interstellarMusic;
        musicAudioSource.Play();
    }

    public void PlayLose() => PlayGameplayClip(loseClip);
    public void PlayWin() => PlayGameplayClip(winClip);

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

    private void PreloadResultAudio()
    {
        if (loseClip != null) loseClip.LoadAudioData();
        if (winClip != null) winClip.LoadAudioData();
    }

    private void PlayGameplayClip(AudioClip clip)
    {
        AudioSource source = otherAudioSource != null ? otherAudioSource : soundEffectsAudioSource;
        if (source != null && clip != null)
            source.PlayOneShot(clip);
    }

    public void PlayCoinChange()
    {
        AudioSource source = otherAudioSource != null ? otherAudioSource : soundEffectsAudioSource;
        if (source != null && coinChangeClip != null)
            source.PlayOneShot(coinChangeClip, .5f);
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

    private void PlayRegularMusic()
    {
        StopMusic();
        musicMode = MusicMode.Regular;
        if (musicAudioSource != null && musicClips != null && musicClips.Length > 0)
            coroutine = StartCoroutine(PlayRegularPlaylist());
    }

    private IEnumerator ResumeRegularMusicAfterSpecial(MusicMode special)
    {
        while (musicMode == special && musicAudioSource != null && (AudioListener.pause || musicAudioSource.isPlaying))
            yield return null;
        if (musicMode != special) yield break;
        coroutine = null;
        PlayRegularMusic();
    }

    private IEnumerator PlayRegularPlaylist()
    {
        while (musicMode == MusicMode.Regular)
        {
            AudioClip next = GetNextRegularClip();
            if (next == null) yield break;

            musicAudioSource.clip = next;
            musicAudioSource.volume = 0f;
            musicAudioSource.Play();
            float fade = 0f;
            while (musicMode == MusicMode.Regular && (AudioListener.pause || musicAudioSource.isPlaying) && fade < 1f)
            {
                if (!AudioListener.pause) fade += Time.unscaledDeltaTime;
                musicAudioSource.volume = musicVolume * Mathf.Clamp01(fade);
                yield return null;
            }
            musicAudioSource.volume = musicVolume;
            while (musicMode == MusicMode.Regular && (AudioListener.pause || musicAudioSource.isPlaying))
                yield return null;
            if (musicMode != MusicMode.Regular) yield break;
            if (Random.value < pauseChance && pauseSeconds > 0f)
                yield return new WaitForSecondsRealtime(pauseSeconds);
            else yield return null;
        }
    }

    private AudioClip GetNextRegularClip()
    {
        int chosen = -1, candidates = 0;
        for (int i = 0; i < musicClips.Length; i++)
        {
            AudioClip clip = musicClips[i];
            if (clip == null || clip == introMusic || clip == attackMusic || clip == interstellarMusic || i == lastMusicNumber) continue;
            if (Random.Range(0, ++candidates) == 0) chosen = i;
        }
        if (chosen < 0)
            for (int i = 0; i < musicClips.Length; i++)
                if (musicClips[i] != null && musicClips[i] != introMusic && musicClips[i] != attackMusic && musicClips[i] != interstellarMusic)
                { chosen = i; break; }
        if (chosen < 0) return null;
        lastMusicNumber = chosen;
        return musicClips[chosen];
    }

    private void StopMusic()
    {
        if (coroutine != null) StopCoroutine(coroutine);
        coroutine = null;
        if (musicAudioSource == null) return;
        musicAudioSource.Stop();
        musicAudioSource.loop = false;
        musicAudioSource.volume = musicVolume;
    }
}
