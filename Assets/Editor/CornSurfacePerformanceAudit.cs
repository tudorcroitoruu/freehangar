using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class CornSurfacePerformanceAudit
{
    [MenuItem("Tools/Grain/Measure hangar kernel updates")]
    public static void Run() => Run("latest");

    private static void Run(string label)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var report = new StringBuilder("Saved Gameplay pile CPU audit; Editor timings, not standalone FPS.\n");
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay.unity");
        try
        {
            var surfaces = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CornSurfaceKernels>(true))
                .Where(s => s.isActiveAndEnabled).ToArray();
            foreach (var s in surfaces) report.AppendLine(s.name + ": " + s.KernelCount + " kernels");
            var surface = surfaces.OrderByDescending(s => s.KernelCount).First();
            var pile = surface.GetComponent<GrainPile>();
            var field = (GrainHeightField)typeof(GrainPile).GetField("field", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pile);
            var update = (Action)Delegate.CreateDelegate(typeof(Action), surface,
                typeof(CornSurfaceKernels).GetMethod("UpdateChangedCells", BindingFlags.Instance | BindingFlags.NonPublic));
            var times = new double[32]; int maxUpdated = 0;
            long allocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < times.Length; i++)
            {
                // A repeated shallow excavation followed by the same settling used in gameplay.
                field.Scoop((i % 4 - 1.5f) * field.Size * .06f, 0, field.Size * .12f, .06f, .3f);
                field.Settle(32, .45f, pile.WorldScale);
                long start = Stopwatch.GetTimestamp(); update();
                times[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                maxUpdated = Math.Max(maxUpdated, surface.LastUpdatedKernels);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
            Array.Sort(times);
            report.AppendLine($"Pile={surface.name}; mean={times.Average():F3} ms; p95={times[30]:F3} ms; max={times[31]:F3} ms; max kernels updated/frame={maxUpdated}; allocations={bytes} B (including field simulation).");
            int drainFrames = 0;
            while (surface.HasPendingSurfaceUpdates && drainFrames < 2000) { update(); drainFrames++; }
            report.AppendLine($"Remaining edits drained in {drainFrames} additional update frames; pending={surface.HasPendingSurfaceUpdates}; density={surface.KernelCount} kernels.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Directory.CreateDirectory("output/GrainPerformance");
        File.WriteAllText("output/GrainPerformance/corn-surface-" + label + ".txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
