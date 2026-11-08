using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class GrainInteractionPerformanceTests
{
    [Test]
    public void ExcavatedRiceRetainsRiceAppearanceWithoutRetainingStock()
    {
        var field = new GrainHeightField(9, 4, 1, (x, z) => 1);
        var spatial = new GrainSpatialField(field, new[] { new GrainRegion(100, GrainType.Rice) },
            GrainSpatialField.Arrangement.FrontToBack, 0, 731);
        try
        {
            var block = new MaterialPropertyBlock();
            spatial.Bind(block, root.transform);
            spatial.PreviewClear(100);
            spatial.Bind(block, root.transform);
            var riceMap = (Texture2D)block.GetTexture("_GrainMapB");
            var cornMap = (Texture2D)block.GetTexture("_GrainMapA");
            Assert.That(riceMap.GetPixel(4, 4).g, Is.EqualTo(1).Within(.001));
            Assert.That(cornMap.GetPixel(4, 4).r, Is.Zero);
            Assert.That(spatial.VolumeOf(GrainType.Rice), Is.Zero);
            var cargo = new GrainCargo();
            spatial.Sample(0, 0, cargo);
            Assert.That(cargo.Total, Is.Zero);
        }
        finally { spatial.Dispose(); }
    }

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

    [Test]
    public void SmallCoveragePatchesShareOneSubmissionBuffer()
    {
        var kernels = root.AddComponent<CornSurfaceKernels>();
        var queue = typeof(CornSurfaceKernels).GetMethod("QueueBatch", Private);
        var matrices = new Matrix4x4[1];
        for (int i = 0; i < 1000; i++)
        {
            matrices[0] = Matrix4x4.Translate(Vector3.right * i);
            queue.Invoke(kernels, new object[] { null, null, matrices, 1,
                new Bounds(Vector3.right * i, Vector3.one), null, 1 });
        }
        var buckets = Field<System.Collections.IDictionary>(kernels, "drawBuckets");
        Assert.AreEqual(1, buckets.Count);
        foreach (System.Collections.DictionaryEntry entry in buckets)
        {
            object bucket = entry.Value;
            var type = bucket.GetType();
            Assert.AreEqual(1000, type.GetField("count").GetValue(bucket));
            var packed = (Matrix4x4[])type.GetField("matrices").GetValue(bucket);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(Matrix4x4.Translate(Vector3.right * i), packed[i]);
            var bounds = (Bounds)type.GetField("bounds").GetValue(bucket);
            Assert.IsTrue(bounds.Contains(Vector3.zero));
            Assert.IsTrue(bounds.Contains(Vector3.right * 999));
        }
        Assert.AreEqual(0, kernels.LastDrawBatches, "Partial patches must wait for a shared flush.");
    }

    [Test]
    public void DistantBatchesKeepDistributedSamplesAndShareSubmissionBuffer()
    {
        var kernels = root.AddComponent<CornSurfaceKernels>();
        var queue = typeof(CornSurfaceKernels).GetMethod("QueueBatch", Private);
        var matrices = new Matrix4x4[1023];
        for (int i = 0; i < matrices.Length; i++) matrices[i] = Matrix4x4.Translate(Vector3.right * i);
        for (int batch = 0; batch < 2; batch++)
            queue.Invoke(kernels, new object[] { null, null, matrices, matrices.Length,
                new Bounds(Vector3.right * 511, Vector3.one * 1024), null, 16 });
        var buckets = Field<System.Collections.IDictionary>(kernels, "drawBuckets");
        Assert.AreEqual(1, buckets.Count);
        foreach (System.Collections.DictionaryEntry entry in buckets)
        {
            var type = entry.Value.GetType();
            Assert.AreEqual(128, type.GetField("count").GetValue(entry.Value));
            var packed = (Matrix4x4[])type.GetField("matrices").GetValue(entry.Value);
            for (int i = 0; i < 128; i++) Assert.AreEqual(matrices[(i % 64) * 16], packed[i]);
        }
        Assert.AreEqual(0, kernels.LastDrawBatches);
    }

    [Test]
    public void DistantGrainLodHalvesTriangleCountAndHasOutwardFaces()
    {
        var medium = (Mesh)typeof(CornSurfaceKernels).GetMethod("CreateLodMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 4 });
        var distant = (Mesh)typeof(CornSurfaceKernels).GetMethod("CreateDistantLodMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        try
        {
            Assert.AreEqual(medium.triangles.Length / 2, distant.triangles.Length);
            var points = distant.vertices;
            var indices = distant.triangles;
            Vector3 center = Vector3.zero;
            foreach (var point in points) center += point / points.Length;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = points[indices[i]]; var b = points[indices[i + 1]]; var c = points[indices[i + 2]];
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - center), 0);
            }
        }
        finally { Object.DestroyImmediate(medium); Object.DestroyImmediate(distant); }
    }

    [Test]
    public void ExtraCoverageReusesBuffersWhenKernelCountChanges()
    {
        var kernels = root.AddComponent<CornSurfaceKernels>();
        var matrices = Field<List<Matrix4x4>>(kernels, "coverageMatrices");
        var patches = Field<System.Collections.IDictionary>(kernels, "coveragePatches");
        var replace = typeof(CornSurfaceKernels).GetMethod("ReplaceCoveragePatch", Private);
        matrices.Add(Matrix4x4.identity);
        replace.Invoke(kernels, null);
        object patch = patches[0];
        var batchField = patch.GetType().GetField("batches");
        var first = ((Matrix4x4[][])batchField.GetValue(patch))[0];
        matrices.Add(Matrix4x4.Translate(Vector3.one));
        replace.Invoke(kernels, null);
        Assert.AreSame(patch, patches[0]);
        Assert.AreSame(first, ((Matrix4x4[][])batchField.GetValue(patches[0]))[0]);
        Assert.AreEqual(2, kernels.KernelCount);
        Assert.AreEqual(matrices[1], first[1]);
        matrices.RemoveAt(1);
        replace.Invoke(kernels, null);
        Assert.AreEqual(1, kernels.KernelCount);
        Assert.AreSame(first, ((Matrix4x4[][])batchField.GetValue(patches[0]))[0]);
    }
    [Test]
    public void ScoopStopsAtThinSurfaceLayerWithoutRemovingUnderlyingStock()
    {
        var field = new GrainHeightField(9, 2, 1, (x, z) => 1);
        int center = 4 * field.Resolution + 4;
        double topVolume = .02 * field.AreaAt(center);
        double before = field.Volume;
        field.LimitRemoval = (cell, requested) => cell == center ? Math.Min(requested, topVolume) : 0;
        float removed = field.Scoop(0, 0, .5f, .5f, 1);
        Assert.That(removed, Is.EqualTo(topVolume).Within(1e-7));
        Assert.That(field.Heights[center], Is.EqualTo(.98f).Within(1e-6));
        Assert.That(before - field.Volume, Is.EqualTo(removed).Within(1e-7));
        Assert.That(field.Heights[center + 1], Is.EqualTo(1));
    }
    [Test]
    public void FasterPileRefillConservesMassAndMatchesAtThirtyAndOneTwentyFps()
    {
        GrainHeightField Run(float dt, int frames, float speed)
        {
            var go = new GameObject("Refill timing"); go.transform.SetParent(root.transform);
            var pile = go.AddComponent<GrainPile>();
            var field = Field<GrainHeightField>(pile, "field");
            field.Scoop(0, 0, .5f, .5f, 1f, .45f);
            double volume = field.Volume;
            typeof(GrainPile).GetField("settling", Private).SetValue(pile, true);
            typeof(GrainPile).GetField("settlingSpeed", Private).SetValue(pile, speed);
            var step = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), pile,
                typeof(GrainPile).GetMethod("AdvanceSettling", Private));
            for (int i = 0; i < frames; i++) step(dt);
            Assert.AreEqual(volume, field.Volume, .00001);
            return field;
        }
        var at30 = Run(1f / 30, 6, 4);
        var at120 = Run(1f / 120, 24, 4);
        for (int i = 0; i < at30.Heights.Length; i++) Assert.AreEqual(at30.Heights[i], at120.Heights[i], .00001f);
        var slow = Run(1f / 30, 6, 1);
        Assert.Greater(at30.Sample(0, 0), slow.Sample(0, 0), "The excavated center should refill faster.");
    }

    [Test]
    public void StandingScoopAcceptsLowAuthoredPileButRejectsObstructionsAndDistance()
    {
        root.transform.position = Vector3.up * 1000;
        var ground = new GameObject("Low authored pile");
        ground.transform.SetParent(root.transform, false);
        var pile = ground.AddComponent<GrainPile>();
        var saved = pile.CaptureSave();
        for (int i = 0; i < saved.heights.Length; i++) saved.heights[i] = .05f;
        pile.RestoreSave(saved);
        var actor = new GameObject("Standing actor");
        actor.transform.SetParent(root.transform, false);
        var camera = actor.AddComponent<Camera>();
        camera.enabled = false;
        actor.transform.localPosition = new Vector3(0, 1.8f, -1);
        var toolObject = new GameObject("Shovel");
        toolObject.transform.SetParent(actor.transform, false);
        var tool = toolObject.AddComponent<ShovelTool>();
        void Set(string name, object value) => typeof(ShovelTool).GetField(name, Private).SetValue(tool, value);
        Set("actionCamera", camera); Set("actor", actor.transform); Set("pendingPile", pile);
        Set("targetPoint", ground.transform.position + Vector3.up * .05f);
        var canScrape = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), tool,
            typeof(ShovelTool).GetMethod("CanScrapeTarget", Private));
        Physics.SyncTransforms();
        Assert.IsTrue(canScrape(), "Standing above low grain should not require animated blade contact.");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(root.transform, false);
        wall.transform.localPosition = new Vector3(0, 1, -.5f);
        wall.transform.localScale = new Vector3(2, 2, .1f);
        Physics.SyncTransforms();
        Assert.IsFalse(canScrape(), "A wall must block the scoop.");
        Object.DestroyImmediate(wall);
        actor.transform.localPosition = new Vector3(0, 1.8f, -5);
        Physics.SyncTransforms();
        Assert.IsFalse(canScrape(), "Moving out of reach must cancel collection.");
        actor.transform.localPosition = new Vector3(0, 1.8f, -1);
        actor.transform.LookAt(ground.transform.position + Vector3.up * .05f);
        Set("bladeTip", toolObject.transform); // Deliberately above the grain.
        typeof(ShovelTool).GetMethod("Awake", Private).Invoke(tool, null);
        Physics.SyncTransforms();
        var previousData = SaveSystem.Data;
        try
        {
            SaveSystem.Data = new PlayerData();
            Assert.IsTrue(tool.TryUse(camera, actor.transform));
            Assert.IsTrue(tool.IsScooping);
            tool.Tick(.6f);
            Assert.Greater(tool.Kilograms, 0, "Standing scoop must actually transfer grain despite the high blade.");
            Assert.LessOrEqual(tool.Kilograms, tool.CapacityKg);
        }
        finally { tool.CancelAction(); SaveSystem.Data = previousData; }
    }

    [TestCase(0f, 1f)]
    [TestCase(1f, 0f)]
    public void PileVisualTransitionIsGradualAndDoesNotChangeStoredGrain(float start, float target)
    {
        var go = new GameObject("Smooth pile");
        go.transform.SetParent(root.transform);
        var pile = go.AddComponent<GrainPile>();
        var actual = Field<GrainHeightField>(pile, "field");
        var visual = Field<GrainHeightField>(pile, "visualField");
        int center = actual.Heights.Length / 2;
        visual.Heights[center] = start;
        actual.Heights[center] = target;
        double mass = pile.Kilograms;
        var step = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), pile,
            typeof(GrainPile).GetMethod("SmoothVisualSurface", Private));
        Assert.IsTrue(step(1f / 60));
        Assert.That(visual.Heights[center], Is.InRange(Mathf.Min(start, target) + .001f, Mathf.Max(start, target) - .001f));
        for (int i = 0; i < 120; i++) step(1f / 60);
        Assert.AreEqual(target, visual.Heights[center]);
        Assert.AreEqual(mass, pile.Kilograms);
        Assert.AreEqual(target, pile.CaptureSave().heights[center]);
    }

    [Test]
    public void PileVisualTransitionIsIndependentOfFrameRate()
    {
        var go = new GameObject("Smooth pile frame rates");
        go.transform.SetParent(root.transform);
        var pile = go.AddComponent<GrainPile>();
        var actual = Field<GrainHeightField>(pile, "field");
        var visual = Field<GrainHeightField>(pile, "visualField");
        int center = actual.Heights.Length / 2;
        actual.Heights[center] = 1;
        var step = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), pile,
            typeof(GrainPile).GetMethod("SmoothVisualSurface", Private));
        visual.Heights[center] = 0;
        for (int i = 0; i < 6; i++) step(1f / 30);
        float at30 = visual.Heights[center];
        visual.Heights[center] = 0;
        for (int i = 0; i < 24; i++) step(1f / 120);
        Assert.AreEqual(at30, visual.Heights[center], .00001f);
    }

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
