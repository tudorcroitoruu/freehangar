using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ValidateCoverageScatter
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Run in Edit Mode");
        var pile=UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var corn=pile.GetComponent<CornSurfaceKernels>(); var scatter=pile.GetComponent<CornGroundScatter>();
        var data=new SerializedObject(corn); int budget=data.FindProperty("maximumSurfaceKernels").intValue;
        Vector3 originalScale=pile.transform.localScale;
        try
        {
            pile.RebuildPreview();corn.Rebuild();scatter.Rebuild();
            int originalCount=corn.KernelCount; float originalSpacing=corn.EffectiveSpacing;
            double mass=pile.Kilograms;
            Check(scatter.KernelCount>50 && scatter.KernelCount<=1023,"Decorative ground scatter exists and is bounded");
            var scatterMatrices=(Matrix4x4[])typeof(CornGroundScatter).GetField("matrices",Flags).GetValue(scatter);
            for(int i=0;i<scatter.KernelCount;i++)
            {
                Vector3 world=scatterMatrices[i].GetColumn(3),local=pile.transform.InverseTransformPoint(world);
                if(!pile.IsInsideBoundary(world,.039f)||pile.SampleLocalHeight(local.x,local.z)*pile.WorldScale.y>.0031f)
                    throw new Exception("Scatter crosses wall or sits in the packed pile");
            }
            checks++;
            scatter.Rebuild();Check(Math.Abs(pile.Kilograms-mass)<.0001,"Scatter cannot change grain mass");
            data.FindProperty("maximumSurfaceKernels").intValue=10000;data.ApplyModifiedProperties();corn.Rebuild();
            Check(corn.KernelCount==originalCount && Mathf.Abs(corn.EffectiveSpacing-originalSpacing)<.00001f,"Budget cannot thin the surface");
            pile.transform.localScale=originalScale*1.1f;pile.RebuildPreview();corn.Rebuild();
            Check(corn.KernelCount>originalCount && Mathf.Abs(corn.EffectiveSpacing-originalSpacing)<.00001f,"Scale adds corn without changing spacing");
            var batches=(Matrix4x4[][])typeof(CornSurfaceKernels).GetField("batches",Flags).GetValue(corn);
            var counts=(int[])typeof(CornSurfaceKernels).GetField("counts",Flags).GetValue(corn);
            var cells=new Dictionary<Vector3Int,List<Vector3>>();
            for(int b=0;b<counts.Length;b++)for(int i=0;i<counts[b];i++)
            {
                Matrix4x4 matrix=batches[b][i];float width=matrix.GetColumn(0).magnitude;
                if(width<.0255f*.899f||width>.0255f*1.121f)throw new Exception("Kernel world size changed");
                Vector3 world=matrix.GetColumn(3);var key=Vector3Int.FloorToInt(world/.08f);
                if(!cells.TryGetValue(key,out var list)){list=new List<Vector3>();cells.Add(key,list);}list.Add(world);
            }
            checks++;
            Mesh mesh=pile.GetComponent<MeshFilter>().sharedMesh;Vector3[] vertices=mesh.vertices;int[] indices=mesh.triangles;
            float worst=0;int sampled=0;
            int stride=Mathf.Max(1,indices.Length/3/200)*3;
            for(int t=0;t+2<indices.Length;t+=stride)
            {
                Vector3 p=(vertices[indices[t]]+vertices[indices[t+1]]+vertices[indices[t+2]])/3;
                p.y=pile.SampleLocalHeight(p.x,p.z);if(p.y<.08f)continue;
                Vector3 world=pile.transform.TransformPoint(p);if(!pile.IsInsideBoundary(world,.1f))continue;
                var key=Vector3Int.FloorToInt(world/.08f);float nearest=float.PositiveInfinity;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    if(cells.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(Vector3 point in list)nearest=Mathf.Min(nearest,Vector3.Distance(point,world));
                worst=Mathf.Max(worst,nearest);sampled++;
            }
            Check(sampled>30 && worst<.045f,"Surface coverage has no large gaps; worst nearest kernel="+worst);
            return "PASS: "+checks+" coverage/scatter checks; "+originalCount+" -> "+corn.KernelCount+" kernels on 10% scale increase, fixed "+originalSpacing+"m spacing; "+sampled+" surface probes, worst gap "+worst+"m; scatter="+scatter.KernelCount;
        }
        finally
        {
            pile.transform.localScale=originalScale;data.Update();data.FindProperty("maximumSurfaceKernels").intValue=budget;data.ApplyModifiedProperties();
            pile.RebuildPreview();corn.Rebuild();scatter.Rebuild();
        }
    }
}
