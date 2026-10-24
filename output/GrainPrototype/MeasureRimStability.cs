using System;
using System.Reflection;
using UnityEngine;

// Use after PreviewCornCarry. Hold the tool transform still to isolate its local solver.
public static class MeasureRimStability
{
    public static string Main()
    {
        var g = UnityEngine.Object.FindFirstObjectByType<ShovelGrainPhysics>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var step = typeof(ShovelGrainPhysics).GetMethod("Simulate", flags);
        var positions = (Vector3[])typeof(ShovelGrainPhysics).GetField("positions", flags).GetValue(g);
        var previous = new Vector3[positions.Length];
        float peak = 0; double distance = 0; int samples = 0;
        for (int frame = 0; frame < 300; frame++)
        {
            Array.Copy(positions, previous, positions.Length);
            step.Invoke(g, new object[] { 1f / 60f });
            if (frame < 180) continue;
            for (int i = 0; i < g.KernelCount; i++)
            {
                float d = Vector3.Distance(previous[i], positions[i]);
                peak = Mathf.Max(peak, d); distance += d; samples++;
            }
        }
        if (peak > .00001f || !g.IsLoadSleeping) throw new Exception("Idle rim still jitters");
        Quaternion original = g.transform.localRotation;
        try
        {
            g.transform.localRotation = original * Quaternion.Euler(0, 12, 0);
            step.Invoke(g, new object[] { 1f / 60f });
            if (g.IsLoadSleeping) throw new Exception("Sharp movement did not wake the load");
        }
        finally { g.transform.localRotation = original; }
        for (int frame = 0; frame < 120; frame++) step.Invoke(g, new object[] { 1f / 60f });
        if (!g.IsLoadSleeping) throw new Exception("Load did not settle again");
        g.SetFill(.5f);
        step.Invoke(g, new object[] { 1f / 60f });
        if (g.IsLoadSleeping || g.KernelCount >= 192) throw new Exception("Pour did not wake and release the load");
        return "PASS: stationary full bowl, final 2 seconds: peak local displacement/frame=" + peak +
            "m, mean=" + distance / Math.Max(1, samples) + "m. Sharp motion and pouring wake the load; it settles again.";
    }
}
