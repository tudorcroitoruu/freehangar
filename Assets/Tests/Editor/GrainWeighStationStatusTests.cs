using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GrainWeighStationStatusTests
{
    private GameObject root, vehicle, actor;
    private SellingZone zone;
    private GrainWeighStation station;
    private GrainContainer cargo;

    [SetUp]
    public void SetUp()
    {
        root=new GameObject("Status-only weighing fixture");root.transform.position=new Vector3(20000,0,20000);
        var area=root.AddComponent<BoxCollider>();area.isTrigger=true;area.center=new Vector3(0,3,0);area.size=new Vector3(10,6,10);
        zone=root.AddComponent<SellingZone>();
        Set(zone,"allGrainsSettings",new[]{new GrainElementSettings{grainType=GrainType.Corn,grainPrice=2}});
        vehicle=new GameObject("Parked vehicle");vehicle.transform.SetParent(root.transform,false);
        vehicle.AddComponent<BicycleController>();
        var bag=GameObject.CreatePrimitive(PrimitiveType.Cube);bag.transform.SetParent(vehicle.transform,false);bag.transform.localPosition=Vector3.up;
        cargo=bag.AddComponent<GrainContainer>();cargo.SetDemonstrationFill(10);
        actor=new GameObject("Outside player");actor.transform.SetParent(root.transform,false);actor.transform.localPosition=Vector3.right*20;
        var player=actor.AddComponent<PlayerInteractor>();
        station=root.AddComponent<GrainWeighStation>();Set(station,"sellingZone",zone);Set(station,"player",player);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void ParkedCargoOnlyShowsRedButtonInstructionsWithoutSellingOrChangingCargo()
    {
        var previous=SaveSystem.Data;SaveSystem.Data=new PlayerData{cash=37};
        try
        {
            Vector3 position=cargo.transform.position,scale=cargo.transform.localScale;
            for(int i=0;i<20;i++)station.RefreshStatus();
            Assert.IsEmpty(station.StatusMessage);
            Assert.IsFalse(zone.CanWeigh(actor.GetComponent<PlayerInteractor>()));
            Assert.IsEmpty(station.StatusMessage);
            Assert.That(SaveSystem.Money,Is.EqualTo(37));
            Assert.That(cargo.Kilograms,Is.EqualTo(10));
            Assert.IsTrue(cargo.isActiveAndEnabled);
            Assert.That(cargo.transform.position,Is.EqualTo(position));
            Assert.That(cargo.transform.localScale,Is.EqualTo(scale));
        }
        finally { SaveSystem.Data=previous; }
    }

    [Test]
    public void GuidanceMatchesManualSellingFlowForPlayerVehicleAndEmptyCargo()
    {
        station.RefreshStatus();Assert.IsEmpty(station.StatusMessage);
        cargo.transform.SetParent(root.transform,true);vehicle.transform.localPosition=Vector3.forward*20;
        actor.transform.localPosition=Vector3.zero;Physics.SyncTransforms();station.RefreshStatus();
        Assert.IsEmpty(station.StatusMessage);
        actor.transform.localPosition=Vector3.right*20;cargo.SetDemonstrationFill(0);Physics.SyncTransforms();station.RefreshStatus();
        StringAssert.Contains("Place containers with grain",station.StatusMessage);
        cargo.SetDemonstrationFill(10);station.RefreshStatus();
        StringAssert.Contains("READY TO WEIGH",station.StatusMessage);
        StringAssert.Contains(GrainWeighStation.RedButtonInstruction,station.StatusMessage);
    }

    [Test]
    public void WeighButtonUsesExclusiveWarningsForInvalidAndEmptyScaleThenAllowsValidCargo()
    {
        var button = root.AddComponent<SellingWeighButton>(); Set(button, "sellingZone", zone);
        var player = actor.GetComponent<PlayerInteractor>();
        Assert.IsTrue(button.TryGetPrompt(player, out var wrong));
        Assert.IsTrue(wrong.WeighingWrong);
        Assert.IsFalse(wrong.WeighingEmpty);
        Assert.IsFalse(wrong.ShowKey);
        Assert.IsFalse(button.TryInteract(player));

        vehicle.transform.localPosition = Vector3.forward * 20;
        Physics.SyncTransforms();
        Assert.IsTrue(button.TryGetPrompt(player, out var empty));
        Assert.IsTrue(empty.WeighingEmpty);
        Assert.IsFalse(empty.WeighingWrong);
        Assert.IsFalse(empty.ShowKey);
        Assert.IsFalse(button.TryInteract(player));

        cargo.transform.SetParent(root.transform, false);
        cargo.transform.localPosition = Vector3.up;
        Physics.SyncTransforms();
        Assert.IsTrue(button.TryGetPrompt(player, out var ready));
        Assert.IsFalse(ready.WeighingEmpty);
        Assert.IsFalse(ready.WeighingWrong);
        Assert.IsTrue(ready.ShowKey);

        var invalid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        invalid.transform.SetParent(root.transform, false);
        invalid.transform.localPosition = Vector3.up * 2;
        invalid.AddComponent<Rigidbody>().isKinematic = true;
        Physics.SyncTransforms();
        Assert.IsTrue(button.TryGetPrompt(player, out wrong));
        Assert.IsTrue(wrong.WeighingWrong);
        Assert.IsFalse(wrong.WeighingEmpty);
        Assert.IsFalse(wrong.ShowKey);
        Assert.IsFalse(button.TryInteract(player));
    }

    private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
}
