using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateBagCornPour
{
    public static async Task<string> Main()
    {
        Time.timeScale = 1; Application.runInBackground = true;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        player.enabled = false; player.characterController.enabled = false;
        var tool = player.GetComponentInChildren<ShovelTool>();
        var bag = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        // Isolate pouring from the user's editable pile placement/shape.
        typeof(ShovelTool).GetField("<Kilograms>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tool, tool.CapacityKg);
        player.GetComponentInChildren<ShovelGrainPhysics>().SetFill(1);
        player.transform.position = bag.transform.position + new Vector3(0, .1f, -1.4f);
        player.characterCamera.transform.LookAt(bag.WorldBounds.center); Physics.SyncTransforms();
        if (!tool.TryUse(player.characterCamera, player.transform)) throw new Exception("Pour did not start");
        for (int i = 0; i < 90; i++) { tool.Tick(1f / 60); player.SendMessage("LateUpdate"); await Task.Delay(17); }
        if (Mathf.Abs(bag.Kilograms - 6.5f) > .01f || tool.Kilograms > .001f || bag.GetComponent<BagCornSurface>().KernelCount < 100)
            throw new Exception("Pour must transfer 6.5 kg and display 3D bag corn");
        return "PASS: 6.5kg poured through actual ShovelTool action; received mass drives 3D bag surface.";
    }
}
