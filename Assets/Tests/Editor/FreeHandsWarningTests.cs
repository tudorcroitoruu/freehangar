using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FreeHandsWarningTests
{
    private GameObject root;
    private GameplayCanvas canvas;
    private CanvasGroup warning;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Free hands warning test");
        canvas = root.AddComponent<GameplayCanvas>();
        var warningObject = new GameObject("Warning", typeof(CanvasGroup));
        warningObject.transform.SetParent(root.transform);
        warning = warningObject.GetComponent<CanvasGroup>();
        typeof(GameplayCanvas).GetField("freeHandsWarning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(canvas, warning);
        typeof(GameplayCanvas).GetField("freeHandsFadeDuration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(canvas, .2f);
        Call("ResetFreeHandsWarning");
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void WarningFadesInAndOutWithoutRestartingOnRepeatedRequests()
    {
        Assert.AreEqual(0f, warning.alpha);
        canvas.SetFreeHandsWarning(true);
        Call("FadeFreeHandsWarning", .1f);
        Assert.That(warning.alpha, Is.EqualTo(.5f).Within(.001f));
        canvas.SetFreeHandsWarning(true);
        Call("FadeFreeHandsWarning", .1f);
        Assert.AreEqual(1f, warning.alpha);
        Assert.IsFalse(warning.interactable);
        Assert.IsFalse(warning.blocksRaycasts);
        canvas.SetFreeHandsWarning(false);
        Call("FadeFreeHandsWarning", .1f);
        Assert.That(warning.alpha, Is.EqualTo(.5f).Within(.001f));
        Call("FadeFreeHandsWarning", .1f);
        Assert.AreEqual(0f, warning.alpha);
    }

    [Test]
    public void ResetHidesWarningAndClearsItsRequestedState()
    {
        canvas.SetFreeHandsWarning(true);
        Call("FadeFreeHandsWarning", 1f);
        Assert.AreEqual(1f, warning.alpha);
        Call("ResetFreeHandsWarning");
        Call("FadeFreeHandsWarning", 1f);
        Assert.AreEqual(0f, warning.alpha);
    }

    [TestCase(true, true)]
    [TestCase(false, true)]
    public void BlockedPromptHidesPanelAndClearsDescription(bool showKey, bool requiresFreeHands)
    {
        var panel = new GameObject("Interaction panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform);
        var prompt = panel.AddComponent<InteractionPrompt>();
        var textObject = new GameObject("Description", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform);
        var description = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        description.text = "Old interaction";
        var cameraObject = new GameObject("Prompt camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(InteractionPrompt).GetField("panel", flags).SetValue(prompt, panel.GetComponent<RectTransform>());
        typeof(InteractionPrompt).GetField("canvasRect", flags).SetValue(prompt, panel.GetComponent<RectTransform>());
        typeof(InteractionPrompt).GetField("description", flags).SetValue(prompt, description);
        var data = new InteractionPromptData(Vector3.forward, "Blocked interaction", showKey);
        data.RequiresFreeHands = requiresFreeHands;
        prompt.Show(cameraObject.GetComponent<Camera>(), data);
        Assert.IsFalse(panel.activeSelf);
        Assert.AreEqual("", prompt.CurrentText);
        Assert.AreEqual("", description.text);
    }

    private void Call(string method, params object[] args) =>
        typeof(GameplayCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, args);
}
