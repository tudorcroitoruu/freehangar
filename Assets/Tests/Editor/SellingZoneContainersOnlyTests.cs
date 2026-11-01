using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SellingZoneContainersOnlyTests
{
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
        button.TryGetPrompt(player,out prompt);Assert.IsFalse(prompt.ShowKey);StringAssert.Contains("move the vehicle off",prompt.Action);
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

    [Test]
    public void BoxLoadedOntoASiblingTrailerSlotMustBeUnloadedBeforeItCanBeSold()
    {
        var box=container.gameObject.AddComponent<BicycleCargoBox>();
        var slot=Child("Trailer cargo slot",Vector3.up).transform;
        Assert.IsTrue(box.Load(slot));Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.IsEmpty(collected);Assert.IsFalse(zone.CanWeigh(player));
        Assert.IsTrue(box.Unload(root.transform.position+Vector3.up));Physics.SyncTransforms();
        zone.CollectContainers(collected);Assert.AreEqual(1,collected.Count);Assert.IsTrue(zone.CanWeigh(player));
    }

    private GameObject Child(string name,Vector3 local){var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=local;return go;}
    private GameObject Cube(string name,Vector3 local){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=local;return go;}
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
}
