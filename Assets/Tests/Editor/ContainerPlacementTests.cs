using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ContainerPlacementTests
{
    [Test]
    public void CrowPerchUsesCargoPositionWhenItsColliderIsDisabled()
    {
        var cargo = Create("Transported barrel", origin + Vector3.up * 2);
        var box = cargo.AddComponent<BoxCollider>();
        box.center = Vector3.up * .5f;
        box.size = new Vector3(.7f, 1f, .7f);
        var container = cargo.AddComponent<GrainContainer>();
        var bird = Create("Crow", origin + Vector3.up * 4).AddComponent<GrainPest>();
        bird.Initialize(container, true, null, null);
        var perch = typeof(GrainPest).GetMethod("CrowPerchPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        Physics.SyncTransforms();
        Vector3 enabledPoint = (Vector3)perch.Invoke(bird, null);
        box.enabled = false;
        Physics.SyncTransforms();
        Vector3 disabledPoint = (Vector3)perch.Invoke(bird, null);
        Assert.That(Vector3.Distance(enabledPoint, disabledPoint), Is.LessThan(.001f));
        Assert.That(disabledPoint.y, Is.GreaterThan(container.WorldBounds.max.y));
        Assert.That(Vector3.Distance(disabledPoint, cargo.transform.position), Is.LessThan(2f));
    }

    [Test]
    public void CrowAboveCargoCanBeHitAtHeadHeightByRayAndBroomSweep()
    {
        var cargo = Create("Elevated barrel", origin + Vector3.up * 2);
        var barrel = cargo.AddComponent<BoxCollider>();
        barrel.size = new Vector3(.7f, 1f, .7f);
        var container = cargo.AddComponent<GrainContainer>();
        var crowObject = Create("Perched crow", origin + Vector3.up * 2.5f);
        var crow = crowObject.AddComponent<GrainPest>();
        crow.Initialize(container, true, null, null);
        Physics.SyncTransforms();
        var ray = new Ray(crowObject.transform.position + Vector3.up * .65f - Vector3.forward * 2,
            Vector3.forward);
        Assert.IsTrue(Physics.Raycast(ray, out var shot, 3f, ~0, QueryTriggerInteraction.Collide));
        Assert.That(shot.collider.GetComponent<GrainPest>(), Is.SameAs(crow));
        Assert.IsTrue(Physics.SphereCast(ray, .22f, out var sweep, 3f, ~0, QueryTriggerInteraction.Collide));
        Assert.That(sweep.collider.GetComponent<GrainPest>(), Is.SameAs(crow));
    }

    [TestCase("Barrel")]
    [TestCase("BigBag")]
    public void DroppedContainerRestoresSolidContacts(string prefabName)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Gameplay/Containers/" + prefabName + ".prefab");
        var instance = Object.Instantiate(prefab, origin, prefab.transform.rotation);
        objects.Add(instance);
        var container = instance.GetComponent<GrainContainer>();
        container.RefreshFillPresentation();
        var box = instance.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "Portable containers need a solid root collider.");
        Assert.That(box.size.sqrMagnitude, Is.GreaterThan(.01f));
        var body = instance.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.detectCollisions = false;
        container.SetCarried(true);
        foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        container.SetCarried(false);
        Assert.IsTrue(box.enabled);
        Assert.IsFalse(box.isTrigger);
        Assert.IsTrue(body.detectCollisions);
        if (prefabName == "BigBag")
        {
            var pallet = (BoxCollider)typeof(GrainContainer).GetField("palletCollider",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(container);
            Assert.IsTrue(pallet.enabled);
            Assert.IsFalse(pallet.isTrigger);
        }
        else
        {
            Bounds empty = new Bounds(box.center, box.size);
            container.Add(container.CapacityKg);
            Assert.That(box.center, Is.EqualTo(empty.center));
            Assert.That(box.size, Is.EqualTo(empty.size), "Rigid barrel bounds must not shrink with fill.");
        }
    }

    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Vector3 origin = new Vector3(10000, 10000, 10000);
    private Camera view;
    private GameObject actor;

    [SetUp]
    public void SetUp()
    {
        actor = Create("Placement actor", origin);
        var cameraObject = Create("Placement camera", origin + Vector3.up * 1.6f);
        cameraObject.transform.SetParent(actor.transform);
        view = cameraObject.AddComponent<Camera>();
        var floor = Create("Floor", origin + Vector3.down * .05f);
        floor.transform.localScale = new Vector3(20f, .1f, 20f);
        floor.AddComponent<BoxCollider>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
    }

    [TestCase(true, .12f)]
    [TestCase(false, .12f)]
    [TestCase(true, .5f)]
    [TestCase(false, .5f)]
    public void FlatAndTallContainersCanBePlacedOnClearGround(bool bag, float halfHeight)
    {
        var carrier = CreateCarrier(bag, halfHeight);
        Assert.IsTrue(FindPosition(carrier, bag), "Clear ground must not block the container's path.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WallStillPreventsPlacingBehindIt(bool bag)
    {
        var wall = Create("Wall", origin + Vector3.forward * .8f + Vector3.up * 1.5f);
        wall.transform.localScale = new Vector3(10, 3, .15f);
        wall.AddComponent<BoxCollider>();
        var carrier = CreateCarrier(bag, .12f);
        Assert.IsFalse(FindPosition(carrier, bag));
    }

    private Component CreateCarrier(bool bag, float halfHeight)
    {
        if (bag)
        {
            var carrier = actor.AddComponent<BagInteraction>();
            SetField(carrier, "viewCamera", view);
            SetField(carrier, "worldExtents", new Vector3(.33f, halfHeight, .28f));
            return carrier;
        }
        var items = actor.AddComponent<PlayerItemCarry>();
        SetField(items, "view", view);
        SetField(items, "extents", new Vector3(.33f, halfHeight, .28f));
        return items;
    }

    private bool FindPosition(Component carrier, bool bag)
    {
        Physics.SyncTransforms();
        return (bool)carrier.GetType().GetMethod(bag ? "FindDropPosition" : "FindPosition",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(carrier, new object[] { Vector3.zero });
    }

    private GameObject Create(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        objects.Add(go);
        return go;
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
