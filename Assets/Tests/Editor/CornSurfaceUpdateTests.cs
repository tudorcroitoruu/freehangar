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
        field = Get<GrainHeightField>(pile, "field");
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
        int initialCount = surface.KernelCount;
        Lower(.7f); update();
        Assert.That(surface.LastUpdatedKernels, Is.InRange(1, 4096));
        Assert.IsTrue(surface.HasPendingSurfaceUpdates);
        // More edits while the previous update is partway through a cell must not be lost.
        Lower(.8f); update(); Drain();
        var samples = Get<Vector2[]>(surface, "samples");
        var batches = Get<Matrix4x4[][]>(surface, "batches");
        var batchIds = Get<int[]>(surface, "sampleBatches");
        var slots = Get<int[]>(surface, "sampleSlots");
        for (int i = 0; i < samples.Length; i++)
        {
            if (batchIds[i] < 0) continue;
            var p = samples[i];
            Assert.That(batches[batchIds[i]][slots[i]].m13, Is.EqualTo(field.Sample(p.x, p.y) + .003f).Within(.00001f));
        }
        Assert.That(surface.KernelCount, Is.EqualTo(initialCount), "Scheduling preserves authored density.");
        update(); Assert.That(surface.LastUpdatedKernels, Is.Zero);
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
        for (int b = 0; b < counts.Length; b++)
        {
            if (counts[b] == 0) continue;
            Assert.IsTrue(live[b]);
            for (int s = 0; s < counts[b]; s++) Assert.IsTrue(bounds[b].Contains(matrices[b][s].GetColumn(3)));
        }
    }
}
