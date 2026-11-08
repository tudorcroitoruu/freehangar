using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ForkliftCargoTests
{
    [Test]
    public void ForkliftAnimatesVisibleWheelParentsAndSteeringWithoutMovingAxles()
    {
        var root = new GameObject("Forklift visual regression");
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Vehicles/Agricultural/TFP_Tractor_01C.prefab");
            Assert.IsNotNull(prefab);
            var model = Object.Instantiate(prefab, root.transform);
            model.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var forklift = root.AddComponent<ForkliftController>();
            var rig = root.GetComponent<BicycleController>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var wheels = new System.Collections.Generic.List<Transform>();
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                if (((node.name.Contains("Wheel_F.") || node.name.Contains("Wheel_R.")) && !node.name.Contains("LOD")) ||
                    node.name.Contains("Steering_Wheel")) wheels.Add(node);
            Assert.AreEqual(5, wheels.Count, "Four visible wheel parents and the steering wheel must be connected.");
            var rests = wheels.ConvertAll(wheel => wheel.localRotation);
            var positions = wheels.ConvertAll(wheel => wheel.localPosition);
            typeof(BicycleController).GetField("wheelAngle", flags).SetValue(rig, 90f);
            typeof(BicycleController).GetField("currentSteering", flags).SetValue(rig, 20f);
            forklift.RefreshWheelVisuals();
            for (int i = 0; i < wheels.Count; i++)
            {
                Assert.Greater(Quaternion.Angle(rests[i], wheels[i].localRotation), 1f, wheels[i].name);
                Assert.AreEqual(positions[i], wheels[i].localPosition, "Axles must stay attached to the chassis.");
            }
            typeof(BicycleController).GetField("wheelAngle", flags).SetValue(rig, 0f);
            typeof(BicycleController).GetField("currentSteering", flags).SetValue(rig, 0f);
            forklift.RefreshWheelVisuals();
            for (int i = 0; i < wheels.Count; i++)
                Assert.Less(Quaternion.Angle(rests[i], wheels[i].localRotation), .01f, "Reverse movement must return to the original pose.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(64, false)]
    [TestCase(0, true)]
    [TestCase(64, true)]
    public void LoadingTargetSurvivesDenseCollidersAndFindsReachablePickupBay(int contacts, bool distantFirstBay)
    {
        var root = new GameObject("Forklift loading target regression");
        var truck = new GameObject("Pickup bays");
        var bagObject = new GameObject("Partial big bag");
        try
        {
            root.transform.position = new Vector3(28000, 1000, 28000);
            var forklift = root.AddComponent<ForkliftController>();
            var anchor = new GameObject("Fork anchor").transform;
            anchor.SetParent(root.transform, false);
            var config = new SerializedObject(forklift);
            config.FindProperty("forkAnchor").objectReferenceValue = anchor;
            config.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < contacts; i++)
            {
                var contact = new GameObject("Detailed mesh contact");
                contact.transform.SetParent(root.transform, false);
                contact.AddComponent<BoxCollider>().size = Vector3.one * .1f;
            }
            truck.transform.position = root.transform.position + Vector3.forward * 2;
            truck.AddComponent<BicycleController>();
            truck.AddComponent<BoxCollider>();
            var layout = truck.AddComponent<PickupCargoLayout>();
            var first = new GameObject("Front bay").transform;
            first.SetParent(truck.transform, false);
            first.localPosition = distantFirstBay ? Vector3.forward * 5 : Vector3.zero;
            var second = new GameObject("Rear bay").transform;
            second.SetParent(truck.transform, false);
            second.localPosition = Vector3.back * .5f;
            layout.largeSlots = new[] { first, second };
            layout.EnsureSlots();
            bagObject.AddComponent<BoxCollider>();
            var bag = bagObject.AddComponent<GrainContainer>();
            var data = new SerializedObject(bag);
            data.FindProperty("cargoTypeId").stringValue = "BigBag";
            data.FindProperty("pickupSpaceUnits").intValue = 2;
            data.FindProperty("capacityKg").floatValue = 500;
            data.FindProperty("kilograms").floatValue = 250;
            data.ApplyModifiedPropertiesWithoutUndo();
            bag.transform.position = anchor.position + new Vector3(0, .5f, .4f);
            Assert.IsTrue(forklift.TryLift(bag));
            Physics.SyncTransforms();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ForkliftController).GetMethod("FindBag", flags).Invoke(forklift, null);
            var target = (Transform)typeof(ForkliftController).GetMethod("FindLoadingSlot", flags).Invoke(forklift, null);
            Assert.AreSame(second, target, "The F action should select the closest reachable free bay.");
            Assert.IsTrue(forklift.TryLoadOntoVehicle(target));
            Assert.AreSame(second, bag.transform.parent);
            Assert.AreEqual(250, bag.Kilograms);
        }
        finally
        {
            Object.DestroyImmediate(bagObject);
            Object.DestroyImmediate(truck);
            Object.DestroyImmediate(root);
        }
    }

    [TestCase("BigBag",0f,true,true)]
    [TestCase("BigBag",250f,false,true)]
    [TestCase("BigBag",500f,false,true)]
    [TestCase("SmallBag",500f,true,false)]
    [TestCase("Crate",500f,true,false)]
    public void HandsAndForkliftRespectContainerTypeAndFill(string type,float kilograms,bool handCarry,bool lift)
    {
        var host=new GameObject("Forklift cargo test");
        try
        {
            var bag=host.AddComponent<GrainContainer>();var data=new SerializedObject(bag);
            data.FindProperty("cargoTypeId").stringValue=type;data.FindProperty("capacityKg").floatValue=500;
            data.FindProperty("kilograms").floatValue=kilograms;data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bag.CanCarry,Is.EqualTo(handCarry));Assert.That(ForkliftController.CanLift(bag),Is.EqualTo(lift));
            data.Update();data.FindProperty("canBeCarried").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(bag.CanBeCarried);
            Assert.IsFalse(bag.CanCarry,"The carry flag must also apply to empty Big Bags.");
            Assert.That(ForkliftController.CanLift(bag),Is.EqualTo(lift),"Hand-carry restrictions must not change forklift eligibility.");
            var interaction=host.AddComponent<BagInteraction>();
            Assert.IsTrue(interaction.TryGetPrompt(bag,out var prompt));
            Assert.AreNotEqual("Take",prompt.Action);
            Assert.IsFalse(prompt.ShowKey);
            bag.SetCarried(true);Assert.IsFalse(bag.CanCarry);Assert.IsFalse(ForkliftController.CanLift(bag));
        }
        finally{Object.DestroyImmediate(host);}
    }
    [Test] public void ContactLoadsAndDropRequiresSupportAndDoesNotImmediatelyReload()
    {
        var root=new GameObject("Forklift contact regression");var bagObject=new GameObject("Full big bag");GameObject floor=null;
        try
        {
            root.transform.position=new Vector3(25000,1000,25000);
            var forklift=root.AddComponent<ForkliftController>();
            var anchor=new GameObject("Fork contact").transform;anchor.SetParent(root.transform,false);anchor.localPosition=new Vector3(0,.2f,2);
            var config=new SerializedObject(forklift);config.FindProperty("forkAnchor").objectReferenceValue=anchor;config.ApplyModifiedPropertiesWithoutUndo();
            bagObject.AddComponent<BoxCollider>();var bag=bagObject.AddComponent<GrainContainer>();
            var data=new SerializedObject(bag);data.FindProperty("cargoTypeId").stringValue="BigBag";data.FindProperty("capacityKg").floatValue=500;data.FindProperty("kilograms").floatValue=500;data.ApplyModifiedPropertiesWithoutUndo();
            bag.transform.position=root.transform.position+new Vector3(0,.5f,3.2f);Physics.SyncTransforms();
            Assert.IsFalse(forklift.TryAutoLift(bag),"Nearby bags must wait for contact.");
            bag.transform.position=root.transform.position+new Vector3(0,.5f,2.4f);Physics.SyncTransforms();
            Assert.IsTrue(forklift.TryAutoLift(bag));Assert.IsTrue(bag.IsCarried);
            Assert.IsFalse(forklift.TryDrop(),"A missing floor must preserve the load.");Assert.AreSame(bag,forklift.Cargo);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=root.transform.position+new Vector3(0,-.1f,2);floor.transform.localScale=new Vector3(10,.2f,10);Physics.SyncTransforms();
            Vector3 releasePosition=bag.transform.position;
            Quaternion releaseRotation=bag.transform.rotation;
            Assert.IsTrue(forklift.TryDrop());Assert.IsFalse(bag.IsCarried);Assert.IsNull(forklift.Cargo);
            Assert.That(Vector3.Distance(releasePosition,bag.transform.position),Is.LessThan(.001f),"Release must preserve the bag's world position.");
            Assert.That(Quaternion.Angle(releaseRotation,bag.transform.rotation),Is.LessThan(.001f));
            Assert.IsNull(bag.transform.parent);
            Assert.IsTrue(bag.GetComponent<Rigidbody>().useGravity);
            Assert.IsFalse(bag.GetComponent<Rigidbody>().isKinematic);
            Assert.IsTrue(bag.GetComponent<BoxCollider>().enabled);
            Assert.IsFalse(forklift.TryAutoLift(bag),"Dropping must not immediately reattach the same bag.");
        }
        finally{Object.DestroyImmediate(bagObject);Object.DestroyImmediate(root);if(floor!=null)Object.DestroyImmediate(floor);}
    }

    [TestCase(0f)]
    [TestCase(250f)]
    [TestCase(500f)]
    public void ParkedVehicleBagTransfersWithoutSnappingAndCannotDropMidAnimation(float kilograms)
    {
        var root = new GameObject("Forklift transfer test");
        var truck = new GameObject("Parked vehicle");
        var bagObject = new GameObject("Vehicle big bag");
        try
        {
            root.transform.position = new Vector3(26000, 1000, 26000);
            var forklift = root.AddComponent<ForkliftController>();
            var anchor = new GameObject("Fork anchor").transform;
            anchor.SetParent(root.transform, false);anchor.localPosition = new Vector3(0, .2f, 2);
            var config = new SerializedObject(forklift);config.FindProperty("forkAnchor").objectReferenceValue=anchor;config.ApplyModifiedPropertiesWithoutUndo();
            var rig = truck.AddComponent<BicycleController>();
            var slot = new GameObject("Cargo slot").transform;slot.SetParent(truck.transform);
            slot.position = root.transform.position + new Vector3(0, 1.8f, 3.5f);
            rig.cargoSlots = new[] {slot};
            bagObject.AddComponent<BoxCollider>();
            var bag = bagObject.AddComponent<GrainContainer>();
            var data = new SerializedObject(bag);data.FindProperty("cargoTypeId").stringValue="BigBag";
            data.FindProperty("capacityKg").floatValue=500;data.FindProperty("kilograms").floatValue=kilograms;data.ApplyModifiedPropertiesWithoutUndo();
            bag.transform.SetParent(slot);bag.transform.position=root.transform.position+new Vector3(0,1.8f,3.5f);
            Vector3 start = bag.transform.position;Physics.SyncTransforms();
            Assert.IsFalse(forklift.TryAutoLift(bag), "Vehicle unloading must wait for the F action.");
            Assert.IsTrue(forklift.TryLift(bag), "Explicit unloading should not require ground contact.");
            Assert.IsTrue(forklift.IsTransferring);Assert.AreSame(bag,forklift.Cargo);
            Assert.That(Vector3.Distance(start,bag.transform.position),Is.LessThan(.001f));
            Assert.IsFalse(rig.HasCargo(bag.transform));Assert.IsTrue(bag.IsCarried);
            Assert.IsFalse(forklift.TryDrop());
            Assert.IsFalse(forklift.TryLoadOntoVehicle(slot), "Cannot transfer again during unloading.");
            typeof(ForkliftController).GetField("transferElapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(forklift,2f);
            typeof(ForkliftController).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(forklift,null);
            Assert.IsFalse(forklift.IsTransferring);
            Assert.That(bag.WorldBounds.min.y,Is.EqualTo(anchor.position.y+.55f).Within(.001f));
            var occupied = new GameObject("Other cargo");
            occupied.transform.SetParent(slot, false);
            Assert.IsFalse(forklift.TryLoadOntoVehicle(slot), "Occupied slots must preserve the fork load.");
            Object.DestroyImmediate(occupied);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = (anchor.position + slot.position) * .5f + Vector3.up * .25f;
                wall.transform.localScale = new Vector3(4, 4, .1f);
                Physics.SyncTransforms();
                Assert.IsFalse(forklift.TryLoadOntoVehicle(slot), "Walls must block loading.");
            }
            finally { Object.DestroyImmediate(wall); }
            Physics.SyncTransforms();
            Assert.IsTrue(forklift.TryLoadOntoVehicle(slot), "Empty, partial and full big bags can be loaded.");
            Assert.IsNull(forklift.Cargo);
            Assert.IsTrue(rig.HasCargo(bag.transform));
            Assert.IsFalse(bag.IsCarried);
            Assert.IsTrue(bag.GetComponent<Rigidbody>().isKinematic);
            Assert.IsTrue(bag.GetComponent<BoxCollider>().enabled);
            Assert.That(bag.Kilograms, Is.EqualTo(kilograms));
            Assert.IsFalse(forklift.TryAutoLift(bag), "Loading must not immediately auto-unload the bag.");
        }
        finally { Object.DestroyImmediate(bagObject);Object.DestroyImmediate(truck);Object.DestroyImmediate(root); }
    }

    [Test]
    public void BigBagPalletIsIncludedInContainerAndRackContactBounds()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/BigBag.prefab");
        var instance=Object.Instantiate(prefab);
        try
        {
            var bag=instance.GetComponent<GrainContainer>();
            var pallet=(BoxCollider)new SerializedObject(bag).FindProperty("palletCollider").objectReferenceValue;
            Assert.IsNotNull(pallet);Assert.IsTrue(pallet.transform.IsChildOf(bag.transform));
            Physics.SyncTransforms();
            Assert.That(bag.WorldBounds.min.y,Is.EqualTo(pallet.bounds.min.y).Within(.002f));
            var rackBounds=VehicleCargoRack.ContactBounds(bag.transform,bag.transform);
            Assert.That(Vector3.Distance(rackBounds.center,bag.LocalBounds.center),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(rackBounds.size,bag.LocalBounds.size),Is.LessThan(.001f));
            var rootBox=bag.GetComponent<BoxCollider>();
            Assert.That(rootBox.bounds.min.y,Is.GreaterThanOrEqualTo(pallet.bounds.max.y-.01f));
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
