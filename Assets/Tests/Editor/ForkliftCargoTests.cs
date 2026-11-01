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
    }}
