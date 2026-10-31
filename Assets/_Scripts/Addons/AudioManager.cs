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
    private enum MusicMode { None, Regular, Intro, Attack, Ending }
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

    private void Start()
    {
        if (musicMode != MusicMode.None) return;
        if (SceneManager.GetActiveScene().name == "Last Scene") PlayInterstellar();
        else PlayRegularMusic();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Last Scene") PlayInterstellar();
        else if (musicMode == MusicMode.Ending || musicMode == MusicMode.None) PlayRegularMusic();
    }

    public void PlayIntroOnce()
    {
        if (introPlayed || introMusic == null || musicAudioSource == null) return;
        introPlayed = true;
        StopMusic();
        musicMode = MusicMode.Intro;
        musicAudioSource.clip = introMusic;
        musicAudioSource.Play();
        coroutine = StartCoroutine(ResumeRegularMusicAfterSpecial(MusicMode.Intro));
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
        while (musicMode == special && musicAudioSource != null && musicAudioSource.isPlaying)
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
            while (musicMode == MusicMode.Regular && musicAudioSource.isPlaying && fade < 1f)
            {
                fade += Time.unscaledDeltaTime;
                musicAudioSource.volume = musicVolume * Mathf.Clamp01(fade);
                yield return null;
            }
            musicAudioSource.volume = musicVolume;
            while (musicMode == MusicMode.Regular && musicAudioSource.isPlaying)
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
