using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// Run after PreviewCornCarry in a fresh Play Mode session.
public static class ValidateBagAim
{
    public static async Task<string> Main()
    {
        Time.timeScale = 1; Application.runInBackground = true;
        var p = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var t = p.GetComponentInChildren<ShovelTool>();
        var bag = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        var collider = bag.GetComponent<Collider>();
        var find = typeof(ShovelTool).GetMethod("FindHit", BindingFlags.NonPublic | BindingFlags.Instance);
        p.transform.position = bag.transform.position + new Vector3(0, .1f, -2.2f);
        Physics.SyncTransforms();
        Bounds bounds = collider.bounds;
        Vector3[] aims = { bounds.center + Vector3.right * (bounds.extents.x + .2f),
            bounds.center - Vector3.right * (bounds.extents.x + .2f),
            new Vector3(bounds.center.x, bounds.max.y + .2f, bounds.center.z) };
        int checks = 0;
        foreach (Vector3 point in aims)
        {
            p.characterCamera.transform.LookAt(point);
            Ray ray = p.characterCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (collider.Raycast(ray, out _, 4f)) throw new Exception("Near-miss fixture accidentally hits the physical bag");
            var args = new object[] { p.characterCamera, p.transform, default(RaycastHit) };
            if (!(bool)find.Invoke(t, args) || ((RaycastHit)args[2]).collider.GetComponentInParent<GrainContainer>() != bag)
                throw new Exception("Near-edge/opening target missed");
            checks++;
        }
        p.characterCamera.transform.LookAt(bounds.center + Vector3.right * (bounds.extents.x + 1f));
        var far = new object[] { p.characterCamera, p.transform, default(RaycastHit) };
        bool found = (bool)find.Invoke(t, far);
        if (found && ((RaycastHit)far[2]).collider.GetComponentInParent<GrainContainer>() == bag) throw new Exception("Aim padding is unbounded");
        checks++;
        p.characterCamera.transform.LookAt(aims[0]);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = p.characterCamera.transform.position + p.characterCamera.transform.forward * .8f;
            wall.transform.rotation = p.characterCamera.transform.rotation;
            wall.transform.localScale = new Vector3(2, 2, .15f);
            Physics.SyncTransforms();
            var blocked = new object[] { p.characterCamera, p.transform, default(RaycastHit) };
            found = (bool)find.Invoke(t, blocked);
            if (found && ((RaycastHit)blocked[2]).collider.GetComponentInParent<GrainContainer>() == bag) throw new Exception("Aim assist selected a bag through a wall");
            checks++;
        }
        finally { UnityEngine.Object.Destroy(wall); }
        await Task.Delay(50);
        Physics.SyncTransforms();
        float load = t.Kilograms, before = bag.Kilograms;
        if (load < 4.9f) throw new Exception("Expected loaded shovel");
        t.TryUse(p.characterCamera, p.transform);
        for (int i = 0; i < 90; i++) { t.Tick(1f / 60); p.SendMessage("LateUpdate"); await Task.Delay(17); }
        if (t.Kilograms > .001f || Mathf.Abs(bag.Kilograms - before - load) > .001f) throw new Exception("Near-edge click failed to complete the pour");
        checks++;
        return "PASS: " + checks + " bag aim checks: left/right/above near misses, bounded padding, wall obstruction, complete 5kg edge-aim pour.";
    }
}
