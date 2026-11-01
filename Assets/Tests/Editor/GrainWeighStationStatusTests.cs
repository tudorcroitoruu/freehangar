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
            StringAssert.Contains("READY TO WEIGH",station.StatusMessage);
            StringAssert.Contains(GrainWeighStation.RedButtonInstruction,station.StatusMessage);
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
        actor.transform.localPosition=Vector3.zero;Physics.SyncTransforms();station.RefreshStatus();
        StringAssert.Contains("Step off the scale",station.StatusMessage);
        StringAssert.Contains("Leave your vehicle parked",station.StatusMessage);
        actor.transform.localPosition=Vector3.right*20;cargo.SetDemonstrationFill(0);Physics.SyncTransforms();station.RefreshStatus();
        StringAssert.Contains("Load filled grain containers",station.StatusMessage);
        vehicle.transform.localPosition=Vector3.forward*20;Physics.SyncTransforms();station.RefreshStatus();
        StringAssert.Contains("Park your vehicle",station.StatusMessage);
        StringAssert.Contains(GrainWeighStation.RedButtonInstruction,station.StatusMessage);
    }

    private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
}
