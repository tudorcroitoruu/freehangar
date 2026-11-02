using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class WarningPopupTests
{
    private GameObject root, first, second;
    private WarningPopup popup;
    private CanvasGroup group;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Warning", typeof(RectTransform), typeof(CanvasGroup));
        popup = root.AddComponent<WarningPopup>();
        group = root.GetComponent<CanvasGroup>();
        Set("warningCanvasGroup", group);
        first = new GameObject("First source");
        second = new GameObject("Second source");
        popup.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        if (first != null) Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }

    [Test]
    public void MultipleAttackersKeepOneWarningUntilLastSourceStops()
    {
        popup.SetWarning(WarningType.RatsAttack, first, true);
        popup.SetWarning(WarningType.RatsAttack, second, true);
        popup.Tick(.2f);
        popup.SetWarning(WarningType.RatsAttack, first, false);
        popup.Tick(0f);
        Assert.AreEqual(WarningType.RatsAttack, popup.CurrentType);
        Assert.AreEqual(1f, group.alpha);
        popup.SetWarning(WarningType.RatsAttack, second, false);
        popup.Tick(.2f);
        Assert.IsNull(popup.CurrentType);
        Assert.AreEqual(0f, group.alpha);
    }

    [Test]
    public void RepeatedRequestsDoNotRestartRotationAndDestroyedSourcesAreRemoved()
    {
        popup.SetWarning(WarningType.RatsAttack, first, true);
        popup.SetWarning(WarningType.CrowsAttack, second, true);
        popup.Tick(0f);
        for (int i = 0; i < 3; i++)
        {
            popup.SetWarning(WarningType.RatsAttack, first, true);
            popup.Tick(1f);
        }
        Assert.AreEqual(WarningType.CrowsAttack, popup.CurrentType);
        popup.Tick(3f);
        Assert.AreEqual(WarningType.RatsAttack, popup.CurrentType);
        Object.DestroyImmediate(first);
        popup.Tick(0f);
        Assert.AreEqual(WarningType.CrowsAttack, popup.CurrentType);
    }

    [Test]
    public void ContextWarningPreemptsAttackThenReturnsToItWhenAimChanges()
    {
        popup.SetWarning(WarningType.CrowsAttack, first, true);
        popup.Tick(.2f);
        popup.SetWarning(WarningType.VehicleFull, second, true);
        popup.Tick(0f);
        Assert.AreEqual(WarningType.VehicleFull, popup.CurrentType);
        popup.Tick(3f);
        Assert.AreEqual(WarningType.CrowsAttack, popup.CurrentType);
        popup.SetWarning(WarningType.VehicleFull, second, false);
        popup.Tick(3f);
        Assert.AreEqual(WarningType.CrowsAttack, popup.CurrentType);
    }

    [Test]
    public void TimedHangarWarningRefreshesWithoutDuplicatingAndExpires()
    {
        popup.SetWarning(WarningType.RatsAttack, first, true);
        popup.Tick(0f);
        popup.ShowWarning(WarningType.BlockHangarEntry);
        popup.Tick(1f);
        popup.ShowWarning(WarningType.BlockHangarEntry);
        popup.Tick(1.5f);
        Assert.AreEqual(WarningType.BlockHangarEntry, popup.CurrentType);
        popup.Tick(.6f);
        Assert.AreEqual(WarningType.RatsAttack, popup.CurrentType);
    }

    [Test]
    public void InactivePopupRendersPresetAndFadesWithoutBlockingInput()
    {
        var titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(root.transform);
        var title = titleObject.GetComponent<Text>();
        Set("warningTitle", title);
        Set("warningPresets", new[] { new WarningPresets { warningType = WarningType.VehicleFull, title = "Full" } });
        root.SetActive(false);
        popup.SetWarning(WarningType.VehicleFull, first, true);
        popup.Tick(.1f);
        Assert.IsTrue(root.activeSelf);
        Assert.AreEqual("Full", title.text);
        Assert.That(group.alpha, Is.EqualTo(.5f).Within(.001f));
        Assert.IsFalse(group.blocksRaycasts);
        Assert.IsFalse(group.interactable);
        popup.Tick(0f);
        Assert.That(group.alpha, Is.EqualTo(.5f).Within(.001f));
        popup.Tick(.1f);
        Assert.AreEqual(1f, group.alpha);
    }

    private void Set(string field, object value) => typeof(WarningPopup)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popup, value);
}
