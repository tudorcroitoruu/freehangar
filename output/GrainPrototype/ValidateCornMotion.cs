using System;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateCornMotion
{
    private static int checks;
    private static Transform grip;
    private static Vector3 restGrip;
    private static float maximumGripTravel;
    public static async Task<string> Main()
    {
        if (!Application.isPlaying) throw new Exception("Start a fresh Play Mode session first.");
        Application.runInBackground = true;
        checks = 0;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var tool = player.GetComponentInChildren<ShovelTool>();
        var grains = player.GetComponentInChildren<ShovelGrainPhysics>();
        var data = new UnityEditor.SerializedObject(tool);
        grip = (Transform)data.FindProperty("gripAnchor").objectReferenceValue;
        Check(grip != null, "Handle grip anchor is assigned");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        restGrip = (Vector3)typeof(ShovelTool).GetField("restPosition", flags).GetValue(tool) +
            (Quaternion)typeof(ShovelTool).GetField("restRotation", flags).GetValue(tool) *
            (Vector3)typeof(ShovelTool).GetField("gripInHands", flags).GetValue(tool);
        maximumGripTravel = 0;
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var container = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        CheckColliderFit(container);
        player.enabled = false;
        player.characterController.enabled = false;
        player.characterCamera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        double initial = pile.Kilograms;
        Check(tool.TryUse(player.characterCamera, player.transform), "Empty sky starts a gesture");
        await Step(player, tool, .2f);
        Check(tool.IsBusy && tool.PositionOffset.magnitude > .01f && Quaternion.Angle(tool.RotationOffset, Quaternion.identity) > 1, "Dry swing moves and rotates");
        await Step(player, tool, 1.1f);
        Check(!tool.IsBusy && tool.Kilograms == 0 && Math.Abs(pile.Kilograms - initial) < .01, "Dry swing completes without material");
        Check(grains.ScoopEmittedCount == 0, "Dry swing emits no pile kernels");
        Check(Mathf.Abs(tool.CapacityKg - 6.5f) < .001f, "Capacity increased from 5 to 6.5 kg");
        AimPile(player, pile);
        Check(tool.TryUse(player.characterCamera, player.transform), "Scoop starts");
        await Step(player, tool, .29f);
        Check(tool.Kilograms == 0, "Windup does not create corn");
        Check(grains.ScoopEmittedCount == 0, "Windup emits no pile kernels");
        await Step(player, tool, .30f);
        float partial = tool.Kilograms;
        Check(partial > 0 && partial < tool.CapacityKg, "Pickup is progressive");
        Check(grains.KernelCount > 0 && grains.KernelCount < 192, "Partial load has a partial particle population");
        Check(grains.ScoopEmittedCount > 0 && grains.ScoopEmittedCount < 96 && grains.AirborneCount > 0, "Actual pickup produces progressive airborne pile kernels");
        ScreenCapture.CaptureScreenshot("output/GrainPrototype/ScoopFeedback.png");
        await Step(player, tool, .75f);
        Check(!tool.IsBusy && Mathf.Abs(tool.Kilograms - tool.CapacityKg) < .01f, "Full scoop completes");
        int scoopParticles = grains.ScoopEmittedCount;
        Check(scoopParticles >= 94 && scoopParticles <= 96, "Full scoop has a bounded 96-kernel feedback budget");
        Check(Math.Abs(initial - pile.Kilograms - tool.Kilograms) < .02, "Pile and shovel conserve mass while settling");
        Check(grains.KernelCount == 192 && grains.MaximumBowlViolation < .001f, "Settled full load stays within the bowl");
        Check(tool.TryUse(player.characterCamera, player.transform), "Full shovel still animates");
        await Step(player, tool, 1.3f);
        Check(tool.Kilograms <= tool.CapacityKg + .001f, "Full-shovel gesture cannot add extra mass");
        Check(grains.ScoopEmittedCount == scoopParticles, "Full shovel does not emit fake collection feedback");
        player.transform.position = container.transform.position + new Vector3(0, .1f, -1.4f);
        player.characterCamera.transform.LookAt(container.GetComponent<Collider>().bounds.center);
        Physics.SyncTransforms();
        container.Add(48);
        Check(tool.TryUse(player.characterCamera, player.transform), "Pour starts");
        await Step(player, tool, .55f);
        Check(container.Kilograms > 48 && container.Kilograms < 50, "Sack fills progressively");
        Check(grains.AirborneCount > 0, "Pour produces airborne kernels");
        await Step(player, tool, .8f);
        Check(Mathf.Abs(container.Kilograms - 50) < .001f && Mathf.Abs(tool.Kilograms - (tool.CapacityKg - 2)) < .01f, "Partial capacity preserves remaining corn");
        CheckColliderFit(container);
        Check(tool.TryUse(player.characterCamera, player.transform), "Full sack still allows a gesture");
        tool.CancelAction();
        AimPile(player, pile);
        double beforeCancel = pile.Kilograms;
        Check(tool.TryUse(player.characterCamera, player.transform), "Cancellation setup");
        await Step(player, tool, .2f);
        tool.CancelAction();
        Check(Math.Abs(pile.Kilograms - beforeCancel) < .001, "Cancel before contact preserves material");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = player.characterCamera.transform.position + player.characterCamera.transform.forward * .6f;
        wall.transform.localScale = Vector3.one * .4f;
        Physics.SyncTransforms();
        Check(tool.TryUse(player.characterCamera, player.transform), "Wall still allows a gesture");
        await Step(player, tool, 1.3f);
        Check(Math.Abs(pile.Kilograms - beforeCancel) < .001, "Wall prevents collecting through it");
        Check(grains.ScoopEmittedCount == scoopParticles, "Cancelled/blocked actions cannot emit collection particles");
        UnityEngine.Object.Destroy(wall);
        player.characterCamera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        Check(tool.TryUse(player.characterCamera, player.transform), "Queue setup");
        Check(tool.TryUse(player.characterCamera, player.transform), "Click during action queues a gesture");
        await Step(player, tool, 1.3f);
        Check(tool.IsBusy, "Queued gesture starts after first");
        await Step(player, tool, 1.3f);
        Check(!tool.IsBusy, "Queue has bounded length");
        AimPile(player, pile);
        await Step(player, tool, 5f);
        Check(grains.MaximumBowlViolation < .001f, "Long settling run remains contained");
        Check(grains.AirborneCount == 0, "Airborne feedback expires after settling");
        Check(maximumGripTravel <= .1601f, "Handle never leaves its 16 cm movement envelope, including sky, wall, scoop and pour");
        return "PASS: " + checks + " animated Play Mode assertions. Maximum handle displacement=" + maximumGripTravel + "m. Physical kernels remained inside bowl; progressive pickup/pour and bounded queued/dry swings passed.";
    }

    private static void AimPile(PlayerController player, GrainPile pile)
    {
        player.transform.position = pile.transform.position + new Vector3(0, .1f, -3f);
        Vector3 point = pile.transform.position + new Vector3(0, 0, -1.7f);
        point.y = pile.SurfaceHeight(point);
        player.characterCamera.transform.LookAt(point);
        Physics.SyncTransforms();
    }

    private static async Task Step(PlayerController player, ShovelTool tool, float seconds)
    {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int i = 0; i < frames; i++)
        {
            tool.Tick(1f / 60f);
            player.SendMessage("LateUpdate");
            if (tool.IsBusy)
                maximumGripTravel = Mathf.Max(maximumGripTravel, Vector3.Distance(restGrip, tool.transform.parent.InverseTransformPoint(grip.position)));
            await Task.Delay(17);
        }
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Check " + checks + ": " + message);
    }

    private static void CheckColliderFit(GrainContainer container)
    {
        var body = Array.Find(container.GetComponentsInChildren<SkinnedMeshRenderer>(), r => r.name == "Sack_Body");
        var baked = new Mesh();
        body.BakeMesh(baked); baked.RecalculateBounds();
        Bounds reference = default;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = baked.bounds.center + Vector3.Scale(baked.bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 point = body.transform.TransformPoint(corner);
            if (i == 0) reference = new Bounds(point, Vector3.zero); else reference.Encapsulate(point);
        }
        UnityEngine.Object.Destroy(baked);
        Bounds actual = container.GetComponent<Collider>().bounds;
        Check(Vector3.Distance(reference.center, actual.center) < .025f && Mathf.Abs(reference.max.y - actual.max.y) < .025f,
            "Rotated sack collider follows the visible fill shape");
    }
}
