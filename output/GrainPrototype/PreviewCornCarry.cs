using System.Reflection;
using UnityEngine;

public static class PreviewCornCarry
{
    public static string Main()
    {
        Application.runInBackground = true;
        var p = Object.FindFirstObjectByType<PlayerController>();
        var tool = p.GetComponentInChildren<ShovelTool>();
        var grains = p.GetComponentInChildren<ShovelGrainPhysics>();
        var pile = Object.FindFirstObjectByType<GrainPile>();
        p.enabled = false; p.characterController.enabled = false;
        var simulate = typeof(ShovelGrainPhysics).GetMethod("Simulate", BindingFlags.NonPublic | BindingFlags.Instance);
        p.transform.position = pile.transform.position + new Vector3(0, .1f, -3);
        Vector3 point = pile.transform.position + new Vector3(0, 0, -1.7f);
        point.y = pile.SurfaceHeight(point);
        p.characterCamera.transform.LookAt(point);
        Physics.SyncTransforms();
        tool.TryUse(p.characterCamera, p.transform);
        for (int i = 0; i < 100; i++)
        {
            tool.Tick(1f / 60); p.SendMessage("LateUpdate");
            simulate.Invoke(grains, new object[] { 1f / 60 });
        }
        p.transform.position = pile.transform.position + new Vector3(-2.4f, .1f, -3.6f);
        p.characterCamera.transform.LookAt(pile.transform.position + Vector3.up * .7f);
        for (int i = 0; i < 120; i++)
        {
            p.SendMessage("LateUpdate"); simulate.Invoke(grains, new object[] { 1f / 60 });
        }
        Time.timeScale = 0;
        grains.SendMessage("LateUpdate");
        return "Loaded preview: kg=" + tool.Kilograms + ", moving=" + grains.KernelCount + ", packed=" + grains.PackedKernelCount;
    }
}
