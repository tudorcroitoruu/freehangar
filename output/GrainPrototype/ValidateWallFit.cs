using System;
using System.Reflection;
using UnityEngine;

public static class ValidateWallFit
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static void CheckMesh(GrainPile pile)
    {
        foreach (var vertex in pile.GetComponent<MeshCollider>().sharedMesh.vertices)
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(vertex))) throw new Exception("Collider vertex crosses wall");
        checks++;
        foreach (var vertex in pile.GetComponent<MeshFilter>().sharedMesh.vertices)
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(vertex))) throw new Exception("Visual vertex crosses wall");
        checks++;
        Mesh mesh = pile.GetComponent<MeshCollider>().sharedMesh;
        Vector3[] v = mesh.vertices; int[] t = mesh.triangles;
        double volume = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i+1]], c = v[t[i+2]];
            float area = Mathf.Abs((b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x)) * .5f;
            volume += area * (a.y+b.y+c.y) / 3.0;
        }
        double mass = volume * Mathf.Abs(pile.transform.localToWorldMatrix.determinant) * 720;
        Check(Math.Abs(mass-pile.Kilograms) < .05, "Gameplay mass must match clipped collider volume");
    }
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode");
        var pile = UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        pile.RebuildPreview();
        var kernels = pile.GetComponent<CornSurfaceKernels>(); kernels.Rebuild();
        Check(pile.DetectedWallCount > 0, "Detect current barn wall"); CheckMesh(pile);
        var batches = (Matrix4x4[][])typeof(CornSurfaceKernels).GetField("batches",Flags).GetValue(kernels);
        var counts = (int[])typeof(CornSurfaceKernels).GetField("counts",Flags).GetValue(kernels);
        for (int b=0;b<counts.Length;b++) for(int i=0;i<counts[b];i++)
            if(!pile.IsInsideBoundary(batches[b][i].GetColumn(3), .0399f)) throw new Exception("Kernel lacks wall margin");
        checks++;
        try
        {
            Vector3 point=pile.transform.position+new Vector3(1.6f,0,0); point.y=pile.SurfaceHeight(point);
            double before=pile.Kilograms; float taken=pile.Scoop(point,.4f,.18f,6.5f);
            Check(taken>6.49f && Math.Abs(before-pile.Kilograms-taken)<.03,"Wall-side scoop conserves mass");
            var field=(GrainHeightField)typeof(GrainPile).GetField("field",Flags).GetValue(pile);
            for(int i=0;i<100;i++) field.Settle(32,.45f,pile.WorldScale);
            Check(Math.Abs(before-pile.Kilograms-taken)<.03,"Clipped settling conserves mass");
            typeof(GrainPile).GetMethod("RefreshSurface",Flags).Invoke(pile,null);
            typeof(GrainPile).GetMethod("RefreshCollider",Flags).Invoke(pile,null); CheckMesh(pile);
            Check(pile.Scoop(pile.transform.position+Vector3.right*3,.4f,.18f,6.5f)==0,"Cannot scoop beyond wall");
        }
        finally { pile.RebuildPreview(); kernels.Rebuild(); }
        var root=new GameObject("Temporary wall-fit checks");
        try
        {
            Vector3 origin=new Vector3(-200,0,-200);
            var testObject=new GameObject("Test pile"); testObject.transform.SetParent(root.transform); testObject.transform.position=origin;
            var test=testObject.AddComponent<GrainPile>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position=origin+new Vector3(1.4f,2,1.4f); wall.transform.rotation=Quaternion.Euler(0,45,0); wall.transform.localScale=new Vector3(8,4,.2f);
            test.transform.localScale=new Vector3(1.2f,1.3f,.8f); test.transform.rotation=Quaternion.Euler(0,18,0);
            Physics.SyncTransforms(); test.RebuildPreview();
            Check(test.DetectedWallCount>0,"Detect rotated wall with nonuniform pile scale"); CheckMesh(test);
            var second=GameObject.CreatePrimitive(PrimitiveType.Cube); second.transform.SetParent(root.transform);
            second.transform.position=origin+new Vector3(-1.5f,2,0); second.transform.localScale=new Vector3(.2f,4,8);
            Physics.SyncTransforms(); test.RebuildPreview();
            Check(test.DetectedWallCount>=2,"Detect corner walls"); CheckMesh(test);
            wall.transform.position+=Vector3.one*30; second.transform.position+=Vector3.one*30;
            Physics.SyncTransforms(); test.SendMessage("Update");
            Check(test.DetectedWallCount==0,"Moving walls away refreshes authoring preview");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        return "PASS: "+checks+" wall-fit checks; all current surface/collider vertices and kernels inside walls; clipped mass, scoops, settling, rotated wall, corner and moved-wall refresh.";
    }
}
