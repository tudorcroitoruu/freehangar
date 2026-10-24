using System.Reflection;
using UnityEngine;

public static class PreviewCornSpill
{
    public static string Main()
    {
        Time.timeScale = 1;
        var p = Object.FindFirstObjectByType<PlayerController>();
        var grains = p.GetComponentInChildren<ShovelGrainPhysics>();
        var tool = p.GetComponentInChildren<ShovelTool>();
        var simulate = typeof(ShovelGrainPhysics).GetMethod("Simulate", BindingFlags.Instance | BindingFlags.NonPublic);
        Quaternion rest = p.characterCamera.transform.rotation;
        float before = tool.Kilograms;
        for (int i = 0; i < 26; i++)
        {
            p.characterCamera.transform.rotation = Quaternion.AngleAxis(Mathf.Sin(i * .6f) * 13f, Vector3.up) * rest;
            p.SendMessage("LateUpdate");
            simulate.Invoke(grains, new object[] { 1f / 60 });
        }
        Time.timeScale = 0;
        grains.SendMessage("LateUpdate");
        return "Cosmetic spill: airborne=" + grains.AirborneCount + ", kg before=" + before + ", after=" + tool.Kilograms;
    }
}
