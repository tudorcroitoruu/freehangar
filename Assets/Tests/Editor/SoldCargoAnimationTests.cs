using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SoldCargoAnimationTests
{
    [Test]
    public void PaidCargoIsImmediatelyUncollectableAndCannotBeWeighedAgain()
    {
        var scale = new GameObject("Sale scale test");
        SoldCargoAnimation animation = null;
        try
        {
            scale.transform.position = new Vector3(30000, 1000, 30000);
            var area = scale.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = Vector3.one * 10;
            var zone = scale.AddComponent<SellingZone>();
            typeof(SellingZone).GetField("allGrainsSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(zone, new[] { new GrainElementSettings { grainType = GrainType.Corn, grainPrice = 2 } });
            var bag = GameObject.CreatePrimitive(PrimitiveType.Cube); bag.transform.SetParent(scale.transform, false);
            var cargo = bag.AddComponent<GrainContainer>(); cargo.SetDemonstrationFill(10);
            var body = bag.AddComponent<Rigidbody>();
            Physics.SyncTransforms();
            var collected = new List<GrainContainer>(); zone.CollectContainers(collected); Assert.AreEqual(1, collected.Count);
            animation = SoldCargoAnimation.Begin(collected);
            Assert.IsTrue(bag.activeSelf, "Keep visuals visible during the sale animation.");
            Assert.IsFalse(cargo.enabled); Assert.IsFalse(cargo.CanCarry);
            Assert.IsFalse(bag.GetComponent<Collider>().enabled); Assert.IsTrue(body.isKinematic);
            Physics.SyncTransforms(); zone.CollectContainers(collected); Assert.IsEmpty(collected);
        }
        finally
        {
            if (animation != null) Object.DestroyImmediate(animation.gameObject);
            Object.DestroyImmediate(scale);
        }
    }

    [Test]
    public void WholeBatchRisesShrinksAndDisappearsAfterPointEightSeconds()
    {
        var first = new GameObject("First sold bag"); var second = new GameObject("Second sold bag");
        first.transform.position = new Vector3(30000, 1000, 30000);
        second.transform.position = first.transform.position + Vector3.right;
        first.transform.localScale = new Vector3(1, 2, 3);
        var origin = first.transform.position; var scale = first.transform.localScale;
        var animation = SoldCargoAnimation.Begin(new[] { first.AddComponent<GrainContainer>(), second.AddComponent<GrainContainer>() });
        try
        {
            var advance = typeof(SoldCargoAnimation).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            advance.Invoke(animation, new object[] { .4f });
            Assert.Greater(first.transform.position.y, origin.y);
            Assert.Less(first.transform.localScale.magnitude, scale.magnitude);
            Assert.That(Vector3.Distance(first.transform.position, second.transform.position), Is.EqualTo(1).Within(.001f));
            Assert.IsTrue(first.activeSelf); Assert.IsTrue(second.activeSelf);
            advance.Invoke(animation, new object[] { .4f });
            Assert.IsTrue(first == null); Assert.IsTrue(second == null); Assert.IsTrue(animation == null);
        }
        finally { if (animation != null) Object.DestroyImmediate(animation.gameObject); }
    }
}
