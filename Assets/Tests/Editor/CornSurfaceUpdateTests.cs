using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class CornSurfaceUpdateTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Material material;
    private Mesh kernel;
    private GrainPile pile;
    private CornSurfaceKernels surface;
    private GrainHeightField field;
    private Action update;
    private Action nextFrame;

    private T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

    [SetUp] public void SetUp()
    {
        root = new GameObject("Kernel scheduling regression"); root.SetActive(false);
        pile = root.AddComponent<GrainPile>();
        Set(pile, "fitToWalls", false); Set(pile, "size", 8f); Set(pile, "resolution", 33);
        surface = root.AddComponent<CornSurfaceKernels>();
        kernel = new Mesh(); material = new Material(Shader.Find("HDRP/Lit"));
        Set(surface, "pile", pile); Set(surface, "kernelMesh", kernel); Set(surface, "kernelMaterial", material);
        root.SetActive(true);
        field = Get<GrainHeightField>(pile, "visualField");
        update = (Action)Delegate.CreateDelegate(typeof(Action), surface, typeof(CornSurfaceKernels).GetMethod("UpdateChangedCells", Private));
        nextFrame = (Action)Delegate.CreateDelegate(typeof(Action), surface, typeof(CornSurfaceKernels).GetMethod("LateUpdate", Private));
        Set(surface, "rebuildRequested", false);
    }

    [TearDown] public void TearDown()
    {
        Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(kernel);
    }

    private void Lower(float factor)
    {
        for (int i = 0; i < field.Heights.Length; i++) field.Heights[i] *= factor;
    }

    [Test] public void TextureOnlyPileDoesNotGenerateOrUpdateKernels()
    {
        Set(surface, "textureOnly", true);
        surface.Rebuild();
        nextFrame();
        Assert.AreEqual(0, surface.KernelCount);
        Assert.AreEqual(0, surface.SideKernelCount);
        Assert.AreEqual(0, surface.LastUpdatedKernels);
        Assert.AreEqual(0, surface.LastDrawBatches);
        Assert.IsNull(Get<Vector2[]>(surface, "samples"));
    }

    private void Drain()
    {
        int frames = 0;
        while (surface.HasPendingSurfaceUpdates && frames++ < 2000)
        {
            nextFrame();
            Assert.That(surface.LastUpdatedKernels, Is.LessThanOrEqualTo(4096));
        }
        Assert.IsFalse(surface.HasPendingSurfaceUpdates, "Surface edits must eventually finish.");
    }

    [Test] public void BroadExcavationHasBoundedWorkAndEventuallyMatchesEveryKernelHeight()
    {
        Assert.That(surface.KernelCount, Is.GreaterThan(4096));
        Lower(.7f); update();
        Assert.That(surface.LastUpdatedKernels, Is.InRange(1, 4096));
        Assert.IsTrue(surface.HasPendingSurfaceUpdates);
        // More edits while the previous update is partway through a cell must not be lost.
        Lower(.8f); update(); Drain();
        var samples = Get<Vector2[]>(surface, "samples");
        var batches = Get<Matrix4x4[][]>(surface, "batches");
        var batchIds = Get<int[]>(surface, "sampleBatches");
        var slots = Get<int[]>(surface, "sampleSlots");
        int expectedVisible = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (batchIds[i] < 0) continue;
            var p = samples[i];
            if (field.Sample(p.x, p.y) >= .001f) expectedVisible++;
            Assert.That(batches[batchIds[i]][slots[i]].m13, Is.EqualTo(field.Sample(p.x, p.y) + .003f).Within(.00001f));
        }
        Assert.That(surface.KernelCount, Is.EqualTo(expectedVisible), "Thin grains below the visibility threshold are excluded.");
        update(); Assert.That(surface.LastUpdatedKernels, Is.Zero);
    }

    [Test] public void AnimatedSurfaceUploadsEveryHeightEvenWhileKernelWorkIsQueued()
    {
        var gpuMaterial = new Material(Shader.Find("Freehangar/HDRP/Grain Kernels"));
        try
        {
            Set(surface, "kernelMaterial", gpuMaterial);
            surface.Rebuild();
            Assert.IsTrue(Get<bool>(surface, "gpuSurface"));
            var matrices = Get<Matrix4x4[][]>(surface, "batches");
            var original = (Matrix4x4[])matrices[0].Clone();
            var upload = (Action)Delegate.CreateDelegate(typeof(Action), surface,
                typeof(CornSurfaceKernels).GetMethod("UploadSurface", Private));
            for (int frame = 0; frame < 10; frame++)
            {
                Lower(.97f);
                update();
                Set(surface, "uploadedRevision", -1);
                upload();
                var heights = Get<Texture2D>(surface, "surfaceHeights").GetPixelData<Vector2>(0);
                for (int i = 0; i < field.Heights.Length; i++)
                {
                    Assert.AreEqual(field.Heights[i], heights[i].x);
                    Assert.AreEqual(0, heights[i].y, "Authored piles retain their original base.");
                }
                CollectionAssert.AreEqual(original, matrices[0], "GPU grains must not snap to queued CPU transforms.");
            }
            Array.Clear(field.Heights, 0, field.Heights.Length);
            update(); Drain();
            Assert.AreEqual(0, surface.KernelCount);
        }
        finally { Object.DestroyImmediate(gpuMaterial); }
    }

    [Test] public void ClearingAndRefillingQueuedCellsRestoresVisibilityAndCullingBounds()
    {
        var initial = (float[])field.Heights.Clone();
        Array.Clear(field.Heights, 0, field.Heights.Length); update(); Drain();
        Assert.That(Get<bool[]>(surface, "liveBatches"), Has.None.True);
        for (int i = 0; i < initial.Length; i++) field.Heights[i] = initial[i] * 1.2f;
        update(); Drain();
        var matrices = Get<Matrix4x4[][]>(surface, "batches");
        var counts = Get<int[]>(surface, "counts");
        var bounds = Get<Bounds[]>(surface, "surfaceBounds");
        var live = Get<bool[]>(surface, "liveBatches");
        var visibleCounts = Get<int[]>(surface, "activeBatchCounts");
        for (int b = 0; b < counts.Length; b++)
        {
            if (counts[b] == 0) continue;
            Assert.AreEqual(visibleCounts[b] > 0, live[b], "Reserved bare-ground samples must not make an empty batch visible.");
            for (int s = 0; s < counts[b]; s++) Assert.IsTrue(bounds[b].Contains(matrices[b][s].GetColumn(3)));
        }
    }

    [Test] public void SteepExcavationAddsCoverageAndLaterEditsRemoveStaleGrains()
    {
        int originalCount = surface.KernelCount;
        int center = field.Resolution / 2 * field.Resolution + field.Resolution / 2;
        field.Heights[center] = .001f;
        var expectedShape = (float[])field.Heights.Clone();
        update(); Drain();
        var patches = Get<System.Collections.IDictionary>(surface, "coveragePatches");
        Assert.That(patches.Count, Is.GreaterThan(0), "Steep cuts need additional surface samples.");
        Assert.That(surface.KernelCount, Is.GreaterThan(originalCount));
        CollectionAssert.AreEqual(expectedShape, field.Heights, "Coverage must not change the excavation shape.");
        foreach (System.Collections.DictionaryEntry entry in patches)
        {
            Assert.That((int)entry.Value.GetType().GetField("count").GetValue(entry.Value), Is.LessThanOrEqualTo(1024),
                "A steep cell must not grow an unbounded decorative kernel population.");
            var matrices = (Matrix4x4[][])entry.Value.GetType().GetField("batches").GetValue(entry.Value);
            foreach (var batch in matrices)
            foreach (var matrix in batch)
            {
                var local = root.transform.InverseTransformPoint(matrix.GetColumn(3));
                Assert.That(local.y, Is.EqualTo(field.Sample(local.x, local.z) + .003f).Within(.0001f));
            }
        }
        // Edit again while coverage work is queued, then remove the entire pile.
        field.Heights[center] = 2f; update();
        Array.Clear(field.Heights, 0, field.Heights.Length); update(); Drain();
        Assert.That(surface.KernelCount, Is.Zero);
        Assert.That(patches.Count, Is.Zero);
        foreach (var batch in Get<bool[][]>(surface, "visibleBatchSamples"))
            foreach (bool visible in batch) Assert.IsFalse(visible, "Cleared kernels must not be submitted to rendering.");
        foreach (bool live in Get<bool[]>(surface, "liveBatches")) Assert.IsFalse(live);
    }
}
