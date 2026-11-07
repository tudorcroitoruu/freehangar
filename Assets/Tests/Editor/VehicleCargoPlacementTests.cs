using NUnit.Framework;
using UnityEngine;

public class VehicleCargoPlacementTests
{
    [Test]
    public void PickingLowerCargoSelectsTopOfItsColumnOnly()
    {
        var root = new GameObject("Wheelbarrow_Small");
        try
        {
            root.AddComponent<WheelbarrowCargo>();
            GrainContainer MakeBag(string name, Vector3 position)
            {
                var item = new GameObject(name);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = position;
                item.AddComponent<BoxCollider>().size = Vector3.one;
                return item.AddComponent<GrainContainer>();
            }
            var bottom = MakeBag("Bottom", Vector3.zero);
            var middle = MakeBag("Middle", Vector3.up);
            var top = MakeBag("Top", Vector3.up * 2f);
            var adjacent = MakeBag("Other column", new Vector3(2f, 3f, 0f));
            root.transform.rotation = Quaternion.Euler(10f, 45f, 5f);
            Assert.AreSame(top, BagInteraction.TopCargoBag(bottom));
            Assert.AreSame(top, BagInteraction.TopCargoBag(middle));
            Assert.AreSame(adjacent, BagInteraction.TopCargoBag(adjacent));
            top.transform.SetParent(null);
            Object.DestroyImmediate(top.gameObject);
            Assert.AreSame(middle, BagInteraction.TopCargoBag(bottom));
            middle.gameObject.SetActive(false);
            Assert.AreSame(bottom, BagInteraction.TopCargoBag(bottom));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("FreeBag")]
    [TestCase("SmallBag")]
    public void EmptyStarterWheelbarrowAcceptsFinishedStarterBagAndReportsOversizeSeparately(string bagId)
    {
        var root = new GameObject("Wheelbarrow regression");
        var item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/" + bagId + ".prefab"));
        var previousData = SaveSystem.Data;
        try
        {
            SaveSystem.Data = new PlayerData();
            var barrowObject = new GameObject("Wheelbarrow_Small");
            barrowObject.transform.SetParent(root.transform);
            var barrow = barrowObject.AddComponent<WheelbarrowCargo>();
            var actor = new GameObject("Carrier"); actor.transform.SetParent(root.transform);
            var carry = actor.AddComponent<BagInteraction>();
            var interactor = actor.AddComponent<PlayerInteractor>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerInteractor).GetMethod("Awake", flags).Invoke(interactor, null);
            typeof(BagInteraction).GetField("carryAnchor", flags).SetValue(carry, actor.transform);
            VehicleCargoPreview.PrepareContainer(item, 1, 1);
            Assert.IsTrue(carry.RestoreCarried(item.GetComponent<GrainContainer>()));
            Assert.IsTrue(barrow.CanLoad(carry));
            Assert.IsTrue(barrow.TryGetPrompt(interactor, out var prompt));
            Assert.IsTrue(prompt.ShowKey);
            Assert.IsFalse(prompt.VehicleFull);
            typeof(WheelbarrowCargo).GetField("occupiedVolume", flags).SetValue(barrow, barrow.CapacityVolume);
            barrow.TryGetPrompt(interactor, out prompt);
            Assert.IsFalse(prompt.ShowKey); Assert.IsTrue(prompt.VehicleFull);
            typeof(WheelbarrowCargo).GetField("occupiedVolume", flags).SetValue(barrow, 0f);
            typeof(BagInteraction).GetField("carriedVolume", flags).SetValue(carry, 2f);
            barrow.TryGetPrompt(interactor, out prompt);
            Assert.AreEqual(bagId == "SmallBag", prompt.ShowKey); Assert.IsFalse(prompt.VehicleFull);
        }
        finally { Object.DestroyImmediate(item); Object.DestroyImmediate(root); SaveSystem.Data = previousData; }
    }

    [Test]
    public void RestoredWheelbarrowVolumeDoesNotGrowWithCargoRotation()
    {
        var root = new GameObject("Restored barrow");
        try
        {
            var barrow = root.AddComponent<WheelbarrowCargo>();
            var slot = new GameObject("Bag slot 1"); slot.transform.SetParent(root.transform);
            var item = new GameObject("Cargo"); item.transform.SetParent(slot.transform);
            item.AddComponent<BoxCollider>().size = new Vector3(.5f, 1, .4f);
            item.transform.rotation = Quaternion.Euler(20, 45, 30);
            var bag = item.AddComponent<GrainContainer>();
            barrow.RestoreLoadedBag(bag, slot.transform);
            Assert.AreEqual(.2f, barrow.OccupiedVolume, .00001f);
            barrow.NotifyBagTaken(bag);
            Assert.AreEqual(0f, barrow.OccupiedVolume);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EmptyBagsStackWithoutPreviewPositionOffsetsOrSlotGaps()
    {
        var root = new GameObject("Empty bag stack");
        try
        {
            root.transform.rotation = Quaternion.Euler(0, 35, 0);
            root.transform.localScale = Vector3.one * 1.4f;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
            var profile = new VehicleUpgradeSettings.VehicleType { cargoPoses = new[] {
                new VehicleUpgradeSettings.CargoPose { containerId="FreeBag", slotIndex=0,
                    position=new Vector3(.4f, .7f, -.2f), rotation=new Vector3(0, 90, -90) } } };
            Transform previous = null;
            for (int i = 0; i < 3; i++)
            {
                var slot = new GameObject("Slot " + i).transform;
                slot.SetParent(root.transform, false);
                slot.localPosition = new Vector3(i * .3f, i * .6f, 0);
                var item = Object.Instantiate(prefab, slot);
                VehicleCargoPreview.PrepareContainer(item, 0, 0);
                var scale = item.transform.localScale;
                var rotation = item.transform.localRotation;
                for (int pass = 0; pass < 3; pass++)
                    VehicleCargoPlacement.Place(item.transform, slot, profile, "FreeBag", i, rotation, true, scale);
                Bounds bounds = VehicleCargoRack.ContactBounds(item.transform, root.transform);
                if (previous == null)
                {
                    Assert.Less(Mathf.Abs(bounds.min.y), .0001f);
                    Assert.Less(Mathf.Abs(bounds.center.x), .0001f);
                    Assert.Less(Mathf.Abs(bounds.center.z), .0001f);
                }
                else
                {
                    Bounds below = VehicleCargoRack.ContactBounds(previous, root.transform);
                    Assert.Less(Mathf.Abs(bounds.min.y - below.max.y), .0001f);
                    Assert.Less(Mathf.Abs(bounds.center.x - below.center.x), .0001f);
                    Assert.Less(Mathf.Abs(bounds.center.z - below.center.z), .0001f);
                }
                previous = item.transform;
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0f, 0f, true)]
    [TestCase(.5f, 0f, false)]
    [TestCase(.5f, 1f, false)]
    [TestCase(1f, 0f, false)]
    [TestCase(1f, 1f, true)]
    public void VehicleLoadingRequiresEmptyOrFinishedBag(float fill, float close, bool allowed)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
        var item = Object.Instantiate(prefab);
        var actor = new GameObject("Carrier");
        var slot = new GameObject("Vehicle slot");
        try
        {
            VehicleCargoPreview.PrepareContainer(item, fill, close);
            var cargo = item.GetComponent<GrainContainer>();
            Assert.That(cargo.CanLoadOnVehicle, Is.EqualTo(allowed));
            if (!allowed)
            {
                var carry = actor.AddComponent<BagInteraction>();
                typeof(BagInteraction).GetField("carried", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(carry, cargo);
                Assert.IsFalse(carry.TryStow(slot.transform));
                Assert.IsNull(item.transform.parent);
            }
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(item); Object.DestroyImmediate(slot); }
    }

    [TestCase(0f)]
    [TestCase(1f)]
    public void OnlyEmptyBagsIgnorePreviewRotation(float fill)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
        var item = Object.Instantiate(prefab);
        var slot = new GameObject("Vehicle slot");
        try
        {
            VehicleCargoPreview.PrepareContainer(item, fill, fill);
            slot.transform.rotation = Quaternion.Euler(0, 35, 0);
            var pose = new VehicleUpgradeSettings.CargoPose { containerId="FreeBag", slotIndex=0, rotation=new Vector3(0, 90, -90), position=new Vector3(.1f, .2f, .3f), scale=new Vector3(.8f, .9f, 1.1f) };
            var profile = new VehicleUpgradeSettings.VehicleType { cargoPoses=new[] { pose } };
            var baseline = item.transform.rotation;
            var scale = item.transform.localScale;
            for (int i = 0; i < 3; i++)
                VehicleCargoPlacement.Place(item.transform, slot.transform, profile, "FreeBag", 0, baseline, true, scale);
            var expected = slot.transform.rotation * baseline * (fill == 0 ? Quaternion.identity : Quaternion.Euler(pose.rotation));
            Assert.Less(Quaternion.Angle(expected, item.transform.rotation), .01f);
            Assert.Less(Vector3.Distance(item.transform.localScale, Vector3.Scale(scale, pose.EffectiveScale)), .0001f);
        }
        finally { Object.DestroyImmediate(item); Object.DestroyImmediate(slot); }
    }

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
