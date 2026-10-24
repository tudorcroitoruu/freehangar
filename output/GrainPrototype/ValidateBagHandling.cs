using System;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateBagHandling
{
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    public static async Task<string> Main()
    {
        Time.timeScale = 1; Application.runInBackground = true;
        var p = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        p.enabled = false; p.characterController.enabled = false;
        var tool = p.GetComponentInChildren<ShovelTool>();
        var handler = p.GetComponent<BagInteraction>();
        var prompt = UnityEngine.Object.FindFirstObjectByType<BagInteractionPrompt>();
        var bag = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        var sack = bag.GetComponent<SackController>();
        Vector3 originalScale = bag.transform.localScale;
        Transform originalParent = bag.transform.parent;
        float load = tool.Kilograms;
        p.transform.position = bag.transform.position + new Vector3(0, .1f, -2.2f);
        p.characterCamera.transform.LookAt(bag.WorldBounds.center);
        Physics.SyncTransforms(); await Task.Delay(150);
        Check(!bag.Finish() && !handler.TryInteract() && !prompt.IsVisible, "Partial bag must not finish or show a prompt");
        bag.Add(bag.CapacityKg);
        p.characterCamera.transform.LookAt(bag.WorldBounds.center);
        Physics.SyncTransforms(); await Task.Delay(150);
        Check(prompt.IsVisible && prompt.CurrentText == "Finish", "Full bag must show Finish");
        ScreenCapture.CaptureScreenshot("output/GrainPrototype/BagFinish.png");
        await Task.Delay(100);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = p.characterCamera.transform.position + p.characterCamera.transform.forward * .8f;
        wall.transform.rotation = p.characterCamera.transform.rotation;
        wall.transform.localScale = new Vector3(2, 2, .15f);
        Physics.SyncTransforms();
        Check(!handler.TryInteract(), "Must not finish through a wall");
        UnityEngine.Object.Destroy(wall); await Task.Delay(50); Physics.SyncTransforms();
        Check(handler.TryInteract() && bag.IsClosing && !bag.IsFinished, "Finish must start animated closure");
        Check(!handler.TryInteract(), "Repeated E during closing must not take bag");
        await Task.Delay(1000);
        Check(bag.IsFinished && !bag.IsClosing && sack.Close == 1f && prompt.CurrentText == "Take", "Closure must finish and show Take");
        Check(bag.Add(5) == 0 && bag.Kilograms == bag.CapacityKg, "Closed bag must preserve capacity");
        ScreenCapture.CaptureScreenshot("output/GrainPrototype/BagTake.png"); await Task.Delay(100);
        Check(handler.TryInteract() && handler.CarriedBag == bag, "Take must pick up same bag");
        await Task.Delay(150);
        Check(!tool.gameObject.activeInHierarchy && bag.IsCarried && bag.GetComponent<Rigidbody>().isKinematic, "Held bag must replace shovel and disable physics");
        foreach (var collider in bag.GetComponentsInChildren<Collider>()) Check(!collider.enabled, "Held colliders must be disabled");
        Check(prompt.CurrentText == "Drop" && bag.transform.IsChildOf(p.characterCamera.transform), "Held bag must show Drop");
        Check(Mathf.Abs(tool.Kilograms - load) < .001f, "Taking must preserve shovel load");
        ScreenCapture.CaptureScreenshot("output/GrainPrototype/BagHeld.png"); await Task.Delay(100);
        // A large wall blocks all three nearby drop paths.
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 forward = Vector3.ProjectOnPlane(p.characterCamera.transform.forward, Vector3.up).normalized;
        wall.transform.position = p.transform.position + forward * .85f + Vector3.up;
        wall.transform.rotation = Quaternion.LookRotation(forward);
        wall.transform.localScale = new Vector3(5, 3, .2f);
        Physics.SyncTransforms();
        Check(!handler.TryInteract() && handler.IsCarrying, "Blocked drop must leave bag in hands");
        UnityEngine.Object.Destroy(wall); await Task.Delay(50); Physics.SyncTransforms();
        Check(handler.TryInteract() && !handler.IsCarrying, "Drop must find nearby open floor");
        Check(bag.transform.parent == originalParent && bag.transform.localScale == originalScale && bag.GetComponent<Collider>().enabled, "Drop must restore bag parent, scale and collider");
        Check(tool.gameObject.activeInHierarchy && Mathf.Abs(tool.Kilograms - load) < .001f, "Drop must restore loaded shovel");
        Check(!bag.GetComponent<Rigidbody>().isKinematic && bag.IsFinished && bag.IsFull, "Dropped bag must remain closed/full with gravity");
        await Task.Delay(1300);
        Check(bag.WorldBounds.min.y > -.05f && bag.GetComponent<Rigidbody>().linearVelocity.magnitude < .15f, "Dropped bag must settle on floor");
        p.characterCamera.transform.LookAt(bag.WorldBounds.center); Physics.SyncTransforms();
        Check(handler.TryInteract() && handler.IsCarrying, "Dropped bag must be reusable");
        Check(handler.TryInteract() && !handler.IsCarrying && bag.Kilograms == bag.CapacityKg, "Repeated pickup/drop must preserve contents");
        return "PASS: " + checks + " bag handling checks; shovel load preserved: " + load + " kg.";
    }
}
