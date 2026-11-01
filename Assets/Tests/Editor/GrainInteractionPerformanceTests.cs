using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class GrainInteractionPerformanceTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private delegate bool HeightSample(float x,float z,out float height);
    private GameObject root;
    [SetUp] public void SetUp()=>root=new GameObject("Grain interaction regressions");
    [TearDown] public void TearDown()=>Object.DestroyImmediate(root);
    private GameObject Bag(float fill)
    {
        var bag=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab"),root.transform);
        VehicleCargoPreview.PrepareContainer(bag,fill,0);bag.GetComponent<BagCornSurface>().Rebuild();return bag;
    }
    private static T Field<T>(object obj,string field)=>(T)obj.GetType().GetField(field,Private).GetValue(obj);
    private static void UpdateBag(BagCornSurface surface)=>typeof(BagCornSurface).GetMethod("LateUpdate",Private).Invoke(surface,null);

    [TestCase(.2f)][TestCase(.75f)][TestCase(1f)]
    public void SpatialSamplingMatchesEveryTriangleReferenceAcrossTheBag(float fill)
    {
        var surface=Bag(fill).GetComponent<BagCornSurface>();
        var vertices=Field<List<Vector3>>(surface,"vertices");var triangles=Field<List<int>>(surface,"triangles");var bounds=Field<Bounds>(surface,"samplingBounds");
        var sample=(HeightSample)Delegate.CreateDelegate(typeof(HeightSample),surface,typeof(BagCornSurface).GetMethod("Sample",Private));
        for(int z=0;z<=32;z++)for(int x=0;x<=32;x++)
        {
            float px=Mathf.Lerp(bounds.min.x,bounds.max.x,x/32f),pz=Mathf.Lerp(bounds.min.z,bounds.max.z,z/32f);
            bool found=sample(px,pz,out float actual);float expected=float.NegativeInfinity;
            for(int i=0;i<triangles.Count;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.0000001f)continue;
                float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/d,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/d;
                if(u<0||v<0||u+v>1)continue;expected=Mathf.Max(expected,u*a.y+v*b.y+(1-u-v)*c.y);
            }
            Assert.AreEqual(!float.IsNegativeInfinity(expected),found,$"Surface coverage changed at {x},{z}");
            if(found)Assert.AreEqual(expected,actual,.00001f);
        }
    }
    [Test]
    public void MovingAndRotatingBagReusesBakedSurfaceAndKeepsKernelsAttached()
    {
        var bag=Bag(.75f);var surface=bag.GetComponent<BagCornSurface>();var grain=Field<SkinnedMeshRenderer>(surface,"grainSurface");
        int bakes=surface.SurfaceBakeCount,count=surface.KernelCount;Assert.Greater(count,0);
        Matrix4x4 old=Field<Matrix4x4[]>(surface,"first")[0],local=grain.transform.worldToLocalMatrix*old;
        bag.transform.position+=new Vector3(7,3,-4);bag.transform.rotation=Quaternion.Euler(0,87,0)*bag.transform.rotation;
        UpdateBag(surface);
        Assert.AreEqual(bakes,surface.SurfaceBakeCount);Assert.AreEqual(count,surface.KernelCount);
        Matrix4x4 expected=grain.transform.localToWorldMatrix*local,actual=Field<Matrix4x4[]>(surface,"first")[0];
        for(int i=0;i<16;i++)Assert.AreEqual(expected[i],actual[i],.0001f);
        bag.GetComponent<SackController>().Fill=.8f;UpdateBag(surface);Assert.AreEqual(bakes+1,surface.SurfaceBakeCount);
    }
    [Test]
    public void ScalingBagStillResamplesItsWorldSpaceKernelDensity()
    {
        var bag=Bag(.75f);var surface=bag.GetComponent<BagCornSurface>();int bakes=surface.SurfaceBakeCount;
        bag.transform.localScale*=2;UpdateBag(surface);Assert.AreEqual(bakes+1,surface.SurfaceBakeCount);
    }
    [Test]
    public void LoadedShovelSleepsDuringUniformWalkingButWakesForAccelerationAndTurning()
    {
        var player=Object.Instantiate(Resources.Load<GameObject>("Player"),root.transform);var grain=player.GetComponentInChildren<ShovelGrainPhysics>(true);
        typeof(ShovelGrainPhysics).GetMethod("Awake",Private).Invoke(grain,null);
        var step=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),grain,typeof(ShovelGrainPhysics).GetMethod("Simulate",Private));
        grain.SetFill(1);for(int i=0;i<300;i++){grain.transform.position+=Vector3.forward*.03f;step(.01f);}
        Assert.IsTrue(grain.IsLoadSleeping);int count=grain.KernelCount;Assert.Greater(count,0);
        var positions=Field<Vector3[]>(grain,"positions");var snapshot=(Vector3[])positions.Clone();
        for(int i=0;i<20;i++){grain.transform.position+=Vector3.forward*.03f;step(.01f);}
        for(int i=0;i<count;i++)Assert.AreEqual(snapshot[i],positions[i]);
        grain.transform.position+=Vector3.forward*.15f;step(.01f);Assert.IsFalse(grain.IsLoadSleeping);Assert.AreEqual(count,grain.KernelCount);
        for(int i=0;i<200;i++){grain.transform.position+=Vector3.forward*.03f;step(.01f);}
        Assert.IsTrue(grain.IsLoadSleeping);
        grain.transform.rotation=Quaternion.Euler(0,45,0)*grain.transform.rotation;step(.01f);Assert.IsFalse(grain.IsLoadSleeping);Assert.AreEqual(count,grain.KernelCount);
    }

    private ShovelGrainPhysics LoadedShovel(out Action<float> step,out Action update)
    {
        var player=Object.Instantiate(Resources.Load<GameObject>("Player"),root.transform);
        var grain=player.GetComponentInChildren<ShovelGrainPhysics>(true);
        typeof(ShovelGrainPhysics).GetMethod("Awake",Private).Invoke(grain,null);
        step=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),grain,typeof(ShovelGrainPhysics).GetMethod("Simulate",Private));
        update=(Action)Delegate.CreateDelegate(typeof(Action),grain,typeof(ShovelGrainPhysics).GetMethod("UpdateLoad",Private));
        grain.SetFill(1);for(int i=0;i<200;i++)step(.01f);update();return grain;
    }
    [Test]
    public void ActiveLoadMeshKeepsGeometryAndUpdatesOnlyTheChangedKernel()
    {
        var grain=LoadedShovel(out var step,out var update);var mesh=Field<Mesh>(grain,"visibleLoad");
        var kernel=Field<Mesh>(grain,"kernelMesh");int amount=grain.KernelCount+grain.PackedKernelCount;
        Assert.AreEqual((uint)(amount*kernel.GetIndexCount(0)),mesh.GetIndexCount(0));
        var positions=Field<Vector3[]>(grain,"positions");
        update();Assert.AreEqual(0,grain.LastUploadedLoadVertices);
        positions[0]+=Vector3.right*.001f;update();Assert.AreEqual(kernel.vertexCount,grain.LastUploadedLoadVertices);
        var matrices=Field<Matrix4x4[]>(grain,"matrices");var source=kernel.vertices;var normals=kernel.normals;
        var output=mesh.vertices;var outputNormals=mesh.normals;
        foreach(int k in new[]{0,grain.KernelCount-1,amount-1})
        for(int j=0;j<source.Length;j++)
        {
            Assert.Less(Vector3.Distance(matrices[k].MultiplyPoint3x4(source[j]),output[k*source.Length+j]),.00001f);
            Assert.Less(Vector3.Distance(matrices[k].inverse.transpose.MultiplyVector(normals[j]).normalized,outputNormals[k*source.Length+j]),.00001f);
            Assert.IsTrue(mesh.bounds.Contains(output[k*source.Length+j]));
        }
        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off,Field<MeshRenderer>(grain,"loadRenderer").shadowCastingMode);
    }
    [Test]
    public void LoadCountChangesReuseRetainedVerticesAndUploadNewSlots()
    {
        var grain=LoadedShovel(out var step,out var update);
        var draw=(Action<int>)Delegate.CreateDelegate(typeof(Action<int>),grain,typeof(ShovelGrainPhysics).GetMethod("DrawLoad",Private));
        var kernel=Field<Mesh>(grain,"kernelMesh");
        var mesh=Field<Mesh>(grain,"visibleLoad");
        int amount=grain.KernelCount+grain.PackedKernelCount;
        draw(amount-1);
        Assert.AreEqual(0,grain.LastUploadedLoadVertices);
        Assert.AreEqual((uint)((amount-1)*kernel.GetIndexCount(0)),mesh.GetIndexCount(0));
        draw(amount);
        Assert.AreEqual(kernel.vertexCount,grain.LastUploadedLoadVertices);
        Assert.AreEqual((uint)(amount*kernel.GetIndexCount(0)),mesh.GetIndexCount(0));
    }
    [Test]
    public void PurePourStreamDoesNotQuerySceneCollidersAndDrainsAfterLanding()
    {
        var grain=LoadedShovel(out var step,out var update);
        grain.SetPourTarget(grain.transform.position+Vector3.down*.6f,null);grain.SetFill(0);
        step(.01f);update();Assert.AreEqual(0,grain.LastWorldCollisionQueries);Assert.AreEqual(0,grain.KernelCount);
        for(int i=0;i<100;i++){step(.01f);Assert.AreEqual(0,grain.LastWorldCollisionQueries);}
        update();Assert.IsFalse(Field<MeshRenderer>(grain,"loadRenderer").enabled);
        foreach(float life in Field<float[]>(grain,"streamLife"))Assert.LessOrEqual(life,0f);
    }
    [TestCase(.01f)][TestCase(.033f)][TestCase(.05f)]
    public void MovingLoadStaysBoundedAtFastAndSlowFrameRates(float dt)
    {
        var grain=LoadedShovel(out var step,out var update);int count=grain.KernelCount;
        for(int i=0;i<120;i++)
        {
            grain.transform.rotation=Quaternion.Euler(0,i*2f,0);step(dt);
            Assert.AreEqual(count,grain.KernelCount);Assert.LessOrEqual(grain.MaximumBowlViolation,.0001f);
            foreach(var point in Field<Vector3[]>(grain,"positions"))Assert.IsTrue(float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z));
        }
    }
}
