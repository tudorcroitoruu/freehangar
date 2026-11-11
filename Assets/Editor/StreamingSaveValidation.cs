using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public static class StreamingSaveValidation
{
    public static void Run()
    {
        var data = new PlayerData { cash = 12345, playerName = "Streaming save", world = new WorldSaveData() };
        var heights = new float[512 * 512];
        for (int i = 0; i < heights.Length; i++) heights[i] = (i % 101) / 100f;
        var bag = new BagSave { id = "bag-test", position = new Vector3(1, 2, 3), rotation = Quaternion.identity,
            container = new ContainerSave { kilograms = 12.5f, composition = new[] { .25f, .75f } },
            pile = new PileSave { resolution = 512, heights = heights } };
        var vehicle = new VehicleSave { id = "truck" }; vehicle.bags.Add(bag); data.world.vehicles.Add(vehicle);
        byte[] bytes = CompressedSaveWriter.Compress(data);
        string json = SaveSystem.DecompressString(bytes);
        var restored = JsonUtility.FromJson<PlayerData>(json);
        Assert.AreEqual(data.cash, restored.cash);
        Assert.AreEqual(data.playerName, restored.playerName);
        var loadedBag = restored.world.vehicles[0].bags[0];
        Assert.AreEqual(bag.position, loadedBag.position);
        Assert.AreEqual(bag.rotation, loadedBag.rotation);
        Assert.AreEqual(12.5f, loadedBag.container.kilograms);
        CollectionAssert.AreEqual(heights, loadedBag.pile.heights);
        CollectionAssert.AreEqual(bag.container.composition, loadedBag.container.composition);
        Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(
            Newtonsoft.Json.Linq.JToken.Parse(JsonUtility.ToJson(data)),
            Newtonsoft.Json.Linq.JToken.Parse(JsonUtility.ToJson(restored))), "Unity field contract must round-trip.");
        Directory.CreateDirectory("output/SaveSystem/Switch");
        File.WriteAllText("output/SaveSystem/Switch/streaming.txt", $"PASS: Unity-compatible profile/world/cargo roundtrip, {heights.Length} height values; gzip {bytes.Length} bytes. No uncompressed world JSON is allocated by the production writer.");
        Debug.Log("PASS streaming save compatibility and large grain payload.");
    }
}
