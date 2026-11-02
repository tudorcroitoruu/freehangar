using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ForkliftCargoTests
{
    [TestCase("BigBag",0f,true,false)]
    [TestCase("BigBag",250f,false,false)]
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
            Assert.IsTrue(forklift.TryDrop());Assert.IsFalse(bag.IsCarried);Assert.IsNull(forklift.Cargo);
            Assert.IsFalse(forklift.TryAutoLift(bag),"Dropping must not immediately reattach the same bag.");
        }
        finally{Object.DestroyImmediate(bagObject);Object.DestroyImmediate(root);if(floor!=null)Object.DestroyImmediate(floor);}
    }

    [Test]
    public void ParkedVehicleBagTransfersWithoutSnappingAndCannotDropMidAnimation()
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
            rig.cargoSlots = new[] {slot};
            bagObject.AddComponent<BoxCollider>();
            var bag = bagObject.AddComponent<GrainContainer>();
            var data = new SerializedObject(bag);data.FindProperty("cargoTypeId").stringValue="BigBag";
            data.FindProperty("capacityKg").floatValue=500;data.FindProperty("kilograms").floatValue=500;data.ApplyModifiedPropertiesWithoutUndo();
            bag.transform.SetParent(slot);bag.transform.position=root.transform.position+new Vector3(0,1.8f,3.5f);
            Vector3 start = bag.transform.position;Physics.SyncTransforms();
            Assert.IsTrue(forklift.TryAutoLift(bag), "Stowed cargo should not require ground contact.");
            Assert.IsTrue(forklift.IsTransferring);Assert.AreSame(bag,forklift.Cargo);
            Assert.That(Vector3.Distance(start,bag.transform.position),Is.LessThan(.001f));
            Assert.IsFalse(rig.HasCargo(bag.transform));Assert.IsTrue(bag.IsCarried);
            Assert.IsFalse(forklift.TryDrop());
            typeof(ForkliftController).GetField("transferElapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(forklift,2f);
            typeof(ForkliftController).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(forklift,null);
            Assert.IsFalse(forklift.IsTransferring);
            Assert.That(bag.WorldBounds.min.y,Is.EqualTo(anchor.position.y+.55f).Within(.001f));
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
