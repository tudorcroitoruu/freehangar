using System;
using UnityEditor;
using UnityEngine;

public static class GrainBehaviorValidation
{
    [MenuItem("Tools/Grain/Validate mass and capacity")]
    public static void Run()
    {
        var field = new GrainHeightField(33, 6, 1.8f);
        double start = field.Volume;
        float before = field.Sample(0, 0);
        float removed = field.Scoop(0, 0, .4f, .18f, 5f / 720f);
        Check(removed > 0 && removed <= 5f / 720f + .000001f, "Scoop respects tool capacity");
        Check(field.Sample(0, 0) < before, "Scoop makes a visible depression");
        Check(Math.Abs(start - field.Volume - removed) < .000001, "Scoop conserves volume");
        double afterScoop = field.Volume;
        for (int i = 0; i < 600; i++) field.Settle(32, .45f);
        Check(Math.Abs(field.Volume - afterScoop) < .00001, "Settling conserves volume");
        var crater = new GrainHeightField(33, 6, 1.8f);
        crater.Scoop(0, 0, .5f, 1f, 1f);
        float craterBottom = crater.Sample(0, 0);
        for (int i = 0; i < 120; i++) crater.Settle(32, .45f);
        Check(crater.Sample(0, 0) > craterBottom, "Surrounding grain falls into a deep scoop");
        ValidateLocalRefill();
        foreach (float height in field.Heights) Check(height >= 0 && !float.IsNaN(height), "Nonnegative finite heights");
        double beforeMiss = field.Volume;
        Check(field.Scoop(100, 100, .4f, .18f, 1) == 0 && field.Volume == beforeMiss, "Miss removes nothing");
        Check(field.Scoop(0, 0, .4f, .18f, 0) == 0, "Full tool removes nothing");
        var empty = new GrainHeightField(9, 2, 0);
        Check(empty.Scoop(0, 0, .4f, .18f, 1) == 0, "Empty pile removes nothing");
        var drain = new GrainHeightField(9, 2, .2f);
        double initial = drain.Volume, total = 0;
        for (int i = 0; i < 100; i++) total += drain.Scoop(0, 0, 10, 1, .01f);
        Check(drain.Volume < .000001 && Math.Abs(total - initial) < .000001, "Repeated scoops exhaust without duplication");
        var go = new GameObject("Temporary grain validation");
        try
        {
            var container = go.AddComponent<GrainContainer>();
            Check(container.Add(48) == 48, "Container accepts grain");
            Check(container.Add(5) == 2 && container.Kilograms == 50, "Partial fill leaves overflow on shovel");
            Check(container.Add(1) == 0 && container.Add(-1) == 0 && container.Add(float.NaN) == 0, "Full and invalid transfers rejected");
            container.enabled = false;
            Check(container.Add(1) == 0, "Disabled container rejected");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Debug.Log("Grain validation passed: capacity, excavation, conservation, settling, empty/missed scoops, exhaustion and invalid transfers.");
    }

    private static void ValidateLocalRefill()
    {
        var field = new GrainHeightField(33, 6, 1.8f);
        var untouched = (float[])field.Heights.Clone();
        Check(!field.Settle(32, .45f), "Unworked pile remains at rest");
        const float x = 0, z = -1.5f, radius = .4f, margin = .45f;
        double volume = field.Volume;
        float before = field.Sample(x, z);
        float removed = field.Scoop(x, z, radius, .18f, 5f / 720f, margin);
        float excavated = field.Sample(x, z);
        bool moved = false;
        for (int i = 0; i < 600; i++) moved |= field.Settle(32, .45f);
        float refilled = field.Sample(x, z);
        Check(moved && refilled > excavated + .001f, "Nearby grain visibly refills the worked spot");
        Check(refilled < before - .001f, "Refilled surface stays lower than before the scoop");
        Check(Math.Abs(volume - field.Volume - removed) < .00001, "Local refill conserves removed volume");
        bool donorLowered = false;
        for (int iz = 0; iz < field.Resolution; iz++)
        for (int ix = 0; ix < field.Resolution; ix++)
        {
            int i = iz * field.Resolution + ix;
            float dx = ix * field.Spacing - field.Size * .5f - x;
            float dz = iz * field.Spacing - field.Size * .5f - z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance > radius + margin) Check(field.Heights[i] == untouched[i], "Distant surface is exactly unchanged");
            if (distance > radius && field.Heights[i] < untouched[i] - .0001f) donorLowered = true;
            Check(field.Heights[i] <= untouched[i] + .000001f, "Refill never restores above the original surface");
        }
        Check(donorLowered, "Refill takes volume from nearby donors");
        Check(!field.Settle(32, .45f), "Local settling eventually sleeps");
        var peak = new GrainHeightField(33, 6, 1.8f);
        peak.Scoop(0, 0, .2f, .01f, .001f);
        float unsupported = peak.Sample(0, 0);
        for (int i = 0; i < 120; i++) peak.Settle(32, .45f);
        Check(peak.Sample(0, 0) == unsupported, "No refill without higher neighboring grain");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Grain validation failed: " + message);
    }
}
