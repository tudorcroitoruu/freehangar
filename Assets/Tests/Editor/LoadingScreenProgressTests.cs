using System.Reflection;
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
