using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenProgressTests
{
    private GameObject root;
    private LoadingScreen screen, previousInstance;
    private Image image;
    private Material shared;
    private bool previousLoadingFlag;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        previousInstance = LoadingScreen.Instance;
        previousLoadingFlag = LoadingScreen.isLoadingOn;
        root = new GameObject("Loading progress tests", typeof(RectTransform), typeof(Canvas));
        var curtain = new GameObject("Curtain", typeof(RectTransform), typeof(CanvasGroup));
        curtain.transform.SetParent(root.transform);
        screen = curtain.AddComponent<LoadingScreen>();
        var graphic = new GameObject("Progress", typeof(RectTransform), typeof(Image));
        graphic.transform.SetParent(curtain.transform);
        image = graphic.GetComponent<Image>();
        shared = new Material(Shader.Find("UI/Image Margin Fade"));
        shared.SetFloat("_FillAmount", .4f);
        shared.SetFloat("_UseFill", 0);
        image.material = shared;
        typeof(LoadingScreen).GetField("loadingImage", Private).SetValue(screen, image);
        typeof(LoadingScreen).GetMethod("Awake", Private).Invoke(screen, null);
    }

    [TearDown]
    public void TearDown()
    {
        typeof(LoadingScreen).GetMethod("OnDestroy", Private).Invoke(screen, null);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(shared);
        LoadingScreen.Instance = previousInstance;
        LoadingScreen.isLoadingOn = previousLoadingFlag;
    }

    [TestCase(-1f, 0f)]
    [TestCase(.25f, .25f)]
    [TestCase(2f, 1f)]
    public void ProgressUpdatesOnlyTheBarsMaterialAndEnablesShaderFill(float value, float expected)
    {
        screen.SetProgress(value);
        Assert.AreNotSame(shared, image.material);
        Assert.AreEqual(expected, image.material.GetFloat("_FillAmount"));
        Assert.AreEqual(1, image.material.GetFloat("_UseFill"));
        Assert.AreEqual(.4f, shared.GetFloat("_FillAmount"));
        Assert.AreEqual(0, shared.GetFloat("_UseFill"));
        Assert.IsNull(image.sprite);
    }

    [Test]
    public void SharedSceneSwitchWaitsForTheCurtainAndKeepsItVisibleUntilCompletion()
    {
        bool switched=false;
        screen.LoadingOn();
        var transition=(IEnumerator)typeof(LoadingScreen).GetMethod("RunCoveredTransition",Private)
            .Invoke(screen,new object[]{Switch(()=>switched=true),0f});
        Assert.IsTrue(transition.MoveNext());Assert.IsInstanceOf<WaitForEndOfFrame>(transition.Current);
        Assert.IsFalse(switched);Assert.IsTrue(LoadingScreen.isLoadingOn);
        Assert.IsTrue(transition.MoveNext());Assert.IsTrue(switched);Assert.IsTrue(LoadingScreen.isLoadingOn);
        while(transition.MoveNext()){}
        Assert.IsFalse(LoadingScreen.isLoadingOn);Assert.That(screen.GetComponent<CanvasGroup>().alpha,Is.Zero);
    }

    [Test]
    public void FailedSharedSceneSwitchStillClosesTheLoadingCurtain()
    {
        screen.LoadingOn();
        var transition=(IEnumerator)typeof(LoadingScreen).GetMethod("RunCoveredTransition",Private)
            .Invoke(screen,new object[]{Fail(),0f});
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("Test transition failure"));
        while(transition.MoveNext()){}
        Assert.IsFalse(LoadingScreen.isLoadingOn);Assert.That(screen.GetComponent<CanvasGroup>().alpha,Is.Zero);
    }

    private static IEnumerator Switch(System.Action action){action();yield return null;}
    private static IEnumerator Fail(){yield return null;throw new System.InvalidOperationException("Test transition failure");}

    [Test]
    public void LoadingProgressMovesGraduallyAndNeverRegresses()
    {
        screen.LoadingOn();
        screen.SetProgress(.8f);
        Assert.That(image.material.GetFloat("_FillAmount"), Is.Zero);

        var advance = typeof(LoadingScreen).GetMethod("AdvanceProgress", Private);
        advance.Invoke(screen, new object[] { .02f });
        float firstFrame = image.material.GetFloat("_FillAmount");
        Assert.That(firstFrame, Is.GreaterThan(0f).And.LessThan(.8f));

        screen.SetProgress(.1f);
        for (int frame = 0; frame < 60; frame++) advance.Invoke(screen, new object[] { .02f });
        Assert.That(image.material.GetFloat("_FillAmount"), Is.EqualTo(.8f).Within(.001f));
    }

    [Test]
    public void SlowActivationFrameDoesNotSkipTheVisibleProgressAnimation()
    {
        screen.LoadingOn();
        screen.SetProgress(1f);
        typeof(LoadingScreen).GetMethod("AdvanceProgress", Private).Invoke(screen, new object[] { 5f });
        Assert.That(image.material.GetFloat("_FillAmount"), Is.GreaterThan(0f).And.LessThan(1f));
        Assert.IsFalse(screen.IsProgressComplete);
        Assert.IsTrue(LoadingScreen.IsLoading);
    }

    [Test]
    public void RevealWaitsForTheVisibleBarAndKeepsGameplayBlocked()
    {
        screen.LoadingOn();
        var reveal = (IEnumerator)typeof(LoadingScreen).GetMethod("FadeOut", Private)
            .Invoke(screen, new object[] { .35f });
        Assert.IsTrue(reveal.MoveNext());
        Assert.IsNull(reveal.Current);
        Assert.IsTrue(LoadingScreen.IsLoading);
        Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

        var advance = typeof(LoadingScreen).GetMethod("AdvanceProgress", Private);
        for (int frame = 0; frame < 60; frame++) advance.Invoke(screen, new object[] { .02f });
        Assert.That(image.material.GetFloat("_FillAmount"), Is.EqualTo(1f));
        Assert.IsTrue(screen.IsProgressComplete);
        Assert.IsTrue(reveal.MoveNext());
        Assert.IsInstanceOf<WaitForSecondsRealtime>(reveal.Current);
        Assert.IsTrue(LoadingScreen.IsLoading);
    }

    [Test]
    public void EveryLoadingStartResetsFillAndDestroyRestoresSharedMaterial()
    {
        screen.SetProgress(1);
        var runtime = image.material;
        screen.LoadingOn();
        Assert.AreSame(runtime, image.material);
        Assert.AreEqual(0, runtime.GetFloat("_FillAmount"));
        Assert.AreEqual(1, screen.GetComponent<CanvasGroup>().alpha);
        screen.SetProgress(.6f);
        screen.LoadingOff();
        screen.LoadingOn();
        Assert.AreEqual(0, runtime.GetFloat("_FillAmount"));
        typeof(LoadingScreen).GetMethod("OnDestroy", Private).Invoke(screen, null);
        Assert.AreSame(shared, image.material);
        Assert.IsTrue(runtime == null);
    }

    [Test]
    public void ProgressAlsoUpdatesTheCachedStencilMaterial()
    {
        var maskObject = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskObject.transform.SetParent(root.transform);
        image.transform.SetParent(maskObject.transform);
        screen.SetProgress(.25f);
        var rendered = image.materialForRendering;
        Assert.AreNotSame(image.material, rendered);
        Assert.AreEqual(.25f, rendered.GetFloat("_FillAmount"));
        screen.SetProgress(.75f);
        Assert.AreEqual(.75f, image.materialForRendering.GetFloat("_FillAmount"));
        Assert.AreEqual(1, image.materialForRendering.GetFloat("_UseFill"));
        Assert.AreEqual(.4f, shared.GetFloat("_FillAmount"));
    }
}
