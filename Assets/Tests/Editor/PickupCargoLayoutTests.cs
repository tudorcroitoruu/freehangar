using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PickupCargoLayoutTests
{
    [Test]
    public void ResetPlacementRejectsUnclearedHangarAndDoorwayOverlap()
    {
        var previousData = SaveSystem.Data;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var cache = (System.Collections.Generic.List<Transform>)typeof(HangarVehicleAccess).GetField("hangars", flags).GetValue(null);
        var previousHangars = cache.ToArray();
        var scan = typeof(HangarVehicleAccess).GetField("nextScan", flags);
        var previousScan = scan.GetValue(null);
        var hangar = new GameObject("01_Modular_Hangar");
        try
        {
            SaveSystem.Data = new PlayerData();
            hangar.transform.position = new Vector3(10000, 0, 10000);
            cache.Clear(); cache.Add(hangar.transform);
            scan.SetValue(null, float.PositiveInfinity);
            var inside = new Bounds(hangar.transform.position + Vector3.up, new Vector3(2, 2, 4));
            var doorway = new Bounds(hangar.transform.position + new Vector3(0, 1, 16), new Vector3(2, 2, 4));
            var outside = new Bounds(hangar.transform.position + new Vector3(0, 1, 20), new Vector3(2, 2, 4));
            Assert.IsTrue(HangarVehicleAccess.BlocksPlacement(inside, null));
            Assert.IsTrue(HangarVehicleAccess.BlocksPlacement(doorway, null));
            Assert.IsFalse(HangarVehicleAccess.BlocksPlacement(outside, null));
            SaveSystem.Data.TryRecordHangarClearance(1, 98);
            Assert.IsFalse(HangarVehicleAccess.BlocksPlacement(inside, null));
        }
        finally
        {
            cache.Clear(); cache.AddRange(previousHangars); scan.SetValue(null, previousScan);
            SaveSystem.Data = previousData; Object.DestroyImmediate(hangar);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void VehicleAccessOpensAtHangarCompletion(int number)
    {
        var previous = SaveSystem.Data;
        var hangar = new GameObject(number == 1 ? "01_Modular_Hangar" : "Hangar access test");
        try
        {
            SaveSystem.Data = new PlayerData();
            SaveSystem.Data.TryOpenHangar(number);
            if (number != 1)
            {
                var context = hangar.AddComponent<GoalContext>();
                typeof(GoalContext).GetField("hangarId", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(context, number);
            }
            SaveSystem.Data.TryRecordHangarClearance(number, 97f);
            Assert.IsFalse(HangarVehicleAccess.IsCleared(hangar.transform));
            SaveSystem.Data.TryRecordHangarClearance(number, 98f);
            Assert.IsTrue(HangarVehicleAccess.IsCleared(hangar.transform));
            SaveSystem.Data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(SaveSystem.Data));
            Assert.IsTrue(HangarVehicleAccess.IsCleared(hangar.transform));
        }
        finally { SaveSystem.Data = previous; Object.DestroyImmediate(hangar); }
    }

    [TestCase("5_Pick_up_Driveable")]
    [TestCase("6_Pick_Up_Bigger_Driveable")]
    [TestCase("7_Pick_Up_Last_Driveable")]
    public void ParkedPickupUsesSurfaceCollidersInsteadOfSolidBedEnvelope(string name)
    {
        var parent = new GameObject("Inactive pickup collision test");
        parent.SetActive(false);
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/" + name + ".prefab");
            var instance = Object.Instantiate(prefab, parent.transform);
            var pickup = instance.GetComponent<PickupController>();
            typeof(PickupController).GetMethod("InitializeParkedCollisions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pickup, null);
            Assert.IsFalse(instance.GetComponent<BoxCollider>().enabled, "The root envelope must not seal the bed.");
            var contacts = instance.GetComponentsInChildren<MeshCollider>(true);
            Assert.That(contacts.Length, Is.GreaterThan(0));
            foreach (var contact in contacts)
            {
                Assert.IsNotNull(contact.sharedMesh);
                Assert.IsFalse(contact.convex, "A convex hull seals open cargo beds too.");
                Assert.IsFalse(contact.isTrigger);
                Assert.IsTrue(contact.enabled);
            }
        }
        finally { Object.DestroyImmediate(parent); }
    }

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
