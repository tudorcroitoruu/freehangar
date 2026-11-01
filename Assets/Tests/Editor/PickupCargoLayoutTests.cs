using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PickupCargoLayoutTests
{
    private GameObject root;
    private PickupCargoLayout layout;
    private void Create(int bays)
    {
        root=new GameObject("Pickup layout test");
        layout=root.AddComponent<PickupCargoLayout>();
        layout.largeSlots=new Transform[bays];
        for(int i=0;i<bays;i++)
        {
            var slot=new GameObject("Large bay "+i).transform;slot.SetParent(root.transform,false);
            slot.localPosition=new Vector3(i,1,0);layout.largeSlots[i]=slot;
        }
        layout.EnsureSlots();
    }
    private GrainContainer Container(int units)
    {
        var item=new GameObject("Container");item.transform.SetParent(root.transform,false);
        var cargo=item.AddComponent<GrainContainer>();
        var data=new SerializedObject(cargo);data.FindProperty("pickupSpaceUnits").intValue=units;data.ApplyModifiedPropertiesWithoutUndo();
        return cargo;
    }
    private void Load(GrainContainer item)
    {
        var slot=layout.FreeSlot(item);Assert.IsNotNull(slot);Assert.IsTrue(layout.CanLoadAt(slot,item));
        item.transform.SetParent(slot,false);
    }
    [Test]
    public void SerializedEmptyAnchorCacheRebuildsForPreviewWithoutDuplicatingSlots()
    {
        Create(6);
        var data=new SerializedObject(layout);
        data.FindProperty("anchors").arraySize=0;
        data.ApplyModifiedPropertiesWithoutUndo();
        var small=Container(1);
        int children=root.transform.childCount;
        Assert.That(layout.PreviewSlots(small).Count,Is.EqualTo(12));
        Assert.That(root.transform.childCount,Is.EqualTo(children));
        Assert.That(layout.PreviewSlots(small).Count,Is.EqualTo(12));
        Assert.IsNotNull(layout.FreeSlot(small));
    }

    [Test]
    public void SmallContainerPairsRunAlongTheVehicleLengthAndRefreshCachedPositions()
    {
        Create(2);var small=Container(1);var slots=layout.PreviewSlots(small);
        Vector3 center=layout.largeSlots[0].localPosition;
        Assert.That(slots[0].localPosition.x,Is.EqualTo(center.x).Within(.0001f));
        Assert.That(slots[1].localPosition.x,Is.EqualTo(center.x).Within(.0001f));
        Assert.That(slots[1].localPosition.z-slots[0].localPosition.z,Is.EqualTo(layout.smallContainerSeparation).Within(.0001f));
        layout.smallContainerSeparation=.6f;slots=layout.PreviewSlots(small);
        Assert.That(slots[1].localPosition.z-slots[0].localPosition.z,Is.EqualTo(.6f).Within(.0001f));
    }

    [TearDown] public void Cleanup() { if(root!=null) Object.DestroyImmediate(root); }

    [TestCase(5,1,10)] [TestCase(6,1,12)] [TestCase(8,1,16)]
    [TestCase(5,2,5)] [TestCase(6,2,6)] [TestCase(8,2,8)]
    public void CapacityDependsOnContainerSize(int bays,int units,int expected)
    {
        Create(bays);
        for(int i=0;i<expected;i++) Load(Container(units));
        Assert.IsNull(layout.FreeSlotByUnits(1));Assert.IsNull(layout.FreeSlotByUnits(2));
    }
    [Test]
    public void MixedLoadSharesBaysAndUnloadingBothSmallContainersFreesALargeBay()
    {
        Create(6);
        for(int i=0;i<3;i++) Load(Container(2));
        GrainContainer first=null,second=null;
        for(int i=0;i<6;i++) { var item=Container(1);Load(item);if(i==0)first=item;if(i==1)second=item; }
        Assert.IsNull(layout.FreeSlotByUnits(1));Assert.IsNull(layout.FreeSlotByUnits(2));
        first.transform.SetParent(root.transform,false);
        Assert.IsNotNull(layout.FreeSlotByUnits(1));Assert.IsNull(layout.FreeSlotByUnits(2));
        second.transform.SetParent(root.transform,false);
        Assert.IsNotNull(layout.FreeSlotByUnits(2));
    }
    [Test]
    public void SmallContainersUseTheSameBayBeforeBlockingAnotherLargeBay()
    {
        Create(2);var first=Container(1);Load(first);var second=Container(1);Load(second);
        Assert.That(layout.PoseIndex(first.transform.parent),Is.EqualTo(0));
        Assert.That(layout.PoseIndex(second.transform.parent),Is.EqualTo(1));
        Assert.IsNotNull(layout.FreeSlotByUnits(2));
        Assert.That(layout.PreviewSlots(first).Count,Is.EqualTo(4));
        var big=Container(2);Assert.That(layout.PreviewSlots(big).Count,Is.EqualTo(2));
    }
}
