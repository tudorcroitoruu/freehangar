using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldSaveData
{
    public int version = 2;
    public string scene;
    public List<BagSave> bags = new List<BagSave>();
    public List<VehicleSave> vehicles = new List<VehicleSave>();
    public List<WheelbarrowSave> wheelbarrows = new List<WheelbarrowSave>();
    public List<WorldObjectSave> items = new List<WorldObjectSave>();

    // Traversal for the scene adapter; never serialized as a duplicate list.
    public IEnumerable<WorldObjectSave> Records()
    {
        foreach (var vehicle in vehicles) { yield return vehicle; foreach (var bag in vehicle.bags) yield return bag; }
        foreach (var barrow in wheelbarrows) { yield return barrow; foreach (var bag in barrow.bags) yield return bag; }
        foreach (var item in items) yield return item;
        foreach (var bag in bags) yield return bag;
    }
    public void NestCargo()
    {
        for (int i = bags.Count - 1; i >= 0; i--)
        {
            var bag = bags[i];
            var vehicle = vehicles.Find(v => v.id == bag.parent);
            var barrow = wheelbarrows.Find(v => v.id == bag.parent);
            if (vehicle != null) vehicle.bags.Add(bag);
            else if (barrow != null) barrow.bags.Add(bag);
            else continue;
            bags.RemoveAt(i);
        }
    }
    public List<string> authoredContainers = new List<string>();
    public Vector3 playerPosition;
    public Quaternion playerRotation = Quaternion.identity;
    public Quaternion lookRotation = Quaternion.identity;
    public Vector2 ridingLook;
    public float[] shovelCargo;
    public float stamina = -1, hunger;
    public int equippedTool;
    public string carriedBag, carriedItem, riddenVehicle, pushedWheelbarrow;
    public bool launchDoorOpen;
    public float introTime, launchTime, endingTime;
    public bool introPlaying, launchPlaying, endingPlaying;
}

[Serializable]
public class WorldObjectSave
{
    public string id, kind, prefab, parent, slot;
    public Vector3 position, scale = Vector3.one, localPosition;
    public Quaternion rotation = Quaternion.identity, localRotation = Quaternion.identity;
    public bool active, kinematic;
    public bool hasBody, gravity;
    public float mass = 1, linearDamping, angularDamping;
    public int inventoryLimit;
    public int barrierHits;
    public bool barrierExposed;
    public Vector3 slotPosition, slotScale = Vector3.one, originalScale = Vector3.one, poseScale;
    public Quaternion slotRotation = Quaternion.identity, poseRotation = Quaternion.identity;
    public bool placement, seatAtBottom;
    public Vector3 velocity, angularVelocity;
    public ContainerSave container;
    public PileSave pile;
    public List<BodySave> bodies = new List<BodySave>();
}

[Serializable] public sealed class BodySave
{
    public string path;
    public Vector3 position, velocity, angularVelocity;
    public Quaternion rotation;
}
[Serializable] public sealed class ContainerSave
{
    public float kilograms, closure;
    public int grain;
    public bool closing;
    public string achievementId;
}
[Serializable] public sealed class PileSave
{
    public float size;
    public int resolution;
    public double initialKilograms;
    public float[] heights;
    public List<GrainColumnSave> columns;
}
[Serializable] public sealed class GrainColumnSave
{
    public int index;
    public double volume;
    public float[] weights;
}

[Serializable] public sealed class BagSave : WorldObjectSave { }
[Serializable] public class VehicleSave : WorldObjectSave
{
    public List<BagSave> bags = new List<BagSave>();
}
[Serializable] public sealed class WheelbarrowSave : VehicleSave { }
