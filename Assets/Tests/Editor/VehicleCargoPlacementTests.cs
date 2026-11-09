using NUnit.Framework;
using UnityEngine;

public class VehicleCargoPlacementTests
{
    [TestCase("Small", "FreeBag")]
    [TestCase("Small", "SmallBag")]
    [TestCase("Small", "LargeBag")]
    [TestCase("Medium", "FreeBag")]
    [TestCase("Medium", "SmallBag")]
    [TestCase("Medium", "LargeBag")]
    [TestCase("Max", "FreeBag")]
    [TestCase("Max", "SmallBag")]
    [TestCase("Max", "LargeBag")]
    public void WheelbarrowBagsStayUprightInsideSeparateSlotsAtEveryFill(string tier, string id)
    {
        var root = new GameObject("Wheelbarrow_" + tier);
        try
        {
            root.transform.SetPositionAndRotation(new Vector3(1000, 30, 1000), Quaternion.Euler(0, 37, 0));
            root.transform.localScale = new Vector3(1.9f, 1.38f, 1.64f);
            var barrow = root.AddComponent<WheelbarrowCargo>();
            var profile = new VehicleUpgradeSettings.VehicleType { cargoPoses = new[] {
                new VehicleUpgradeSettings.CargoPose { containerId=id, rotation=new Vector3(-90,0,0),
                    position=new Vector3(0,.15f,-.1f) } } };
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/" + id + ".prefab");
            var occupied = new System.Collections.Generic.List<Bounds>();
            for (int i = 0; i < barrow.CargoSlotCount; i++)
            {
                var slot = new GameObject("Bag slot " + i).transform;
                slot.SetParent(root.transform, false);
                slot.localPosition = barrow.SlotPosition(i);
                slot.localRotation = barrow.SlotRotation;
                var item = Object.Instantiate(prefab, slot);
                var bag = item.GetComponent<GrainContainer>();
                var scale = item.transform.localScale;
                foreach (float fill in new[] { 0f, .5f, 1f, 0f })
                {
                    VehicleCargoPreview.PrepareContainer(item, fill, fill == 1 ? 1 : 0);
                    for (int pass = 0; pass < 3; pass++)
                        VehicleCargoPlacement.Place(item.transform, slot, profile, id, i, Quaternion.Euler(90,0,0), true, scale);
                    Assert.Less(Quaternion.Angle(item.transform.rotation, root.transform.rotation * bag.UprightRotation), .01f);
                    var bounds = VehicleCargoRack.ContactBounds(item.transform, root.transform);
                    Assert.That(bounds.center.x, Is.EqualTo(slot.localPosition.x).Within(.001f));
                    Assert.That(bounds.center.z, Is.EqualTo(slot.localPosition.z).Within(.001f));
                    Assert.That(bounds.min.y, Is.EqualTo(slot.localPosition.y).Within(.001f));
                    Assert.GreaterOrEqual(bounds.min.x, -.35f);
                    Assert.LessOrEqual(bounds.max.x, .35f);
                    Assert.GreaterOrEqual(bounds.min.z, -.72f - .425f);
                    Assert.LessOrEqual(bounds.max.z, -.72f + .425f);
                    foreach (var previous in occupied)
                        Assert.IsFalse(bounds.min.x < previous.max.x && bounds.max.x > previous.min.x &&
                            bounds.min.z < previous.max.z && bounds.max.z > previous.min.z, "Independent bag footprints must not overlap.");
                }
                occupied.Add(VehicleCargoRack.ContactBounds(item.transform, root.transform));
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BigBagKeepsFilledVehiclePoseAtEveryFillLevel(bool bottom)
    {
        var slot = new GameObject("Big bag placement slot");
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/BigBag.prefab");
        var item = Object.Instantiate(prefab, slot.transform);
        try
        {
            slot.transform.SetPositionAndRotation(new Vector3(20, 3, 40), Quaternion.Euler(0, 35, 0));
            var bag = item.GetComponent<GrainContainer>();
            var scale = item.transform.localScale;
            var profile = new VehicleUpgradeSettings.VehicleType { cargoPoses = new[] {
                new VehicleUpgradeSettings.CargoPose { containerId = "BigBag", slotIndex = 0,
                    position = new Vector3(.1f, .7f, -.3f), rotation = new Vector3(0, 15, 0), scale = Vector3.one }
            }};
            bag.RestoreSave(new ContainerSave { kilograms = bag.CapacityKg, closure = 1 });
            VehicleCargoPlacement.Place(item.transform, slot.transform, profile, "BigBag", 0, bag.UprightRotation, bottom, scale);
            var fullPosition = item.transform.position;
            var fullRotation = item.transform.rotation;
            foreach (float fraction in new[] { 0f, .25f, 1f })
            {
                bag.RestoreSave(new ContainerSave { kilograms = bag.CapacityKg * fraction, closure = 0 });
                VehicleCargoPlacement.Place(item.transform, slot.transform, profile, "BigBag", 0, bag.UprightRotation, bottom, scale);
                Assert.Less(Vector3.Distance(fullPosition, item.transform.position), .001f, "Fill or closure must not move the pallet.");
                Assert.Less(Quaternion.Angle(fullRotation, item.transform.rotation), .01f);
            }
        }
        finally { Object.DestroyImmediate(slot); }
    }

    [TestCase("Medium")]
    [TestCase("Max")]
    public void UpgradedWheelbarrowLoadsThroughBarrelAfterCargoRestore(string tier)
    {
        foreach (string id in new[] { "FreeBag", "SmallBag", "LargeBag", "Crate", "Barrel" })
        {
            var root = new GameObject("Wheelbarrow_" + tier);
            GameObject item = null;
            try
            {
                var barrow = root.AddComponent<WheelbarrowCargo>();
                var savedSlot = new GameObject("Bag slot 0").transform; savedSlot.SetParent(root.transform);
                var savedBag = new GameObject("Saved bag"); savedBag.transform.SetParent(savedSlot);
                savedBag.AddComponent<BoxCollider>().size = Vector3.one * 4;
                barrow.RestoreLoadedBag(savedBag.AddComponent<GrainContainer>(), savedSlot);
                var actor = new GameObject("Carrier"); actor.transform.SetParent(root.transform);
                var carry = actor.AddComponent<BagInteraction>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(BagInteraction).GetField("carryAnchor", flags).SetValue(carry, actor.transform);
                item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/" + id + ".prefab"));
                VehicleCargoPreview.PrepareContainer(item, 1, 1);
                var bag = item.GetComponent<GrainContainer>();
                Assert.IsTrue(carry.RestoreCarried(bag), tier + " pickup " + id);
                Assert.IsTrue(barrow.CanLoad(carry), tier + " load " + id);
                var slot = new GameObject("Bag slot 1").transform; slot.SetParent(root.transform);
                Assert.IsTrue(carry.TryStow(slot), tier + " stow " + id);
                barrow.RestoreLoadedBag(bag, slot);
                Assert.That(barrow.BagCount, Is.EqualTo(2));
                Assert.That(barrow.OccupiedVolume, Is.EqualTo(2f * barrow.CapacityVolume / barrow.CargoSlotCount).Within(.0001f));
            }
            finally { if (item != null) Object.DestroyImmediate(item); Object.DestroyImmediate(root); }
        }
    }

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
    [TestCase("LargeBag")]
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
            Assert.AreEqual(bagId == "SmallBag" || bagId == "LargeBag", prompt.ShowKey); Assert.IsFalse(prompt.VehicleFull);
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

    [TestCase("FreeBag", 0f, 0f, true)]
    [TestCase("FreeBag", .5f, 0f, false)]
    [TestCase("FreeBag", .5f, 1f, false)]
    [TestCase("FreeBag", 1f, 0f, false)]
    [TestCase("FreeBag", 1f, 1f, true)]
    [TestCase("Crate", .5f, 0f, true)]
    [TestCase("Crate", 1f, 0f, true)]
    [TestCase("Barrel", .5f, 0f, true)]
    [TestCase("Barrel", 1f, 0f, true)]
    [TestCase("BigBag", .5f, 0f, true)]
    [TestCase("BigBag", 1f, 0f, true)]
    public void VehicleLoadingAllowsUnfinishedCratesBarrelsAndBigBags(string containerId, float fill, float close, bool allowed)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Gameplay/Containers/{containerId}.prefab");
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

    [TestCase(typeof(BicycleController), false)]
    [TestCase(typeof(MotorollerController), false)]
    [TestCase(typeof(TrikeController), true)]
    [TestCase(typeof(LawnmowerController), true)]
    [TestCase(typeof(PickupController), true)]
    public void UnfinishedBagsLoadFromTrikeUpIncludingTrailerSlots(System.Type vehicleType, bool allowed)
    {
        var vehicle = new GameObject("Vehicle");
        vehicle.SetActive(false);
        var trailer = new GameObject("Trailer");
        var item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab"));
        try
        {
            var rig = vehicle.AddComponent<BicycleController>();
            if (vehicleType != typeof(BicycleController)) vehicle.AddComponent(vehicleType);
            trailer.AddComponent<VehicleCargoOwner>().vehicle = rig;
            VehicleCargoPreview.PrepareContainer(item, .5f, 0f);
            var bag = item.GetComponent<GrainContainer>();
            Assert.That(VehicleCargoOwner.CanLoadContainer(rig, bag), Is.EqualTo(allowed));
            Assert.That(VehicleCargoOwner.CanLoadContainer(trailer.transform, bag), Is.EqualTo(allowed));
            Assert.IsFalse(VehicleCargoOwner.CanLoadContainer(null, bag));
            Assert.AreEqual(bag.CapacityKg * .5f, bag.Kilograms, .001f);
        }
        finally { Object.DestroyImmediate(item); Object.DestroyImmediate(trailer); Object.DestroyImmediate(vehicle); }
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
