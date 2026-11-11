using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInteractorTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private PlayerInteractor interactor;
    private PlayerData previousData;
    private readonly List<ScriptableObject> testSettings = new List<ScriptableObject>();
    private readonly Vector3 origin = new Vector3(10000, 10000, 10000);

    [SetUp]
    public void SetUp()
    {
        previousData = SaveSystem.Data;
        SaveSystem.Data = new PlayerData();
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
        foreach (var settings in testSettings) Object.DestroyImmediate(settings);
        testSettings.Clear();
        SaveSystem.Data = previousData;
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
    public void WheelbarrowUsesXForUpgradeAndYForPushing()
    {
        var body = Create("Wheelbarrow station", origin + Vector3.forward * 2);
        var wheelbarrow = body.AddComponent<WheelbarrowCargo>();
        var upgrade = body.AddComponent<VehicleUpgradeInteractable>();
        ConfigureVehicle(body);
        var collider = body.AddComponent<BoxCollider>();
        var menu = Create("Upgrade manager", origin);
        menu.SetActive(false);
        var manager = menu.AddComponent<UpgradeManager>();
        SetField(upgrade, "upgradeManager", manager);
        Assert.AreSame(upgrade, FindTarget());
        Assert.IsTrue(upgrade.TryGetPrompt(interactor, out var prompt));
        object[] args = { upgrade, prompt };
        typeof(PlayerInteractor).GetMethod("AddActions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, args);
        prompt = (InteractionPromptData)args[1];
        Assert.AreEqual(KeyCode.E, prompt.InteractionKey);
        Assert.AreEqual(1, prompt.AdditionalActions.Length);
        Assert.AreEqual(KeyCode.F, prompt.AdditionalActions[0].Key);
        Assert.AreSame(upgrade, VehicleUpgradeInteractable.FindForHit(collider, interactor));
        upgrade.enabled = false;
        Assert.IsNull(VehicleUpgradeInteractable.FindForHit(collider, interactor));
        Assert.AreSame(body.GetComponent<WheelbarrowCargo>(), FindTarget());
    }

    [Test]
    public void UpgradeActionOnChildColliderCoexistsWithTheParentVehicleAction()
    {
        var body = Create("Parent wheelbarrow", origin + Vector3.forward * 2);
        var wheelbarrow = body.AddComponent<WheelbarrowCargo>();
        var child = Create("Upgrade collider", body.transform.position);
        child.transform.SetParent(body.transform);
        child.AddComponent<BoxCollider>();
        var upgrade = child.AddComponent<VehicleUpgradeInteractable>();
        ConfigureVehicle(body);
        SetField(upgrade, "targetVehicle", body);
        var menu = Create("Upgrade manager", origin);
        menu.SetActive(false);
        SetField(upgrade, "upgradeManager", menu.AddComponent<UpgradeManager>());
        Assert.AreSame(upgrade, FindTarget());
        upgrade.TryGetPrompt(interactor, out var prompt);
        object[] args = { upgrade, prompt };
        typeof(PlayerInteractor).GetMethod("AddActions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, args);
        prompt = (InteractionPromptData)args[1];
        Assert.AreEqual(KeyCode.F, prompt.AdditionalActions[0].Key);
    }

    [Test]
    public void StandaloneUpgradeStationUsesThePrimaryInteraction()
    {
        var body = Create("Upgrade station", origin + Vector3.forward * 2);
        var station = body.AddComponent<VehicleUpgradeInteractable>();
        body.AddComponent<BoxCollider>();
        Assert.AreSame(station, FindTarget());
        Assert.AreEqual(KeyCode.E, station.InteractionKey);
    }

    [Test]
    public void CargoOnUpgradeVehicleStillUsesItsOwnInteraction()
    {
        var body = Create("Vehicle station", origin + Vector3.forward * 2);
        body.AddComponent<VehicleUpgradeInteractable>();
        var bag = Create("Loaded bag", body.transform.position);
        bag.transform.SetParent(body.transform);
        var container = bag.AddComponent<GrainContainer>();
        var collider = bag.AddComponent<BoxCollider>();
        Assert.IsNull(VehicleUpgradeInteractable.FindForHit(collider, interactor));
        Assert.AreSame(container, FindTarget());
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

    [Test]
    public void ShopHighlightPreservesStylesWhenOutlinesAreAddedAndReorderedBeforeReenable()
    {
        var target = CreateTarget(1);
        var first = AddHighlight(target);
        first.OutlineColor = Color.red;
        first.OutlineWidth = 3f;
        var highlight = target.GetComponent<InteractionHighlight>();
        highlight.SetShopVisible(true);
        highlight.enabled = false;

        var second = Create("New model", target.transform.position).AddComponent<Outline>();
        second.OutlineColor = Color.blue;
        second.OutlineWidth = 4f;
        SetField(highlight, "outlines", new[] { second, null, first });
        highlight.enabled = true;
        typeof(InteractionHighlight).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(highlight, null);

        Assert.AreEqual(Color.white, first.OutlineColor);
        Assert.AreEqual(Color.white, second.OutlineColor);
        highlight.SetShopVisible(false);
        Assert.AreEqual(Color.red, first.OutlineColor);
        Assert.AreEqual(3f, first.OutlineWidth);
        Assert.AreEqual(Color.blue, second.OutlineColor);
        Assert.AreEqual(4f, second.OutlineWidth);
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

    [Test]
    public void LeavingShopWithVehicleCameraClearsOutlinesWhileBodyStaysBehind()
    {
        var target = CreateTarget(1);
        var outline = AddHighlight(target);
        var shop = Create("Shop area", origin).AddComponent<ShopHighlightArea>();
        var actor = Create("Walking body", origin);
        actor.SetActive(false);
        var player = actor.AddComponent<PlayerController>();
        var camera = Create("Vehicle camera", origin).AddComponent<Camera>();
        player.characterCamera = camera;
        SetField(shop, "player", player);
        SetField(shop, "localBounds", new Bounds(Vector3.zero, Vector3.one * 10));
        SetField(shop, "displays", new[] { target.GetComponent<InteractionHighlight>() });
        var update = typeof(ShopHighlightArea).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        update.Invoke(shop, null);
        Assert.IsTrue(outline.enabled);
        camera.transform.position += Vector3.forward * 30;
        update.Invoke(shop, null);
        Assert.IsFalse(outline.enabled);
        Assert.AreEqual(origin, actor.transform.position);
    }

    private void ConfigureVehicle(GameObject body)
    {
        var settings = ScriptableObject.CreateInstance<VehicleUpgradeSettings>();
        settings.vehicles = new[] { new VehicleUpgradeSettings.VehicleType { id = "test-vehicle", slots = 1, movementSpeed = 1f, price = 0 } };
        testSettings.Add(settings);
        var vehicle = body.AddComponent<VehicleUpgrade>();
        SetField(vehicle, "settings", settings);
        SetField(vehicle, "vehicleId", "test-vehicle");
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
