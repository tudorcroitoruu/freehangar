using System.Reflection;
using UnityEngine;

/// <summary>Play Mode only: freezes a representative pouring frame for a camera capture.</summary>
public static class PreviewCornPour
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new System.Exception("Start a fresh Play Mode session.");
        Application.runInBackground = true;
        var player = Object.FindFirstObjectByType<PlayerController>();
        var tool = player.GetComponentInChildren<ShovelTool>();
        var grains = player.GetComponentInChildren<ShovelGrainPhysics>();
        var pile = Object.FindFirstObjectByType<GrainPile>();
        var receiver = GameObject.Find("Sack").GetComponent<GrainContainer>();
        player.enabled = false;
        player.characterController.enabled = false;
        var simulate = typeof(ShovelGrainPhysics).GetMethod("Simulate", BindingFlags.NonPublic | BindingFlags.Instance);
        player.transform.position = pile.transform.position + new Vector3(0, .1f, -3f);
        Vector3 point = pile.transform.position + new Vector3(0, 0, -1.7f);
        point.y = pile.SurfaceHeight(point);
        player.characterCamera.transform.LookAt(point);
        Physics.SyncTransforms();
        tool.TryUse(player.characterCamera, player.transform);
        for (int i = 0; i < 90; i++)
        {
            tool.Tick(1f / 60f); player.SendMessage("LateUpdate");
            simulate.Invoke(grains, new object[] { 1f / 60f });
        }
        player.transform.position = receiver.transform.position + new Vector3(0, .1f, -1.4f);
        player.characterCamera.transform.LookAt(receiver.PourPoint);
        Physics.SyncTransforms();
        tool.TryUse(player.characterCamera, player.transform);
        for (int i = 0; i < 35; i++)
        {
            tool.Tick(1f / 60f); player.SendMessage("LateUpdate");
            simulate.Invoke(grains, new object[] { 1f / 60f });
        }
        Time.timeScale = 0;
        grains.SendMessage("LateUpdate");
        return "Pour pose frozen; capture camera, then leave Play Mode. Shovel=" + tool.Kilograms + ", sack=" + receiver.Kilograms + ", stream=" + grains.AirborneCount;
    }
}
