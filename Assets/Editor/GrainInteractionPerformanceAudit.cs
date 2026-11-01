using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

public static class GrainInteractionPerformanceAudit
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Grain/Measure interaction CPU work")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var report=new StringBuilder("Editor CPU microbenchmarks (not standalone frame times)\n");
        var root=new GameObject("Grain CPU audit");
        try
        {
            var bag=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab"),root.transform);
            VehicleCargoPreview.PrepareContainer(bag,.75f,0);
            var surface=bag.GetComponent<BagCornSurface>();
            Measure(report,"Bag bake/sample Rebuild",20,()=>surface.Rebuild());
            report.AppendLine("Bag visible kernels="+surface.KernelCount);
            var updateBag=(Action)Delegate.CreateDelegate(typeof(Action),surface,typeof(BagCornSurface).GetMethod("LateUpdate",Private));
            int bakes=surface.SurfaceBakeCount;
            Measure(report,"Bag rigid movement cached update",200,()=>{bag.transform.position+=Vector3.forward*.03f;updateBag();});
            report.AppendLine("Bag mesh bakes during rigid movement="+(surface.SurfaceBakeCount-bakes));
            var player=Object.Instantiate(Resources.Load<GameObject>("Player"),root.transform);
            var grain=player.GetComponentsInChildren<ShovelGrainPhysics>(true).First();
            typeof(ShovelGrainPhysics).GetMethod("Awake",Private).Invoke(grain,null);
            var simulate=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),grain,typeof(ShovelGrainPhysics).GetMethod("Simulate",Private));
            grain.SetFill(1);for(int i=0;i<200;i++)simulate(.01f);
            for(int i=0;i<100;i++){grain.transform.position+=Vector3.forward*.03f;simulate(.01f);}
            Measure(report,"Loaded shovel constant walking Simulate",200,()=>{grain.transform.position+=Vector3.forward*.03f;simulate(.01f);});
            report.AppendLine("Loaded shovel sleeping during uniform walking="+grain.IsLoadSleeping+", kernels="+grain.KernelCount);
            var updateLoad=(Action)Delegate.CreateDelegate(typeof(Action),grain,typeof(ShovelGrainPhysics).GetMethod("UpdateLoad",Private));
            var stopwatch=Stopwatch.StartNew();double solver=0,visual=0,maxSolver=0,maxVisual=0;
            for(int i=0;i<300;i++)
            {
                grain.SetFill((i%100)/100f);grain.transform.rotation=Quaternion.Euler(0,i*.7f,0);
                long start=Stopwatch.GetTimestamp();simulate(.01f);double a=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                start=Stopwatch.GetTimestamp();updateLoad();double b=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                solver+=a;visual+=b;maxSolver=Math.Max(maxSolver,a);maxVisual=Math.Max(maxVisual,b);
            }
            report.AppendLine($"Active scoop/pour: solver mean={solver/300:F3} ms max={maxSolver:F3}; load mesh mean={visual/300:F3} ms max={maxVisual:F3}");
            var field=new GrainHeightField(33,16,3);
            for(int i=0;i<3;i++)field.Scoop(i*.15f,0,1.5f,.2f,.1f);
            int steps=0;var watch=Stopwatch.StartNew();while(steps<1000&&field.Settle(33,.45f))steps++;watch.Stop();
            report.AppendLine($"Three scoops settle steps={steps}, total={watch.Elapsed.TotalMilliseconds:F3} ms, still active={steps==1000}");
        }
        finally{Object.DestroyImmediate(root);}
        Directory.CreateDirectory("output/GrainPerformance");File.WriteAllText("output/GrainPerformance/latest.txt",report.ToString());Debug.Log(report.ToString());
    }
    private static void Measure(StringBuilder report,string label,int iterations,Action work)
    {
        work();var samples=new double[iterations];
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<iterations;i++){var start=Stopwatch.GetTimestamp();work();samples[i]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(samples);
        report.AppendLine($"{label}: mean={samples.Average():F3} ms, p95={samples[(int)(iterations*.95)]:F3} ms, max={samples[iterations-1]:F3} ms, managed={allocated/iterations} B/call");
    }
}
