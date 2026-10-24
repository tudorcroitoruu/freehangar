using System;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateLocalRefill
{
    public static async Task<string> Main()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first.");
        Application.runInBackground = true;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        player.enabled = false;
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        const int side = 33;
        float spacing = pile.Size / (side - 1);
        var original = new float[side * side];
        for (int z = 0; z < side; z++)
        for (int x = 0; x < side; x++)
            original[z * side + x] = pile.SampleLocalHeight(x * spacing - pile.Size * .5f, z * spacing - pile.Size * .5f);
        Vector3 local = new Vector3(0, pile.SampleLocalHeight(0, -1.5f), -1.5f);
        Vector3 point = pile.transform.TransformPoint(local);
        double beforeMass = pile.Kilograms;
        float removed = pile.Scoop(point, .4f, .18f, 5);
        float excavated = pile.SampleLocalHeight(0, -1.5f);
        await Task.Delay(2400);
        float refilled = pile.SampleLocalHeight(0, -1.5f);
        if (!(refilled > excavated && refilled < local.y)) throw new Exception("Local hole did not partially refill");
        if (Math.Abs(beforeMass - pile.Kilograms - removed) > .001) throw new Exception("Mass changed during local refill");
        int untouched = 0;
        for (int z = 0; z < side; z++)
        for (int x = 0; x < side; x++)
        {
            float px = x * spacing - pile.Size * .5f, pz = z * spacing - pile.Size * .5f;
            if (new Vector2(px, pz + 1.5f).magnitude <= .85f) continue;
            if (pile.SampleLocalHeight(px, pz) != original[z * side + x]) throw new Exception("Distant pile changed");
            untouched++;
        }
        Vector3 surface = pile.transform.TransformPoint(new Vector3(0, refilled, -1.5f));
        if (!pile.GetComponent<MeshCollider>().Raycast(new Ray(surface + Vector3.up, Vector3.down), out RaycastHit hit, 2f) ||
            Mathf.Abs(hit.point.y - surface.y) > .001f) throw new Exception("Settled collider does not match the surface");
        return "PASS: localized Play Mode refill. Before=" + local.y.ToString("F4") + " m; excavated=" + excavated.ToString("F4") +
            " m; settled=" + refilled.ToString("F4") + " m. Removed=" + removed.ToString("F3") + " kg. " + untouched +
            " distant grid vertices unchanged; mass conserved; collider matches within 1 mm.";
    }
}
