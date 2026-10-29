using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerItemCarryTests
{
    private GameObject actor, item, secondItem, ground, wall;
    private PlayerItemCarry carrier;
    private Transform instrument;

    [SetUp]
    public void SetUp()
    {
        actor = new GameObject("Inventory carrier test");
        var player = actor.AddComponent<PlayerController>();
        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(actor.transform);
        player.characterCamera = cameraObject.AddComponent<Camera>();
        instrument = new GameObject("Instrument").transform;
        instrument.SetParent(actor.transform);
        SetField(player, "heldInstrument", instrument);
        carrier = actor.AddComponent<PlayerItemCarry>();
        typeof(PlayerItemCarry).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(carrier, null);
        item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.transform.localScale = new Vector3(1, 2, 1);
        item.AddComponent<Rigidbody>();
    }

    [TearDown]
    public void TearDown()
    {
        if (item != null) carrier.CancelTake(item);
        Object.DestroyImmediate(item);
        Object.DestroyImmediate(secondItem);
        Object.DestroyImmediate(actor);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(wall);
    }

    [Test]
    public void HoldingDisablesPhysicsAndToolAndReleaseRestoresThem()
    {
        var collider = item.GetComponent<Collider>();
        var body = item.GetComponent<Rigidbody>();
        Assert.IsTrue(carrier.TryTake(item));
        Assert.IsTrue(carrier.IsCarrying);
        Assert.IsTrue(item.transform.IsChildOf(actor.transform));
        Assert.IsFalse(collider.enabled);
        Assert.IsTrue(body.isKinematic);
        Assert.IsFalse(instrument.gameObject.activeSelf);

        carrier.CancelTake(item);
        Assert.IsFalse(carrier.IsCarrying);
        Assert.IsNull(item.transform.parent);
        Assert.IsTrue(collider.enabled);
        Assert.IsFalse(body.isKinematic);
        Assert.IsTrue(instrument.gameObject.activeSelf);
        Assert.That(Vector3.Distance(new Vector3(1, 2, 1), item.transform.lossyScale), Is.LessThan(.001f));
    }

    [Test]
    public void CannotReplaceHeldItemWithAnotherOne()
    {
        secondItem = new GameObject("Second inventory item");
        Assert.IsTrue(carrier.TryTake(item));
        Assert.IsFalse(carrier.CanTake);
        Assert.IsFalse(carrier.TryTake(secondItem));
        Assert.AreSame(item, carrier.CarriedObject);
        Assert.IsNull(secondItem.transform.parent);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(50f)]
    public void ContainerCanBeCarriedAtEveryFillLevelWithoutLosingContents(float kilograms)
    {
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", kilograms);
        Assert.IsTrue(container.CanCarry);
        Assert.IsTrue(carrier.TryTake(item));
        Assert.IsTrue(container.IsCarried);
        Assert.AreEqual(kilograms, container.Kilograms);
        carrier.CancelTake(item);
        Assert.IsFalse(container.IsCarried);
        Assert.AreEqual(kilograms, container.Kilograms);
        Assert.IsTrue(carrier.TryTake(item));
    }

    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(50f)]
    public void NonPortableContainerCannotBeCarriedAtAnyFillLevel(float kilograms)
    {
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", kilograms);
        SetField(container, "canBeCarried", false);
        Assert.IsFalse(container.CanCarry);
        Assert.IsFalse(carrier.TryTake(item));
        Assert.IsFalse(container.IsCarried);
        Assert.IsFalse(carrier.IsCarrying);
        Assert.AreEqual(kilograms, container.Kilograms);
        Assert.IsNull(item.transform.parent);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(50f)]
    public void WorldContainerPromptAllowsTakingAtEveryFillLevel(float kilograms)
    {
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", kilograms);
        var interaction = actor.AddComponent<BagInteraction>();
        Assert.IsTrue(interaction.TryGetPrompt(container, out var prompt));
        Assert.AreEqual("Take", prompt.Action);
        Assert.IsTrue(prompt.ShowKey);
        SetField(container, "canBeCarried", false);
        Assert.IsTrue(interaction.TryGetPrompt(container, out prompt));
        Assert.IsFalse(prompt.ShowKey);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(50f)]
    public void TakingContainerPutsShovelAwayAndKeepsItUnequippedAfterRelease(float kilograms)
    {
        var player = actor.GetComponent<PlayerController>();
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(player, "shovelTool", shovel);
        var interactor = actor.AddComponent<PlayerInteractor>();
        typeof(PlayerInteractor).GetProperty("Items").SetValue(interactor, carrier);
        SetField(player, "interactor", interactor);
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", kilograms);

        Assert.IsTrue(player.IsShovelEquipped);
        Assert.IsTrue(carrier.CanTake);
        Assert.IsTrue(carrier.TryTake(item));
        Assert.IsFalse(player.IsShovelEquipped);
        Assert.IsFalse(shovel.enabled);
        Assert.IsFalse(instrument.gameObject.activeSelf);
        Assert.IsFalse(player.ToggleShovel());
        carrier.CancelTake(item);
        Assert.IsFalse(player.IsShovelEquipped);
        Assert.IsFalse(instrument.gameObject.activeSelf);
        Assert.AreEqual(kilograms, container.Kilograms);
        Assert.IsTrue(player.ToggleShovel());
        Assert.IsTrue(player.IsShovelEquipped);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(50f)]
    public void WorldPickupAutomaticallyPutsShovelAway(float kilograms)
    {
        var player = actor.GetComponent<PlayerController>();
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(player, "shovelTool", shovel);
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", kilograms);
        var interaction = actor.AddComponent<BagInteraction>();
        SetField(interaction, "viewCamera", player.characterCamera);
        SetField(interaction, "carryAnchor", player.characterCamera.transform);
        SetField(interaction, "shovel", shovel);

        interaction.TryGetPrompt(container, out var prompt);
        Assert.AreEqual("Take", prompt.Action);
        Assert.IsTrue(prompt.ShowKey);
        Assert.IsFalse(prompt.RequiresFreeHands);
        Assert.IsTrue(interaction.TryInteract(container));
        Assert.IsFalse(player.IsShovelEquipped);
        Assert.IsFalse(shovel.enabled);
        Assert.IsTrue(container.IsCarried);
        Assert.AreEqual(kilograms, container.Kilograms);
    }

    [Test]
    public void ContainerPourUsesDropActionInsteadOfScoopAction()
    {
        var player = actor.GetComponent<PlayerController>();
        actor.transform.position = new Vector3(10000f, 10000f, 10000f);
        item.transform.position = actor.transform.position + Vector3.forward * 2f;
        var container = item.AddComponent<GrainContainer>();
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(shovel, "bladeTip", instrument);
        typeof(ShovelTool).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shovel, null);
        shovel.Cargo.Add(GrainType.Corn, 2f);
        Physics.SyncTransforms();

        Assert.IsTrue(shovel.TryUse(player.characterCamera, actor.transform));
        shovel.Tick(2f);
        Assert.AreEqual(0f, container.Kilograms);
        Assert.AreEqual(2f, shovel.Kilograms);

        Assert.IsTrue(shovel.TryDrop(player.characterCamera, actor.transform));
        shovel.Tick(2f);
        Assert.That(container.Kilograms, Is.EqualTo(2f).Within(.0001f));
        Assert.That(shovel.Kilograms, Is.LessThan(.0001f));
    }

    [TestCase(false, 2f, false, 0f, false)]
    [TestCase(true, 0f, false, 0f, false)]
    [TestCase(true, 2f, false, 0f, true)]
    [TestCase(true, 2f, true, 0f, false)]
    [TestCase(true, 2f, false, 50f, false)]
    public void ContainerPromptChoosesOneInputBasedOnAcceptedShovelCargo(
        bool equipped, float load, bool differentGrain, float filled, bool showMouse)
    {
        var player = actor.GetComponent<PlayerController>();
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(player, "shovelTool", shovel);
        shovel.Cargo.Add(GrainType.Corn, load);
        shovel.enabled = equipped;
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", filled);
        SetField(container, "restrictGrain", differentGrain);
        SetField(container, "acceptedGrain", differentGrain ? GrainType.Wheat : GrainType.Corn);
        var bags = actor.AddComponent<BagInteraction>();
        SetField(bags, "shovel", shovel);

        Assert.IsTrue(bags.TryGetPrompt(container, out var prompt));
        Assert.AreEqual(showMouse, prompt.ShowPrimaryButton);
        Assert.AreEqual(!showMouse, prompt.ShowKey);
        Assert.AreEqual(showMouse ? "Fill" : "Take", prompt.Action);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EquippingShovelPlacesHeldObjectOnGround(bool bag)
    {
        var player = PrepareShovelAndGround();
        var container = item.AddComponent<GrainContainer>();
        SetField(container, "kilograms", 25f);
        BagInteraction bags = null;
        if (bag)
        {
            bags = actor.AddComponent<BagInteraction>();
            SetField(player, "bagInteraction", bags);
            SetField(bags, "viewCamera", player.characterCamera);
            SetField(bags, "carryAnchor", player.characterCamera.transform);
            SetField(bags, "shovel", player.Shovel);
            Assert.IsTrue(bags.TryInteract(container));
        }
        else Assert.IsTrue(carrier.TryTake(item));

        Assert.IsTrue(player.IsCarrying);
        Assert.IsTrue(player.ToggleShovel());
        Assert.IsFalse(player.IsCarrying);
        Assert.IsFalse(container.IsCarried);
        Assert.IsNull(item.transform.parent);
        Assert.IsTrue(item.GetComponent<Collider>().enabled);
        Assert.AreEqual(25f, container.Kilograms);
        Assert.IsTrue(player.IsShovelEquipped);
    }

    [Test]
    public void BlockedPlacementKeepsObjectHeldAndShovelUnequipped()
    {
        var player = PrepareShovelAndGround();
        wall = new GameObject("Placement blocker");
        wall.transform.position = actor.transform.position + Vector3.forward * .8f + Vector3.up * 1.5f;
        wall.transform.localScale = new Vector3(10, 3, .15f);
        wall.AddComponent<BoxCollider>();
        Assert.IsTrue(carrier.TryTake(item));
        Physics.SyncTransforms();
        Assert.IsFalse(player.ToggleShovel());
        Assert.IsTrue(carrier.IsCarrying);
        Assert.IsFalse(player.IsShovelEquipped);
    }

    private PlayerController PrepareShovelAndGround()
    {
        actor.transform.position = new Vector3(10000, 10000, 10000);
        item.transform.position = actor.transform.position;
        var player = actor.GetComponent<PlayerController>();
        player.characterCamera.transform.localPosition = Vector3.up * 1.6f;
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(player, "shovelTool", shovel);
        var interactor = actor.AddComponent<PlayerInteractor>();
        typeof(PlayerInteractor).GetProperty("Items").SetValue(interactor, carrier);
        SetField(player, "interactor", interactor);
        Assert.IsTrue(player.ToggleShovel());
        ground = new GameObject("Placement ground");
        ground.transform.position = actor.transform.position + Vector3.down * .05f;
        ground.transform.localScale = new Vector3(20, .1f, 20);
        ground.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        return player;
    }

    [TestCase(0f)]
    [TestCase(2f)]
    public void InventoryPickupIsAvailableWithShovelAndAutomaticallyPutsItAway(float load)
    {
        var player = actor.GetComponent<PlayerController>();
        var shovel = instrument.gameObject.AddComponent<ShovelTool>();
        SetField(player, "shovelTool", shovel);
        shovel.Cargo.Add(GrainType.Corn, load);
        var interactor = actor.AddComponent<PlayerInteractor>();
        typeof(PlayerInteractor).GetProperty("Items").SetValue(interactor, carrier);
        SetField(player, "interactor", interactor);
        secondItem = new GameObject("Inventory prompt test");
        var product = secondItem.AddComponent<InventoryProduct>();
        SetField(product, "productID", "freeBag");
        SetField(product, "productName", "Free Bag");
        SetField(product, "unlimitedCount", true);
        SetField(product, "objectPrefab", item);
        secondItem.AddComponent<InventoryManager>();

        Assert.IsTrue(product.TryGetPrompt(interactor, out var prompt));
        Assert.IsTrue(prompt.ShowKey);
        Assert.IsTrue(prompt.HasAvailableInput);
        Assert.IsFalse(prompt.ShowPrimaryButton);
        Assert.AreEqual("Take 1 Free Bag", prompt.Action);
        Assert.IsFalse(prompt.RequiresFreeHands);
        Assert.IsTrue(product.TryInteract(interactor));
        Assert.IsFalse(player.IsShovelEquipped);
        var delivered = carrier.CarriedObject;
        carrier.CancelTake(delivered);
        Object.DestroyImmediate(delivered);
        Assert.IsTrue(carrier.TryTake(item));
        product.TryGetPrompt(interactor, out prompt);
        Assert.IsFalse(prompt.ShowKey);
        Assert.IsTrue(prompt.RequiresFreeHands);
    }

    [Test]
    public void InventoryAndWorldPickupUseSamePoseAndKeepImportedBagUpright()
    {
        var player = PrepareShovelAndGround();
        item.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        item.transform.position = actor.transform.position + Vector3.right * 20f;
        item.AddComponent<GrainContainer>();
        var bags = actor.AddComponent<BagInteraction>();
        var anchor = new GameObject("Shared bag anchor").transform;
        anchor.SetParent(player.characterCamera.transform, false);
        anchor.localPosition = new Vector3(.35f, -.35f, 1.05f);
        SetField(bags, "viewCamera", player.characterCamera);
        SetField(bags, "carryAnchor", anchor);
        SetField(bags, "shovel", player.Shovel);
        SetField(player, "bagInteraction", bags);
        var interactor = actor.GetComponent<PlayerInteractor>();
        typeof(PlayerInteractor).GetProperty("Bags").SetValue(interactor, bags);
        secondItem = new GameObject("Rotated inventory shelf");
        secondItem.transform.rotation = Quaternion.Euler(0f, 50f, 0f);
        var product = secondItem.AddComponent<InventoryProduct>();
        SetField(product, "productID", "freeBag");
        SetField(product, "unlimitedCount", true);
        SetField(product, "objectPrefab", item);
        var manager = secondItem.AddComponent<InventoryManager>();
        GameObject delivered = null;
        try
        {
            Assert.IsTrue(product.TryInteract(interactor));
            Assert.IsTrue(bags.IsCarrying);
            Assert.IsFalse(carrier.IsCarrying);
            delivered = bags.CarriedBag.gameObject;
            Assert.AreSame(anchor, delivered.transform.parent);
            Vector3 localPosition = delivered.transform.localPosition;
            Vector3 localScale = delivered.transform.localScale;
            Quaternion localRotation = delivered.transform.localRotation;
            Assert.That(Quaternion.Angle(Quaternion.Euler(-90f, 0f, 0f), localRotation), Is.LessThan(.01f));
            Assert.IsTrue(bags.TryInteract());
            Assert.That(Vector3.Dot(delivered.transform.forward, Vector3.up), Is.GreaterThan(.999f));
            Assert.AreEqual(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ,
                delivered.GetComponent<Rigidbody>().constraints);
            Assert.IsTrue(bags.TryInteract(delivered.GetComponent<GrainContainer>()));
            Assert.That(Vector3.Distance(localPosition, delivered.transform.localPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(localScale, delivered.transform.localScale), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(localRotation, delivered.transform.localRotation), Is.LessThan(.01f));
            Assert.AreEqual(1, manager.SpawnedProducts.Count);
            Assert.AreSame(delivered, manager.SpawnedProducts[0].gameObject);
        }
        finally
        {
            if (delivered != null)
            {
                bags.CancelTake(delivered.GetComponent<GrainContainer>());
                Object.DestroyImmediate(delivered);
            }
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
