using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LargeGrainPileAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        var report = new StringBuilder("Local deformation CPU microbenchmark; editor, not gameplay frame times.\n");
        foreach (float size in new[] { 6f, 24f })
        {
            var root = new GameObject("Temporary clipped pile audit");
            root.SetActive(false);
            root.transform.position = Vector3.up * 10000;
            try
            {
                var pile = root.AddComponent<GrainPile>();
                void Set(string name, object value) => typeof(GrainPile).GetField(name, Private).SetValue(pile, value);
                Set("fitToWalls", false); Set("size", size); Set("initialHeight", 4f);
                Set("excludedFootprint", new Rect(size * .15f, -size * .3f, size * .15f, size * .6f));
                root.SetActive(true);
                var field = (GrainHeightField)typeof(GrainPile).GetField("field", Private).GetValue(pile);
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), pile, typeof(GrainPile).GetMethod("RefreshSurface", Private));
                var collider = (Action)Delegate.CreateDelegate(typeof(Action), pile, typeof(GrainPile).GetMethod("RefreshCollider", Private));
                double renderMs = 0, collisionMs = 0;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                for (int j = 0; j < 30; j++)
                {
                    field.Heights[field.Heights.Length / 2] -= .002f;
                    long start = Stopwatch.GetTimestamp(); refresh();
                    renderMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    start = Stopwatch.GetTimestamp(); collider();
                    collisionMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                }
                report.AppendLine($"size={size}, grid={field.Resolution}, surface={renderMs/30:F3} ms, collision={collisionMs/30:F3} ms, allocations={(GC.GetAllocatedBytesForCurrentThread()-bytes)/30} B/update");
            }
            finally { Object.DestroyImmediate(root); }
        }
        Directory.CreateDirectory("output/GrainPerformance");
        File.WriteAllText("output/GrainPerformance/large-pile.txt", report.ToString());
        UnityEngine.Debug.Log(report.ToString());
    }
}
