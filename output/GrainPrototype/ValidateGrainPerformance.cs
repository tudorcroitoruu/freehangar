using System;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

public static class ValidateGrainPerformance
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play Mode");
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var corn = pile.GetComponent<CornSurfaceKernels>();
        var field = (GrainHeightField)typeof(GrainPile).GetField("field",Flags).GetValue(pile);
        var build = typeof(CornSurfaceKernels).GetMethod("Build",Flags);
        build.Invoke(corn,null);
        var timer = Stopwatch.StartNew();
        for(int i=0;i<10;i++) build.Invoke(corn,null);
        double idle=timer.Elapsed.TotalMilliseconds/10;
        int best=0;
        for(int i=0;i<field.Heights.Length;i++) if(field.Heights[i]>field.Heights[best]) best=i;
        Vector3 local=new Vector3((best%field.Resolution)*field.Spacing-field.Size*.5f, field.Heights[best], (best/field.Resolution)*field.Spacing-field.Size*.5f);
        double mass=pile.Kilograms;
        float removed=pile.Scoop(pile.transform.TransformPoint(local),.4f,.18f,6.5f);
        timer.Restart(); build.Invoke(corn,null); double changed=timer.Elapsed.TotalMilliseconds;
        int updated=corn.LastUpdatedKernels;
        if(removed<6.49f || Math.Abs(mass-pile.Kilograms-removed)>.03) throw new Exception("Scoop mass regression");
        if(updated<=0 || updated>=corn.KernelCount/2) throw new Exception("Update is not local: "+updated);
        var batches=(Matrix4x4[][])typeof(CornSurfaceKernels).GetField("batches",Flags).GetValue(corn);
        var counts=(int[])typeof(CornSurfaceKernels).GetField("counts",Flags).GetValue(corn);
        for(int b=0;b<batches.Length;b++) for(int i=0;i<counts[b];i++)
        {
            var matrix=batches[b][i]; if(matrix.GetColumn(0).magnitude<.0001f)continue;
            Vector3 world=matrix.GetColumn(3);Vector3 p=pile.transform.InverseTransformPoint(world-Vector3.up*.003f);
            if(Mathf.Abs(p.y-pile.SampleLocalHeight(p.x,p.z))>.0001f)throw new Exception("Stale kernel after excavation");
        }
        var go=new GameObject("Temporary LOD validation camera");
        try
        {
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.farClipPlane=100;
            var bounds=pile.GetComponent<MeshRenderer>().bounds;
            camera.transform.position=bounds.center+Vector3.up*6+Vector3.back*14;
            camera.transform.LookAt(bounds.center);
            typeof(CornSurfaceKernels).GetMethod("Draw",Flags).Invoke(corn,new object[]{default(ScriptableRenderContext),camera});
            if(corn.LastDrawTriangles<=0 || corn.LastDrawTriangles>corn.KernelCount*8)throw new Exception("Distant LOD regression");
            var physics=UnityEngine.Object.FindFirstObjectByType<ShovelGrainPhysics>();
            float slope=0;
            for(int z=2;z<field.Resolution-2;z++) for(int x=2;x<field.Resolution-2;x++)
            {
                int i=z*field.Resolution+x;
                Vector3 candidate=new Vector3(x*field.Spacing-field.Size*.5f,field.Heights[i],z*field.Spacing-field.Size*.5f);
                float rise=field.Heights[i+1]-candidate.y;
                if(candidate.y>.2f && rise>slope && pile.IsInsideBoundary(pile.transform.TransformPoint(candidate),.6f)){local=candidate;slope=rise;}
            }
            physics.BeginScoop(); physics.EmitScoop(pile,pile.transform.TransformPoint(local),.4f,1);
            if(physics.ScoopEmittedCount!=0)throw new Exception("Debris emitted before withdrawal");
            physics.ReleaseScoopDebris(1);
            var life=(float[])typeof(ShovelGrainPhysics).GetField("streamLife",Flags).GetValue(physics);
            var scoop=(bool[])typeof(ShovelGrainPhysics).GetField("scoopStream",Flags).GetValue(physics);
            var positions=(Vector3[])typeof(ShovelGrainPhysics).GetField("streamPosition",Flags).GetValue(physics);
            var landing=(Vector3[])typeof(ShovelGrainPhysics).GetField("scoopLanding",Flags).GetValue(physics);
            var simulate=typeof(ShovelGrainPhysics).GetMethod("Simulate",Flags);
            int emitted=physics.ScoopEmittedCount;
            if(emitted==0 || emitted>48)throw new Exception("Scoop emission budget");
            for(int step=0;step<10;step++)
            {
                var previous=(Vector3[])positions.Clone();simulate.Invoke(physics,new object[]{.05f});
                for(int i=0;i<life.Length;i++)if(scoop[i] && positions[i].y>previous[i].y+.0001f)throw new Exception("Debris jumped upward");
            }
            for(int i=0;i<life.Length;i++) if(scoop[i] && (life[i]>0 || Mathf.Abs(positions[i].y-pile.SurfaceHeight(landing[i])-.01f)>.001f))throw new Exception("Debris did not settle into pile");
            return "PASS: mass, local updates, all kernel heights, distant LOD and downward debris settling. Kernels="+corn.KernelCount+", updated="+updated+", idleMs="+idle.ToString("F3")+", changedMs="+changed.ToString("F3")+", distantTriangles="+corn.LastDrawTriangles+", scoopParticles="+emitted;
        }
        finally { UnityEngine.Object.Destroy(go); }
    }
}
