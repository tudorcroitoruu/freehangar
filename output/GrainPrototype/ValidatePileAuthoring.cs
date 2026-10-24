using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ValidatePileAuthoring
{
    static int checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Run in Edit Mode.");
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var kernels = pile.GetComponent<CornSurfaceKernels>();
        var data = new SerializedObject(pile);
        int originalShape = data.FindProperty("shape").enumValueIndex;
        Vector3 originalScale = pile.transform.localScale;
        Quaternion originalRotation = pile.transform.rotation;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            data.FindProperty("shape").enumValueIndex = 0; data.ApplyModifiedProperties();
            pile.transform.localScale = Vector3.one; pile.RebuildPreview(); kernels.Rebuild();
            double originalMass = pile.Kilograms; int originalCount = kernels.KernelCount;
            foreach (Vector3 scale in new[] { new Vector3(1.8f, 1.4f, .7f), new Vector3(.7f, .6f, 1.6f) })
            {
                pile.transform.localScale = scale;
                pile.transform.rotation = Quaternion.Euler(0, 31, 0);
                pile.RebuildPreview(); kernels.Rebuild(); Physics.SyncTransforms();
                double expected = originalMass * scale.x * scale.y * scale.z;
                Check(Math.Abs(pile.Kilograms - expected) / expected < .01, "Scaled volume follows XYZ scale");
                Check(kernels.KernelCount > originalCount, "Larger horizontal area gains kernels");
                var batches = (Matrix4x4[][])typeof(CornSurfaceKernels).GetField("batches", flags).GetValue(kernels);
                var counts = (int[])typeof(CornSurfaceKernels).GetField("counts", flags).GetValue(kernels);
                for (int b = 0; b < counts.Length; b++) for (int i = 0; i < counts[b]; i++)
                {
                    Vector3 lengths = new Vector3(batches[b][i].GetColumn(0).magnitude, batches[b][i].GetColumn(1).magnitude, batches[b][i].GetColumn(2).magnitude);
                    if (lengths.x < .0255f * .899f || lengths.x > .0255f * 1.121f ||
                        Mathf.Abs(lengths.y / lengths.x - .014f / .0255f) > .001f ||
                        Mathf.Abs(lengths.z / lengths.x - .029f / .0255f) > .001f) throw new Exception("Kernel was scaled/stretched by pile transform");
                }
                checks++;
                Vector3 point = pile.transform.TransformPoint(new Vector3(0, pile.SampleLocalHeight(0, -.8f), -.8f));
                var collider = pile.GetComponent<MeshCollider>();
                Check(collider.Raycast(new Ray(point + Vector3.up * 2, Vector3.down), out RaycastHit hit, 3) && Mathf.Abs(hit.point.y - point.y) < .002f, "Scaled collider follows surface");
                double before = pile.Kilograms;
                float taken = pile.Scoop(point, .4f, .18f, 6.5f);
                Check(Mathf.Abs(taken - 6.5f) < .01f && Math.Abs(before - pile.Kilograms - taken) < .03, "Scaled scoop still removes exactly 6.5 kg");
                var field = (GrainHeightField)typeof(GrainPile).GetField("field", flags).GetValue(pile);
                for (int step = 0; step < 50; step++) field.Settle(32, .45f, scale);
                Check(Math.Abs(before - pile.Kilograms - taken) < .03, "Scaled local settling conserves mass");
            }
            pile.transform.localScale = Vector3.one;
            foreach (int shape in new[] { 1, 2 })
            {
                data.Update(); data.FindProperty("shape").enumValueIndex = shape; data.ApplyModifiedProperties();
                pile.RebuildPreview(); kernels.Rebuild();
                Check(pile.SampleLocalHeight(-1.2f, .54f) > .5f && pile.SampleLocalHeight(.81f, 1.08f) > .5f, "Authored lobes produce multiple mounds");
                Check(Mathf.Abs(pile.SampleLocalHeight(-1, 0) - pile.SampleLocalHeight(1, 0)) > .03f, "Shape is asymmetric");
                if (shape == 2)
                {
                    Check(pile.SampleLocalHeight(0, pile.Size * .5f) > 1f, "Bank reaches back wall");
                    Check(pile.SampleLocalHeight(0, -pile.Size * .5f) < .001f, "Front tapers to ground");
                    float backHeight = pile.SampleLocalHeight(0, pile.Size * .5f);
                    Vector3 back = pile.transform.TransformPoint(new Vector3(0, backHeight, pile.Size * .5f));
                    double before = pile.Kilograms;
                    float collected = pile.Scoop(back, .4f, .18f, 6.5f);
                    Check(collected > 6.49f && pile.SampleLocalHeight(0, pile.Size * .5f) < backHeight && Math.Abs(before - pile.Kilograms - collected) < .03, "Wall-edge grain remains scoopable and conserves mass");
                }
            }
            pile.transform.localScale = new Vector3(4, 1, 4); pile.RebuildPreview(); kernels.Rebuild();
            Check(kernels.KernelCount <= 80000, "Large pile respects kernel budget");
            return "PASS: " + checks + " pile scale/shape checks, including every rendered kernel's world dimensions.";
        }
        finally
        {
            pile.transform.localScale = originalScale; pile.transform.rotation = originalRotation;
            data.Update(); data.FindProperty("shape").enumValueIndex = originalShape; data.ApplyModifiedProperties();
            pile.RebuildPreview(); kernels.Rebuild(); Physics.SyncTransforms();
        }
    }
}
