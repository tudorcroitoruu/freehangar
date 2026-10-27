using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInteractorTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private PlayerInteractor interactor;
    private readonly Vector3 origin = new Vector3(10000, 10000, 10000);

    [SetUp]
    public void SetUp()
    {
        var actor = Create("Interactor", origin);
        actor.AddComponent<Camera>();
        interactor = actor.AddComponent<PlayerInteractor>();
        // Edit Mode does not call Awake on these components.
        SetField(interactor, "viewCamera", actor.GetComponent<Camera>());
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) Object.DestroyImmediate(go);
        objects.Clear();
    }

    [Test]
    public void FindsInteractionOnColliderParentAndIgnoresPlayerColliders()
    {
        interactor.gameObject.AddComponent<BoxCollider>();
        var target = Create("Target", origin + Vector3.forward * 2).AddComponent<EventInteractable>();
        var child = Create("Collider", target.transform.position);
        child.transform.SetParent(target.transform);
        child.AddComponent<BoxCollider>();
        Assert.AreSame(target, FindTarget());
    }

    [Test]
    public void WallBlocksInteractionBehindIt()
    {
        CreateTarget(3);
        Create("Wall", origin + Vector3.forward).AddComponent<BoxCollider>();
        Assert.IsNull(FindTarget());
    }

    [Test]
    public void DisabledInteractionDoesNotExposeObjectBehindIt()
    {
        CreateTarget(3);
        CreateTarget(1).enabled = false;
        Assert.IsNull(FindTarget());
    }

    [Test]
    public void ObjectsOutsideReachCannotBeSelected()
    {
        CreateTarget(6);
        Assert.IsNull(FindTarget());
    }

    [Test]
    public void UnavailableEventCannotRunAndCanBeEnabledAgain()
    {
        var target = CreateTarget(2);
        int calls = 0;
        var callback = new UnityEvent();
        callback.AddListener(() => calls++);
        SetField(target, "onInteract", callback);
        target.SetAvailable(false);
        Assert.IsFalse(target.TryGetPrompt(interactor, out _));
        Assert.IsFalse(target.TryInteract(interactor));
        Assert.AreEqual(0, calls);
        target.SetAvailable(true);
        Assert.IsTrue(target.TryGetPrompt(interactor, out var prompt));
        Assert.IsTrue(prompt.ShowKey);
        Assert.IsFalse(prompt.ShowProgress);
        Assert.IsTrue(target.TryInteract(interactor));
        Assert.AreEqual(1, calls);
    }

    private EventInteractable CreateTarget(float distance)
    {
        var go = Create("Target", origin + Vector3.forward * distance);
        go.AddComponent<BoxCollider>();
        return go.AddComponent<EventInteractable>();
    }

    [Test]
    public void FocusMovesOutlineToNextObjectAndClearsForObjectsWithoutHighlight()
    {
        var first = CreateTarget(1);
        var second = CreateTarget(2);
        var firstOutline = AddHighlight(first);
        var secondOutline = AddHighlight(second);

        Focus(first);
        Assert.IsTrue(firstOutline.enabled);
        Assert.IsFalse(secondOutline.enabled);
        Focus(second);
        Assert.IsFalse(firstOutline.enabled);
        Assert.IsTrue(secondOutline.enabled);
        Focus(CreateTarget(3));
        Assert.IsFalse(secondOutline.enabled);
    }

    [Test]
    public void DisablingInteractorClearsOutline()
    {
        var target = CreateTarget(1);
        var outline = AddHighlight(target);
        Focus(target);
        typeof(PlayerInteractor).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, null);
        Assert.IsFalse(outline.enabled);
    }

    [Test]
    public void DisabledHighlightCannotBeFocused()
    {
        var target = CreateTarget(1);
        var outline = AddHighlight(target);
        target.GetComponent<InteractionHighlight>().enabled = false;
        Focus(target);
        Assert.IsFalse(outline.enabled);
    }

    [Test]
    public void FocusControlsAllOutlinesAndIgnoresMissingEntries()
    {
        var target = CreateTarget(1);
        var first = AddHighlight(target);
        var model = Create("Second model", target.transform.position);
        model.transform.SetParent(target.transform);
        var second = model.AddComponent<Outline>();
        second.enabled = false;
        SetField(target.GetComponent<InteractionHighlight>(), "outlines", new[] { first, null, second });

        Focus(target);
        Assert.IsTrue(first.enabled);
        Assert.IsTrue(second.enabled);
        Focus(null);
        Assert.IsFalse(first.enabled);
        Assert.IsFalse(second.enabled);
    }

    private Outline AddHighlight(EventInteractable target)
    {
        // Allow the model/Outline to live on a child of the interactive root.
        var model = Create("Model", target.transform.position);
        model.transform.SetParent(target.transform);
        var outline = model.AddComponent<Outline>();
        outline.enabled = false;
        var highlight = target.gameObject.AddComponent<InteractionHighlight>();
        SetField(highlight, "outlines", new[] { outline });
        return outline;
    }

    private void Focus(IInteractable target) =>
        typeof(PlayerInteractor).GetMethod("SetHighlightedTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, new object[] { target });

    private GameObject Create(string name, Vector3 position)
    {
        var go = new GameObject(name);
        objects.Add(go);
        go.transform.position = position;
        return go;
    }

    private IInteractable FindTarget()
    {
        Physics.SyncTransforms();
        return (IInteractable)typeof(PlayerInteractor).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, null);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
