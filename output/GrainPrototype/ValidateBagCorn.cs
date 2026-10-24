using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateBagCorn
{
    static int checks;
    static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks++; }
    static float Height(BagCornSurface detail)
    {
        var matrices = (Matrix4x4[])typeof(BagCornSurface).GetField("first", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(detail);
        float sum = 0;
        for (int i = 0; i < Mathf.Min(1023, detail.KernelCount); i++) sum += matrices[i].m13;
        return sum / Mathf.Min(1023, detail.KernelCount);
    }
    public static async Task<string> Main()
    {
        Application.runInBackground = true;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        player.enabled = false; player.characterController.enabled = false;
        var bag = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        var detail = bag.GetComponent<BagCornSurface>();
        await Task.Delay(100);
        Check(detail.KernelCount == 0, "Empty bag must have no kernels");
        float last = float.NegativeInfinity;
        foreach (float amount in new[] {5f, 20f, 25f})
        {
            bag.Add(amount); await Task.Delay(100);
            Check(detail.KernelCount > 100 && detail.KernelCount <= 2046, "Filled bag must show bounded kernel population");
            float height = Height(detail);
            Check(height > last + .01f, "Corn surface must rise with received kilograms");
            Check(height < bag.WorldBounds.max.y, "Corn must remain below rim");
            last = height;
        }
        player.transform.position = bag.transform.position + new Vector3(0, .1f, -1.4f);
        player.characterCamera.transform.LookAt(bag.WorldBounds.center + Vector3.up * .2f);
        ScreenCapture.CaptureScreenshot("output/GrainPrototype/BagCornFull.png"); await Task.Delay(100);
        Check(bag.Finish(), "Full bag can still finish");
        await Task.Delay(850);
        Check(detail.KernelCount == 0 && bag.IsFinished && bag.Kilograms == 50, "Closed bag hides kernels without changing mass");
        foreach (var other in UnityEngine.Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == bag) continue;
            other.gameObject.SetActive(true); other.Add(other.CapacityKg); await Task.Delay(100);
            var large = other.GetComponent<BagCornSurface>();
            Check(large != null && large.KernelCount > 500 && large.KernelCount <= 2046, "Scaled alternate sack has fitted 3D corn");
            other.gameObject.SetActive(false);
            Check(large.KernelCount == 0, "Disabled bag stops rendering kernels");
        }
        return "PASS: " + checks + " bag corn checks: empty, rising 5/25/50kg, bounds, closure, mass, scaled sack and disable.";
    }
}
