using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class OpeningPresentationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private GameIntroduction intro;
    private AudioManager audio, previousAudio;
    private AudioSource source;
    private AudioClip music;
    private GameManager manager, previousManager;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Opening presentation tests");
        intro = root.AddComponent<GameIntroduction>();
        previousManager = GameManager.Instance;
        manager = root.AddComponent<GameManager>();
        Set(manager, "introduction", intro);
        GameManager.Instance = manager;
        var player = Child("Player").AddComponent<PlayerController>();
        player.characterCamera = Child("Camera").AddComponent<Camera>();
        player.characterCamera.enabled = false;
        Set(intro, "player", player);
        Set(intro, "bicycle", Child("Bicycle").AddComponent<BicycleController>());
        foreach (string field in new[] { "landscapeStart", "landscapeEnd", "arrival", "grainLook", "shovelLook" }) Set(intro, field, Child(field).transform);
        Set(intro, "bicycleRoute", new[] { Vector3.zero, Vector3.forward });
        Set(intro, "purchasePaperPrefab", Child("Paper"));
        previousAudio = AudioManager.Instance;
        audio = Child("Audio").AddComponent<AudioManager>();
        source = audio.gameObject.AddComponent<AudioSource>();
        source.volume = 0; source.playOnAwake = false;
        music = AudioClip.Create("Intro test", 44100, 1, 44100, false);
        Set(audio, "musicAudioSource", source);
        Set(audio, "introMusic", music);
        AudioManager.Instance = null;
        typeof(AudioManager).GetMethod("Awake", Private).Invoke(audio, null);
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name); child.transform.SetParent(root.transform); return child;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private Canvas Cover => (Canvas)typeof(GameIntroduction).GetField("openingFade", Private).GetValue(intro);

    [TearDown]
    public void TearDown()
    {
        intro.Cancel();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(music);
        AudioManager.Instance = previousAudio;
        GameManager.Instance = previousManager;
    }

    [Test]
    public void PreparingBeforeLoadingCompletesCreatesOpaqueCoverWithoutStartingCutscene()
    {
        Assert.IsTrue(intro.PrepareOpening());
        Assert.IsFalse(intro.IsPlaying);
        Assert.IsNotNull(Cover);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, Cover.renderMode);
        Assert.AreEqual(Color.black, Cover.GetComponentInChildren<Image>().color);
        Assert.AreEqual(root.scene, Cover.gameObject.scene);
    }

    [Test]
    public void PreparingTwiceReusesTheCoverAndCancelRemovesItBeforePlayback()
    {
        intro.PrepareOpening(); var cover = Cover;
        intro.PrepareOpening(); Assert.AreSame(cover, Cover);
        intro.Cancel(); Assert.IsNull(Cover); Assert.IsTrue(cover == null);
    }

    [Test]
    public void LateBindingsStillGetBlackCoverAndFailedPlaybackCanReleaseIt()
    {
        Set(intro, "player", null);
        Assert.IsTrue(manager.PrepareIntroductionForReveal());
        Assert.AreEqual(Color.black, Cover.GetComponentInChildren<Image>().color);
        Assert.IsFalse(intro.Play());
        intro.Cancel(); Assert.IsNull(Cover);
    }

    [Test]
    public void GameplaySceneArrivalKeepsIntroMusicWhileWaitingForLoadingReveal()
    {
        Assert.IsTrue(manager.IntroductionPending);
        typeof(AudioManager).GetMethod("PlaySceneMusic", Private).Invoke(audio, new object[] { "Gameplay" });
        Assert.AreSame(music, source.clip);
        Assert.IsTrue(source.loop);
        Assert.IsFalse((bool)typeof(AudioManager).GetField("introPlayed", Private).GetValue(audio));
    }

    [Test]
    public void SkippingIntroDoesNotCreateAnOpeningCover()
    {
        manager.skipIntroCutscene = true;
        Assert.IsFalse(manager.IntroductionPending);
        Assert.IsFalse(manager.PrepareIntroductionForReveal());
        Assert.IsNull(Cover);
    }

    [Test]
    public void AnotherNewGameCanRestartIntroOnThePersistentAudioManager()
    {
        Set(audio, "introPlayed", true);
        audio.PlayIntroOnce(restart: true);
        Assert.AreSame(music, source.clip);
        Assert.IsFalse(source.loop);
    }

    [TestCase("Bootstrap")]
    [TestCase("Menu")]
    [TestCase("LoadingScene")]
    public void FrontEndSceneRoutingStartsIntroInsteadOfRegularPlaylist(string sceneName)
    {
        typeof(AudioManager).GetMethod("PlaySceneMusic", Private).Invoke(audio, new object[] { sceneName });
        Assert.AreSame(music, source.clip);
        Assert.IsTrue(source.loop);
        Assert.IsFalse((bool)typeof(AudioManager).GetField("introPlayed", Private).GetValue(audio));
    }
}
