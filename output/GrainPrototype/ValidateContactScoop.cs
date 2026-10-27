using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// Runs on an isolated runtime rig. Always removes fixtures and restores global state.
public static class ValidateContactScoop
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static string Main(bool capture = false, int captureStep = 85)
    {
        Check(Application.isPlaying, "Play mode required");
        var listeners = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(b => (b is GoalsManager || b is GrainProgression) && b.enabled).ToArray();
        foreach (var listener in listeners) listener.enabled = false;
        float timeScale = Time.timeScale;
        var cursor = Cursor.lockState;
        bool cursorVisible = Cursor.visible;
        var root = new GameObject("Contact scoop test fixtures") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            Time.timeScale = 1;
            Vector3 origin = new Vector3(1000, 0, 1000);
            var pileObject = new GameObject("Test pile");
            pileObject.transform.SetParent(root.transform);
            pileObject.transform.position = origin;
            var pile = pileObject.AddComponent<GrainPile>();
            var pileData = new SerializedObject(pile);
            pileData.FindProperty("fitToWalls").boolValue = false;
            pileData.ApplyModifiedPropertiesWithoutUndo();
            var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player.prefab"), root.transform);
            rig.transform.position = origin + new Vector3(0, .04f, -2.55f);
            var player = rig.GetComponent<PlayerController>();
            player.enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            var tool = rig.GetComponentInChildren<ShovelTool>();
            var grains = rig.GetComponentInChildren<ShovelGrainPhysics>();
            grains.enabled = false;
            if (capture) PrepareVisuals(pile, player, tool);
            root.SetActive(true);
            pile.Initialize();
            var cameraTransform = player.characterCamera.transform;
            Vector3 aim = origin + new Vector3(0, 0, -1.1f);
            aim.y = pile.SurfaceHeight(aim);
            cameraTransform.LookAt(aim);
            Physics.SyncTransforms();
            Vector3 rest = Field<Vector3>(tool, "restPosition");
            Quaternion restRotation = Field<Quaternion>(tool, "restRotation");
            var tip = Field<Transform>(tool, "bladeTip");
            var grip = Field<Transform>(tool, "gripAnchor");
            Vector3 restGrip = rest + restRotation * Field<Vector3>(tool, "gripInHands");
            var simulate = typeof(ShovelGrainPhysics).GetMethod("Simulate", Private);
            float duration = Field<float>(tool, "actionDuration");
            double before = pile.Kilograms;
            Check(tool.TryUse(player.characterCamera, rig.transform) && tool.IsScooping, "Nearby blade did not select the pile");
            Check(Vector2.Distance(new Vector2(aim.x, aim.z), new Vector2(Field<Vector3>(tool, "targetPoint").x, Field<Vector3>(tool, "targetPoint").z)) < .05f, "Scoop did not target the aim dot");
            var trace = new List<string>();
            float firstLoad = -1, maximumTravel = 0, lowestTip = float.PositiveInfinity, highestLift = float.NegativeInfinity;
            float partial = 0, maximumPenetration = 0;
            for (int i = 0; i < 90; i++)
            {
                float phase = (i + 1) / 60f / duration;
                tool.Tick(1f / 60f);
                tool.ComposePresentation(1f / 60f, Vector2.zero, 0, Vector3.zero, out var offset, out var rotation);
                tool.transform.localPosition = rest + offset;
                tool.transform.localRotation = restRotation * rotation;
                simulate.Invoke(grains, new object[] { 1f / 60f });
                if (tool.Kilograms > 0 && firstLoad < 0) firstLoad = phase;
                maximumTravel = Mathf.Max(maximumTravel, Vector3.Distance(restGrip, tool.transform.parent.InverseTransformPoint(grip.position)));
                float elevation = Mathf.Asin((tip.position - grip.position).normalized.y) * Mathf.Rad2Deg;
                Check(elevation >= -25.02f && elevation <= 30.02f, "Scoop exceeded shaft tilt limits");
                if (phase < .34f) Check(tool.Kilograms == 0, "Load appeared before insertion");
                if (phase > .4f && phase < .54f) { lowestTip = Mathf.Min(lowestTip, tip.position.y); partial = tool.Kilograms; }
                if (phase >= .34f && phase <= .58f)
                    maximumPenetration = Mathf.Max(maximumPenetration, pile.SurfaceHeight(tip.position) - tip.position.y);
                if (phase > .7f && phase < .85f) highestLift = Mathf.Max(highestLift, tip.position.y);
                if (i % 10 == 0) trace.Add($"phase={phase:F2} kg={tool.Kilograms:F2} tip={tip.position-origin} target={Field<Vector3>(tool, "targetPoint")-origin} grip={grip.position-origin} surface={pile.SurfaceHeight(tip.position):F3}");
                if (capture && i == captureStep)
                    Capture(player.characterCamera, grains, "Scoop-" + i);
            }
            Check(firstLoad >= .34f && firstLoad < .55f, "No contact-timed pickup: " + string.Join("; ", trace));
            Check(partial > 0 && partial <= tool.CapacityKg, "No progressive load");
            Check(tool.Kilograms > tool.CapacityKg * .9f, "Incomplete nearby scoop: " + string.Join("; ", trace));
            Check(highestLift > lowestTip + .12f, "No visible extraction arc");
            Check(maximumPenetration >= .06f, "Insertion too shallow: " + maximumPenetration + "; " + string.Join("; ", trace));
            Check(maximumTravel <= .6502f, "Handle left its grip envelope");
            Check(!tool.IsBusy && !tool.IsScooping, "Scoop did not finish/reset world-depth mode");
            Check(Math.Abs(before - pile.Kilograms - tool.Kilograms) < .025, "Scoop did not conserve mass");

            // Settle a full visual load and inspect the actual instancing output.
            grains.SetFill(1);
            for (int i = 0; i < 240; i++) simulate.Invoke(grains, new object[] { 1f / 60f });
            grains.SendMessage("LateUpdate");
            int aCount = 0, bCount = 0;
            float aHeight = float.NegativeInfinity, bHeight = float.NegativeInfinity;
            var matrices = Field<Matrix4x4[]>(grains, "matrices");
            for (int i = 0; i < grains.KernelCount + grains.PackedKernelCount; i++)
            {
                Vector3 local = grains.transform.InverseTransformPoint(matrices[i].GetColumn(3));
                if (local.z < -.035f) { aCount++; aHeight = Mathf.Max(aHeight, local.y); }
                if (local.z > .065f) { bCount++; bHeight = Mathf.Max(bHeight, local.y); }
            }
            Check(aCount > bCount && aCount > 0, $"Rear load not denser: A={aCount}, B={bCount}");
            Check(bCount == 0 || aHeight > bHeight + .005f, $"Rear load not higher than cutting edge: A={aHeight}, B={bHeight}, counts {aCount}/{bCount}");
            Check(grains.MaximumBowlViolation < .001f && grains.HighestLoadPoint <= .0752f, "Grain escaped the bowl/height cap");
            Check(grains.IsLoadSleeping, "Settled grain did not sleep");
            if (capture && captureStep < 0)
            {
                // Inspect the load from above without moving the held tool with the camera.
                tool.transform.SetParent(root.transform, true);
                Vector3 view = grains.transform.TransformPoint(new Vector3(.45f, .65f, -.55f));
                cameraTransform.SetPositionAndRotation(view, Quaternion.LookRotation(grains.transform.position - view, grains.transform.up));
                Capture(player.characterCamera, grains, "Loaded-A-to-B");
                // This inspection pose is outside the input fixture; the other runs cover input cases.
                return $"PASS: top view, A={aCount}/B={bCount}, height {aHeight:F3}/{bHeight:F3}m";
            }

            float fullLoad = tool.Kilograms;
            Check(tool.TryUse(player.characterCamera, rig.transform) && !tool.IsScooping, "Full shovel selected another scoop");
            tool.Tick(duration + .01f);
            Check(Mathf.Abs(tool.Kilograms - fullLoad) < .001f, "Full shovel overfilled");
            var bagObject = new GameObject("Test grain receiver");
            bagObject.transform.SetParent(root.transform);
            bagObject.transform.position = cameraTransform.position + cameraTransform.forward * .8f;
            bagObject.AddComponent<BoxCollider>().size = Vector3.one * .4f;
            var bag = bagObject.AddComponent<GrainContainer>();
            bag.Add(48);
            bagObject.transform.position += cameraTransform.right * .45f;
            Physics.SyncTransforms();
            tool.TryUse(player.characterCamera, rig.transform);
            Check(Field<GrainContainer>(tool, "pendingContainer") == null, "Off-dot bag was selected");
            tool.CancelAction();
            bagObject.transform.position -= cameraTransform.right * .45f;
            Physics.SyncTransforms();
            Check(tool.TryUse(player.characterCamera, rig.transform), "Pour did not start");
            Check(Field<GrainContainer>(tool, "pendingContainer") == bag, "Centered bag not selected");
            for (int i = 0; i < 90; i++) tool.Tick(1f / 60f);
            Check(Mathf.Abs(bag.Kilograms - 50) < .001f && Mathf.Abs(tool.Kilograms - (fullLoad - 2)) < .001f,
                "Final pour did not preserve overflow while capping bag");
            tool.Cargo.Clear();
            typeof(ShovelTool).GetProperty("Kilograms").SetValue(tool, 1f);
            grains.SetFill(1f / tool.CapacityKg);
            Check(tool.TryUse(player.characterCamera, rig.transform), "Already-full bag pour did not start");
            for (int i = 0; i < 90; i++) tool.Tick(1f / 60f);
            Check(Mathf.Abs(tool.Kilograms - 1f) < .001f && Mathf.Abs(bag.Kilograms - 50) < .001f,
                "Already-full bag discarded cargo or exceeded capacity");
            for (int i = 0; i < 180; i++) simulate.Invoke(grains, new object[] { 1f / 60f });
            grains.SendMessage("LateUpdate");
            Check(grains.KernelCount > 0,
                "Rejected full-bag pour hides retained grain on the shovel");
            Object.DestroyImmediate(bagObject);

            // Empty gestures, cancellation and an intervening solid wall must not invent corn.
            float collected = fullLoad;
            double remaining = pile.Kilograms;
            tool.Cargo.Clear();
            typeof(ShovelTool).GetProperty("Kilograms").SetValue(tool, 0f);
            grains.SetFill(0);
            cameraTransform.rotation = Quaternion.LookRotation(Vector3.up);
            Check(tool.TryUse(player.characterCamera, rig.transform) && !tool.IsScooping, "Sky swing unexpectedly scoops");
            tool.Tick(duration + .01f);
            Check(tool.Kilograms == 0, "Sky swing collected grain");
            cameraTransform.LookAt(aim);
            Physics.SyncTransforms();
            tool.TryUse(player.characterCamera, rig.transform);
            tool.Tick(duration * .15f);
            tool.CancelAction();
            Check(tool.Kilograms == 0 && !tool.IsScooping, "Cancellation retained pickup/action state");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position = cameraTransform.position + cameraTransform.forward * .5f;
            wall.transform.localScale = Vector3.one * .5f;
            Physics.SyncTransforms();
            tool.TryUse(player.characterCamera, rig.transform);
            tool.Tick(duration + .01f);
            Check(tool.Kilograms == 0 && Math.Abs(remaining - pile.Kilograms) < .001, "Blocked swing changed grain");
            Object.DestroyImmediate(wall);
            cameraTransform.rotation = Quaternion.LookRotation(Vector3.up);
            tool.TryUse(player.characterCamera, rig.transform);
            tool.TryUse(player.characterCamera, rig.transform);
            tool.Tick(duration + .01f);
            Check(tool.IsBusy, "Queued action was lost");
            tool.Tick(duration + .01f);
            Check(!tool.IsBusy && tool.Kilograms == 0, "Queued action did not finish cleanly");
            grains.SetFill(0);
            for (int i = 0; i < 90; i++) simulate.Invoke(grains, new object[] { 1f / 60f });
            grains.SendMessage("LateUpdate");
            Check(grains.KernelCount == 0 && grains.PackedKernelCount == 0, "Empty shovel still displays grain");
            return $"PASS: contact pickup at phase {firstLoad:F2}, {collected:F2}kg, extraction lift {highestLift-lowestTip:F3}m, max grip travel {maximumTravel:F3}m; conserved scoop mass; tapered A={aCount}/B={bCount} visible kernels, height {aHeight:F3}/{bHeight:F3}m; containment/sleep/tilt; dry swing, cancellation, obstruction, full capacity, retained overflow, already-full bag, queue, empty load. " + string.Join("; ", trace);
        }
        finally
        {
            Object.DestroyImmediate(root);
            foreach (var listener in listeners) if (listener != null) listener.enabled = true;
            Time.timeScale = timeScale;
            Cursor.lockState = cursor;
            Cursor.visible = cursorVisible;
        }
    }

    static void PrepareVisuals(GrainPile pile, PlayerController player, ShovelTool tool)
    {
        var source = Object.FindFirstObjectByType<CornSurfaceKernels>();
        var kernels = pile.gameObject.AddComponent<CornSurfaceKernels>();
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), kernels);
        var data = new SerializedObject(kernels);
        data.FindProperty("pile").objectReferenceValue = pile;
        data.ApplyModifiedPropertiesWithoutUndo();
        pile.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
        int layer = LayerMask.NameToLayer("HeldTools"), mask = 1 << layer;
        foreach (var child in tool.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        Camera world = player.characterCamera;
        world.cullingMask &= ~mask;
        var go = new GameObject("Test tool camera");
        go.transform.SetParent(world.transform, false);
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(world); camera.enabled = false; camera.cullingMask = mask;
        camera.nearClipPlane = .01f;
        go.AddComponent<HDAdditionalCameraData>();
        var driver = world.gameObject.AddComponent<HeldToolCamera>();
        data = new SerializedObject(driver);
        data.FindProperty("worldCamera").objectReferenceValue = world;
        data.FindProperty("toolCamera").objectReferenceValue = camera;
        data.FindProperty("toolLayer").intValue = mask;
        data.ApplyModifiedPropertiesWithoutUndo();
        var volume = go.AddComponent<CustomPassVolume>();
        volume.isGlobal = true; volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        volume.customPasses.Add(new HeldToolPass { name="Test shovel depth", worldCamera=world, toolCamera=camera, toolLayer=mask });
    }

    static void Capture(Camera camera, ShovelGrainPhysics grains, string name)
    {
        float previousTimeScale = Time.timeScale;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        Texture2D image = null;
        try
        {
            Time.timeScale = 0;
            camera.targetTexture = target;
            for (int i = 0; i < 3; i++) { grains.SendMessage("LateUpdate"); camera.Render(); }
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("output/GrainPrototype/ContactScoop");
            System.IO.File.WriteAllBytes("output/GrainPrototype/ContactScoop/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Time.timeScale = previousTimeScale;
            if (image != null) Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
