using System;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateCarryMotion
{
    private static int checks, peakAirborne;
    private static PlayerController player;
    private static ShovelTool tool;
    private static ShovelGrainPhysics grains;
    public static async Task<string> Main()
    {
        if (!Application.isPlaying) throw new Exception("Start fresh Play Mode first.");
        Application.runInBackground = true;
        checks = peakAirborne = 0;
        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        tool = player.GetComponentInChildren<ShovelTool>();
        grains = player.GetComponentInChildren<ShovelGrainPhysics>();
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var sack = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        player.enabled = false; player.characterController.enabled = false;
        player.transform.position = pile.transform.position + new Vector3(0, .1f, -3f);
        Vector3 point = pile.transform.position + new Vector3(0, 0, -1.7f);
        point.y = pile.SurfaceHeight(point);
        player.characterCamera.transform.LookAt(point);
        Physics.SyncTransforms();
        Check(tool.TryUse(player.characterCamera, player.transform), "Scoop starts");
        await Step(110);
        Check(tool.Kilograms > 4.99f && tool.CarryBlend > .99f, "Loaded shovel remains in straight carrying pose");
        Check(grains.KernelCount == 192 && grains.PackedKernelCount > 200, "Full bowl has a packed 3D interior and moving top layer");
        Vector3 up = player.characterCamera.transform.InverseTransformDirection(grains.transform.up);
        Check(Mathf.Abs(up.x) < .03f && up.y > .93f, "Loaded bowl is straight in Z while preserving its forward tilt");
        float kg = tool.Kilograms; double mass = pile.Kilograms;
        int emitted = grains.CosmeticEmittedCount;
        Quaternion original = player.characterCamera.transform.rotation;
        for (int i = 0; i < 25; i++)
        {
            player.characterCamera.transform.rotation = Quaternion.AngleAxis((i + 1) * 8f, Vector3.up) * original;
            await Step(1);
        }
        float rightRoll = player.InstrumentTurnRoll;
        for (int i = 0; i < 50; i++)
        {
            player.characterCamera.transform.rotation = Quaternion.AngleAxis(200f - (i + 1) * 8f, Vector3.up) * original;
            await Step(1);
        }
        Check(Mathf.Abs(rightRoll) > 2f && Mathf.Sign(rightRoll) != Mathf.Sign(player.InstrumentTurnRoll), "Fast left/right turns produce opposing Z roll");
        Check(grains.CosmeticEmittedCount > emitted && peakAirborne > 0, "Rapid camera turns eject cosmetic kernels");
        emitted = grains.CosmeticEmittedCount;
        for (int i = 0; i < 24; i++)
        {
            player.transform.position += Vector3.right * (i % 2 == 0 ? .09f : -.09f);
            await Step(1);
        }
        Check(grains.CosmeticEmittedCount > emitted, "Abrupt translation also makes kernels jump");
        Check(tool.Kilograms == kg && Math.Abs(pile.Kilograms - mass) < .001 && sack.Kilograms == 0,
            "Cosmetic spills do not alter shovel, pile or sack mass");
        Check(grains.AirborneCount <= 256, "Airborne population remains bounded");
        await Step(150);
        Check(grains.AirborneCount == 0 && Mathf.Abs(player.InstrumentTurnRoll) < .1f, "Spills expire and roll settles when motion stops");
        player.transform.position = sack.transform.position + new Vector3(0, .1f, -1.4f);
        player.characterCamera.transform.LookAt(sack.PourPoint);
        Physics.SyncTransforms();
        Check(tool.TryUse(player.characterCamera, player.transform), "Unload starts");
        await Step(120);
        Check(tool.Kilograms < .001f && tool.CarryBlend < .001f && grains.PackedKernelCount == 0 && grains.KernelCount == 0,
            "Unloading removes all visible corn and restores the empty carrying pose");
        Check(Mathf.Abs(sack.Kilograms - kg) < .001f, "All carried kilograms still reach the sack");
        emitted = grains.CosmeticEmittedCount;
        for (int i = 0; i < 30; i++)
        {
            player.characterCamera.transform.Rotate(Vector3.up, 12f, Space.World);
            await Step(1);
        }
        Check(grains.CosmeticEmittedCount == emitted, "Empty shovel cannot create cosmetic corn");
        return "PASS: " + checks + " carry/motion checks; peak airborne=" + peakAirborne +
            ". Full interior, loaded leveling, opposite turn roll, rotation/translation spills, unchanged mass, expiry and empty state verified.";
    }
    private static async Task Step(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            tool.Tick(1f / 60f); player.SendMessage("LateUpdate");
            await Task.Delay(17);
            peakAirborne = Mathf.Max(peakAirborne, grains.AirborneCount);
        }
    }
    private static void Check(bool value, string description)
    {
        checks++;
        if (!value) throw new Exception("Check " + checks + ": " + description);
    }
}
