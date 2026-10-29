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

    [Test]
    public void PromptShowsSeparateKeyRowsAndRemovesThemForTheNextObject()
    {
        var canvasObject = new GameObject("Prompt canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(root.transform);
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600, 600);
        var panelObject = new GameObject("Prompt row", typeof(RectTransform));
        panelObject.transform.SetParent(canvasRect);
        var panel = panelObject.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(200, 50);
        var prompt = canvasObject.AddComponent<InteractionPrompt>();
        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        descriptionObject.transform.SetParent(panel);
        var keyObject = new GameObject("Key", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        keyObject.transform.SetParent(panel);
        var mouse = new GameObject("Mouse");
        mouse.transform.SetParent(panel);
        var cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(InteractionPrompt).GetField("panel", flags).SetValue(prompt, panel);
        typeof(InteractionPrompt).GetField("canvasRect", flags).SetValue(prompt, canvasRect);
        typeof(InteractionPrompt).GetField("description", flags).SetValue(prompt, descriptionObject.GetComponent<TMPro.TMP_Text>());
        typeof(InteractionPrompt).GetField("keycap", flags).SetValue(prompt, keyObject.GetComponent<TMPro.TMP_Text>());
        typeof(InteractionPrompt).GetField("mouseLeft", flags).SetValue(prompt, mouse);
        var data = new InteractionPromptData(Vector3.forward, "Ride bicycle");
        data.AdditionalActions = new[] { new InteractionPromptAction("Upgrade vehicle", KeyCode.F), new InteractionPromptAction("Inspect", KeyCode.H) };
        prompt.Show(cameraObject.GetComponent<Camera>(), data);
        Assert.IsTrue(prompt.IsVisible);
        Assert.AreEqual("E", keyObject.GetComponent<TMPro.TMP_Text>().text);
        Assert.IsFalse(mouse.activeSelf);
        var rows = (System.Collections.IList)typeof(InteractionPrompt).GetField("actionRows", flags).GetValue(prompt);
        Assert.AreEqual(2, rows.Count);
        var rowKey = (TMPro.TMP_Text)rows[0].GetType().GetField("key").GetValue(rows[0]);
        var rowPanel = (RectTransform)rows[0].GetType().GetField("panel").GetValue(rows[0]);
        Assert.AreEqual("F", rowKey.text);
        Assert.IsTrue(rowPanel.gameObject.activeSelf);
        Assert.Less(rowPanel.anchoredPosition.y, panel.anchoredPosition.y);
        prompt.Show(cameraObject.GetComponent<Camera>(), new InteractionPromptData(Vector3.forward, "Take"));
        Assert.IsFalse(rowPanel.gameObject.activeSelf);
        data.AdditionalActions = null;
        data.InteractionKey = KeyCode.F;
        prompt.Show(cameraObject.GetComponent<Camera>(), data);
        Assert.AreEqual("F", keyObject.GetComponent<TMPro.TMP_Text>().text);
        prompt.Hide();
        Assert.IsFalse(prompt.IsVisible);
        Assert.IsFalse(rowPanel.gameObject.activeSelf);
    }

    private void Call(string method, params object[] args) =>
        typeof(GameplayCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, args);
}
