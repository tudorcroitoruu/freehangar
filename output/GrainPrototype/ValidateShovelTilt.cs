using System;
using System.Reflection;
using UnityEngine;

// Run after PreviewCornCarry in Play Mode, with a loaded shovel.
public static class ValidateShovelTilt
{
    public static string Main()
    {
        Time.timeScale = 1;
        var p = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var tool = p.GetComponentInChildren<ShovelTool>();
        var grains = p.GetComponentInChildren<ShovelGrainPhysics>();
        var data = new UnityEditor.SerializedObject(tool);
        var tip = (Transform)data.FindProperty("bladeTip").objectReferenceValue;
        var grip = (Transform)data.FindProperty("gripAnchor").objectReferenceValue;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var simulate = typeof(ShovelGrainPhysics).GetMethod("Simulate", flags);
        var positions = (Vector3[])typeof(ShovelGrainPhysics).GetField("positions", flags).GetValue(grains);
        Vector3 rest = (Vector3)typeof(ShovelTool).GetField("restPosition", flags).GetValue(tool);
        Quaternion restRotation = (Quaternion)typeof(ShovelTool).GetField("restRotation", flags).GetValue(tool);
        Vector3 gripLocal = (Vector3)typeof(ShovelTool).GetField("gripInHands", flags).GetValue(tool);
        Vector3 referenceGrip = rest + restRotation * gripLocal;
        float maximumTilt = 0, maximumTravel = 0;
        int cases = 0;
        float mass = tool.Kilograms;
        foreach (float phase in new[] { 0f, .25f, .5f, .8f })
        foreach (float pitch in new[] { -70f, 0f, 35f, 60f, 80f })
        foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
        foreach (float roll in new[] { -7f, 0f, 7f })
        {
            tool.CancelAction();
            p.characterCamera.transform.rotation = Quaternion.LookRotation(Vector3.up);
            if (phase > 0) { tool.TryUse(p.characterCamera, p.transform); tool.Tick(phase * 1.25f); }
            p.characterCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            tool.ComposePresentation(1f / 60f, Vector2.zero, roll, Vector3.zero, out Vector3 offset, out Quaternion rotation);
            tool.transform.localPosition = rest + offset;
            tool.transform.localRotation = restRotation * rotation;
            float down = -Mathf.Asin((tip.position - grip.position).normalized.y) * Mathf.Rad2Deg;
            float travel = Vector3.Distance(referenceGrip, tool.transform.parent.InverseTransformPoint(grip.position));
            if (down > 25.01f || down < -30.01f || travel > .1601f) throw new Exception("Tilt/grip limit failed: " + down + "/" + travel);
            simulate.Invoke(grains, new object[] { 1f / 60f });
            for (int i = 0; i < grains.KernelCount; i++)
                if (positions[i].y + .018f > .0751f) throw new Exception("Load exceeded its height cap");
            if (grains.MaximumBowlViolation > .001f) throw new Exception("Load escaped bowl bounds");
            maximumTilt = Mathf.Max(maximumTilt, down); maximumTravel = Mathf.Max(maximumTravel, travel); cases++;
        }
        tool.CancelAction();
        if (Mathf.Abs(tool.Kilograms - mass) > .001f) throw new Exception("Presentation changed carried mass");
        p.characterCamera.transform.rotation = Quaternion.Euler(80, 0, 0);
        tool.ComposePresentation(1f / 60f, Vector2.zero, 0, Vector3.zero, out Vector3 finalOffset, out Quaternion finalRotation);
        tool.transform.localPosition = rest + finalOffset; tool.transform.localRotation = restRotation * finalRotation;
        Time.timeScale = 0;
        return "PASS: " + cases + " loaded idle/action, pitch/yaw/roll combinations; 30-degree upward limit and 7.5cm corn height cap. Max down=" + maximumTilt +
            " degrees, max grip travel=" + maximumTravel + "m; mass unchanged. Frozen at camera pitch 80 for visual inspection.";
    }
}
