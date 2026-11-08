using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SellingZoneContainersOnlyTests
{
    [Test]
    public void SpringTextFollowsDeckAndHeavyLoadCannotBurySurface()
    {
        var station = new GameObject("Spring regression");
        try
        {
            var deckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deckObject.name = "Inset weighing deck";
            deckObject.transform.SetParent(station.transform, false);
            deckObject.transform.localPosition = Vector3.up * .045f;
            deckObject.transform.localScale = new Vector3(3.8f, .09f, 4.8f);
            var text = new GameObject("Deck instruction", typeof(RectTransform)).transform;
            text.SetParent(station.transform, false); text.localPosition = Vector3.up * .105f;
            Vector3 textRest = text.position;
            var spring = station.AddComponent<WeighDeckSpring>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WeighDeckSpring).GetMethod("Start", flags).Invoke(spring, null);
            var visual = deckObject.transform.Find("Suspended deck visual");
            Vector3 visualRest = visual.position;
            typeof(WeighDeckSpring).GetField("pollAt", flags).SetValue(spring, float.PositiveInfinity);
            typeof(WeighDeckSpring).GetField("load", flags).SetValue(spring, 1000f);
            typeof(WeighDeckSpring).GetField("velocity", flags).SetValue(spring, -10f);
            var tick = typeof(WeighDeckSpring).GetMethod("FixedUpdate", flags);
            for (int i = 0; i < 300; i++)
            {
                tick.Invoke(spring, null);
                Vector3 offset = visual.position - visualRest;
                Assert.That(offset.y, Is.GreaterThanOrEqualTo(-.04001f));
                Assert.That(Vector3.Distance(text.position - textRest, offset), Is.LessThan(.0001f));
            }
            // Start was invoked manually in EditMode; exercise its teardown pair too.
            typeof(WeighDeckSpring).GetMethod("OnDestroy", flags).Invoke(spring, null);
            Assert.AreSame(station.transform, text.parent);
            Assert.That(Vector3.Distance(text.position, textRest), Is.LessThan(.0001f));
        }
        finally { Object.DestroyImmediate(station); }
    }

    private GameObject root;
    private SellingZone zone;
    private PlayerInteractor player;
    private GrainContainer container;
    private SellingWeighButton button;
    private readonly List<GrainContainer> collected=new List<GrainContainer>();

    [SetUp]
    public void SetUp()
    {
        root=new GameObject("Containers-only scale fixture");root.transform.position=new Vector3(30000,0,30000);
        var area=root.AddComponent<BoxCollider>();area.isTrigger=true;area.center=Vector3.up*3;area.size=new Vector3(10,6,10);
        zone=root.AddComponent<SellingZone>();Set(zone,"allGrainsSettings",new[]{new GrainElementSettings{grainType=GrainType.Corn,grainPrice=2}});
        player=Child("Outside player",Vector3.right*20).AddComponent<PlayerInteractor>();
        container=Cube("Unloaded container",Vector3.up).AddComponent<GrainContainer>();container.SetDemonstrationFill(10);
        button=Child("StartWeight",Vector3.right*7).AddComponent<SellingWeighButton>();Set(button,"sellingZone",zone);
        Physics.SyncTransforms();
    }
    [TearDown]
    public void TearDown()=>Object.DestroyImmediate(root);

    [Test]
    public void UnloadedContainersEnableStartOnlyWhileTheVehicleIsCompletelyOutside()
    {
        var vehicle=Cube("Vehicle",Vector3.forward*20);vehicle.AddComponent<BicycleController>();Physics.SyncTransforms();
        Assert.IsTrue(zone.CanWeigh(player));zone.CollectContainers(collected);Assert.That(collected,Is.EquivalentTo(new[]{container}));
        button.TryGetPrompt(player,out var prompt);Assert.IsTrue(prompt.ShowKey);Assert.That(prompt.Action,Is.EqualTo("Start weighing"));
        vehicle.transform.localPosition=Vector3.up;Physics.SyncTransforms();
        Assert.IsTrue(zone.HasVehicleOnScale);Assert.IsFalse(zone.CanWeigh(player));
        button.TryGetPrompt(player,out prompt);Assert.IsFalse(prompt.ShowKey);Assert.IsTrue(prompt.WeighingWrong);
        Assert.IsFalse(button.TryInteract(player));
        vehicle.transform.localPosition=Vector3.forward*20;Physics.SyncTransforms();Assert.IsTrue(zone.CanWeigh(player));
    }

    [Test]
    public void ContainersStillOnVehicleAreNeverIncludedInTheSale()
    {
        var vehicle=Cube("Loaded vehicle",Vector3.zero);vehicle.AddComponent<BicycleController>();
        container.transform.SetParent(vehicle.transform,true);Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.IsEmpty(collected);Assert.IsFalse(zone.CanWeigh(player));
        container.transform.SetParent(root.transform,true);Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.AreEqual(1,collected.Count);Assert.IsFalse(zone.CanWeigh(player));
        vehicle.transform.localPosition=Vector3.forward*20;Physics.SyncTransforms();Assert.IsTrue(zone.CanWeigh(player));
    }

    [TestCase(1f)]
    [TestCase(10f)]
    [TestCase(25f)]
    public void PartiallyFilledContainersCanBeWeighedForSale(float kilograms)
    {
        container.SetDemonstrationFill(kilograms);
        Assert.IsFalse(container.IsFull);
        Assert.IsTrue(zone.CanWeigh(player));
        zone.CollectContainers(collected);
        Assert.That(collected, Is.EquivalentTo(new[] { container }));
        Assert.IsTrue(button.TryGetPrompt(player, out var prompt));
        Assert.IsTrue(prompt.ShowKey);
        Assert.IsFalse(prompt.WeighingEmpty);
        Assert.IsFalse(prompt.WeighingWrong);
        Assert.AreEqual(kilograms, container.Kilograms);
    }

    [Test]
    public void UnloadedCrateIsWeighedEvenWhenItsColliderWasDisabledByCargoHandling()
    {
        container.GetComponent<BoxCollider>().enabled = false;
        Physics.SyncTransforms();
        zone.CollectContainers(collected);
        Assert.That(collected, Is.EquivalentTo(new[] { container }));
        Assert.IsTrue(zone.CanWeigh(player));
        button.TryGetPrompt(player, out var prompt);
        Assert.IsFalse(prompt.WeighingEmpty);
        container.SetCarried(true);
        zone.CollectContainers(collected);
        Assert.IsEmpty(collected);
        container.SetCarried(false);
        container.transform.localPosition = Vector3.right * 20;
        Physics.SyncTransforms();
        zone.CollectContainers(collected);
        Assert.IsEmpty(collected);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void VehicleColliderAtTheEdgeBlocksEvenWithOriginOutsideOrControllerDisabled(bool enabled)
    {
        var vehicle=Cube("Vehicle at edge",new Vector3(5.25f,1,0));vehicle.AddComponent<BicycleController>().enabled=enabled;
        Physics.SyncTransforms();Assert.IsTrue(zone.HasVehicleOnScale);Assert.IsTrue(zone.HasBlockingOccupants);Assert.IsFalse(zone.CanWeigh(player));
    }

    [Test]
    public void WheelbarrowMustLeaveTheScale()
    {
        Cube("Wheelbarrow",Vector3.up).AddComponent<WheelbarrowCargo>();Physics.SyncTransforms();
        Assert.IsTrue(zone.HasVehicleOnScale);Assert.IsFalse(zone.CanWeigh(player));
    }

    [Test]
    public void TrailerWithoutItsTowingVehicleStillBlocksTheScale()
    {
        var trailer=Cube("Trailer",Vector3.up);trailer.AddComponent<Rigidbody>().isKinematic=true;Physics.SyncTransforms();
        Assert.IsTrue(zone.HasBlockingOccupants);Assert.IsFalse(zone.CanWeigh(player));
    }

    [Test]
    public void PlayersAndEmptyOrCarriedContainersCannotStartWeighing()
    {
        container.SetDemonstrationFill(0);Assert.IsFalse(zone.CanWeigh(player));
        container.SetDemonstrationFill(10);container.SetCarried(true);Assert.IsFalse(zone.CanWeigh(player));zone.CollectContainers(collected);Assert.IsEmpty(collected);
        container.SetCarried(false);player.transform.localPosition=Vector3.up;Physics.SyncTransforms();Assert.IsFalse(zone.CanWeigh(player));
        player.transform.localPosition=Vector3.right*20;
        Cube("Another player on scale",Vector3.up).AddComponent<PlayerInteractor>();Physics.SyncTransforms();Assert.IsFalse(zone.CanWeigh(player));
    }

    [Test]
    public void BagInASiblingTrailerSlotMustBeUnloadedBeforeItCanBeSold()
    {
        var vehicle=Cube("Towing vehicle outside scale",Vector3.forward*20).AddComponent<BicycleController>();
        var slot=Child("Sibling trailer bag slot",Vector3.up).transform;
        vehicle.cargoSlots=new[]{slot};container.transform.SetParent(slot,true);Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.IsEmpty(collected);Assert.IsFalse(zone.CanWeigh(player));
        container.transform.SetParent(root.transform,true);Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.AreEqual(1,collected.Count);Assert.IsTrue(zone.CanWeigh(player));
    }

    private GameObject Child(string name,Vector3 local){var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=local;return go;}
    private GameObject Cube(string name,Vector3 local){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=local;return go;}
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
}
