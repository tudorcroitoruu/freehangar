using NUnit.Framework;
using UnityEngine;

public class VehicleCargoPlacementTests
{
    [TestCase(2)]
    [TestCase(5)]
    public void SingleContainerSlotsHoldExactlyTheConfiguredNumberOfBags(int capacity)
    {
        var root=new GameObject("Cargo rack capacity");
        try
        {
            var rack=root.AddComponent<VehicleCargoRack>();
            var deck=new GameObject("Deck");deck.transform.SetParent(root.transform,false);
            rack.surface=deck.transform;rack.slotCount=capacity;rack.oneContainerPerSlot=true;
            for(int i=0;i<capacity;i++)
            {
                var slot=rack.FreeSlot(true);Assert.IsNotNull(slot);
                var bag=new GameObject("Bag");bag.transform.SetParent(slot,false);bag.AddComponent<GrainContainer>();
            }
            Assert.That(rack.UsedSlots,Is.EqualTo(capacity));
            Assert.That(rack.ContainerSlotCount,Is.EqualTo(capacity));
            Assert.IsNull(rack.FreeSlot(true));Assert.IsNull(rack.FreeSlot(false));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void JointedTrailerResolvesItsDrivingVehicle()
    {
        var rigObject=new GameObject("Mower");var trailer=new GameObject("Trailer");
        try
        {
            var rig=rigObject.AddComponent<BicycleController>();
            var owner=trailer.AddComponent<VehicleCargoOwner>();owner.vehicle=rig;
            var slot=new GameObject("Cargo center");slot.transform.SetParent(trailer.transform,false);
            Assert.AreSame(rig,VehicleCargoOwner.Resolve(slot.transform));
        }
        finally { Object.DestroyImmediate(trailer);Object.DestroyImmediate(rigObject); }
    }

    [Test]
    public void FirstSlotValuesAreSharedByDefaultAndIndependentValuesSurviveToggle()
    {
        var first=new VehicleUpgradeSettings.CargoPose{containerId="FreeBag",slotIndex=0};
        var second=new VehicleUpgradeSettings.CargoPose{containerId="FreeBag",slotIndex=2,position=Vector3.right};
        var profile=new VehicleUpgradeSettings.VehicleType{cargoPoses=new[]{first,second}};
        Assert.IsTrue(profile.UsesFirstSlotForAll("FreeBag"));
        Assert.AreSame(first,profile.FindCargoPose("FreeBag",2));
        Assert.AreSame(first,profile.FindCargoPose("FreeBag",8));
        Assert.IsNull(profile.FindCargoPose("SmallBag",2));
        first.applyToAllSlots=false;
        Assert.AreSame(second,profile.FindCargoPose("FreeBag",2));
        Assert.That(second.position,Is.EqualTo(Vector3.right));
    }

    [Test]
    public void RepeatedPlacementDoesNotAccumulateScaleAndDifferentSlotsStayIndependent()
    {
        var item=new GameObject("Cargo"); var slot=new GameObject("Slot");
        try
        {
            var profile=new VehicleUpgradeSettings.VehicleType
            {
                cargoPoses=new[]{new VehicleUpgradeSettings.CargoPose{containerId="FreeBag",slotIndex=0,applyToAllSlots=false,scale=new Vector3(.5f,2,1.5f)}}
            };
            Vector3 authoredScale=new Vector3(2,3,4);
            for(int i=0;i<100;i++) VehicleCargoPlacement.Place(item.transform,slot.transform,profile,"FreeBag",0,Quaternion.identity,false,authoredScale);
            Assert.That(item.transform.localScale,Is.EqualTo(new Vector3(1,6,6)));
            VehicleCargoPlacement.Place(item.transform,slot.transform,profile,"SmallBag",0,Quaternion.identity,false,authoredScale);
            Assert.That(item.transform.localScale,Is.EqualTo(authoredScale));
            VehicleCargoPlacement.Place(item.transform,slot.transform,profile,"FreeBag",1,Quaternion.identity,false,authoredScale);
            Assert.That(item.transform.localScale,Is.EqualTo(authoredScale));
            Assert.That(new VehicleUpgradeSettings.CargoPose().EffectiveScale,Is.EqualTo(Vector3.one));
        }
        finally { Object.DestroyImmediate(item);Object.DestroyImmediate(slot); }
    }
}
