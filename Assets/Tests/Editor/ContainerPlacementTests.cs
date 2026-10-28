using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ContainerPlacementTests
{
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
