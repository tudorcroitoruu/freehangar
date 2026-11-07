using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GrainEconomyTests
{
    private GameObject root;
    private GrainPile pile;
    private GrainEconomySettings economy;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Economy test pile");
        root.SetActive(false);
        pile = root.AddComponent<GrainPile>();
        Set("fitToWalls", false);
        economy = ScriptableObject.CreateInstance<GrainEconomySettings>();
        Set("economySettings", economy);
        root.SetActive(true);
        pile.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(economy);
    }

    private void Set(string name, object value) => typeof(GrainPile)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pile, value);

    [TestCase(false)]
    [TestCase(true)]
    public void IncompatibleShovelScoopLeavesPileAndCargoUnchanged(bool spatial)
    {
        if (spatial) pile.ConfigureRegions(new[] { new GrainRegion(100f, GrainType.Wheat) });
        else pile.SetRecipe(new[] { new GrainLayer(100f, GrainType.Wheat) });
        var existing = new GrainCargo(); existing.Add(GrainType.Corn, 1f);
        var collected = new GrainCargo();
        var point = new Vector3(0f, pile.SurfaceHeight(Vector3.zero), 0f);
        double before = pile.Kilograms;
        float amount = pile.ScoopCompatibleWith(point, 1f, .2f, 5f, collected, existing, out bool blocked);
        Assert.IsTrue(blocked);
        Assert.AreEqual(0f, amount);
        Assert.AreEqual(before, pile.Kilograms);
        Assert.AreEqual(0f, collected.Total);
        Assert.AreEqual(1f, existing[GrainType.Corn]);
    }

    [Test]
    public void CompatibleShovelScoopStillCollectsGrain()
    {
        pile.SetRecipe(new[] { new GrainLayer(100f, GrainType.Corn) });
        var existing = new GrainCargo(); existing.Add(GrainType.Corn, 1f);
        var collected = new GrainCargo();
        var point = new Vector3(0f, pile.SurfaceHeight(Vector3.zero), 0f);
        float amount = pile.ScoopCompatibleWith(point, 1f, .2f, 5f, collected, existing, out bool blocked);
        Assert.IsFalse(blocked);
        Assert.Greater(amount, 0f);
        Assert.That(collected[GrainType.Corn], Is.EqualTo(amount).Within(.001f));
    }

    [Test]
    public void ChickenDetectsPileEvenWhenGroundRayStartsInsideIt()
    {
        Set("initialHeight", 3f);
        pile.RebuildPreview();
        Vector3 buriedPosition = root.transform.position;
        Assert.IsFalse(FarmChicken.IsGrainClearAt(buriedPosition));
        Assert.IsTrue(FarmChicken.TryGroundPoint(buriedPosition, out var surface));
        Assert.That(surface.y, Is.GreaterThanOrEqualTo(pile.SurfaceHeight(buriedPosition) - .001f));
    }

    [Test]
    public void MagnetSeparatesMatchingCropFromMixedSurfaceAndConservesOtherCrop()
    {
        pile.ConfigureRegions(new[] { new GrainRegion(100f, GrainType.Corn, GrainType.Wheat) });
        var existing = new GrainCargo(); existing.Add(GrainType.Corn, 1);
        double cornBefore = pile.GrainKilograms(GrainType.Corn);
        double wheatBefore = pile.GrainKilograms(GrainType.Wheat);
        var collected = new GrainCargo();
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float amount = pile.ScoopCompatibleWith(point, .3f, .2f, 5, collected, existing, out bool blocked);
        Assert.IsFalse(blocked);
        Assert.That(amount, Is.GreaterThan(0).And.LessThanOrEqualTo(5.001f));
        Assert.That(collected[GrainType.Corn], Is.EqualTo(amount).Within(.001f));
        Assert.That(collected[GrainType.Wheat], Is.Zero);
        Assert.That(pile.GrainKilograms(GrainType.Wheat), Is.EqualTo(wheatBefore).Within(.001));
        Assert.That(cornBefore - pile.GrainKilograms(GrainType.Corn),
            Is.EqualTo(amount * pile.GrainConsumeMultiplier).Within(.01));
    }

    [Test]
    public void EmptyShovelSelectsOneCropAtMixedBoundary()
    {
        pile.ConfigureRegions(new[] { new GrainRegion(100f, GrainType.Corn, GrainType.Wheat) });
        var collected = new GrainCargo();
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float amount = pile.ScoopCompatibleWith(point, .3f, .2f, 5, collected, new GrainCargo(), out bool blocked);
        Assert.IsFalse(blocked);
        Assert.Greater(amount, 0);
        Assert.That(collected[collected.Dominant], Is.EqualTo(collected.Total).Within(.001f));
    }

    [Test]
    public void MagnetFillsCapacityEvenWithVeryShallowContactDepth()
    {
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Wheat) });
        var collected = new GrainCargo();
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float amount = pile.ScoopCompatibleWith(point, .3f, .00001f, 5, collected, new GrainCargo(), out bool blocked);
        Assert.IsFalse(blocked);
        Assert.That(amount, Is.EqualTo(5).Within(.001f));
        Assert.That(collected[GrainType.Wheat], Is.EqualTo(5).Within(.001f));
    }

    [Test]
    public void EmptyShovelCannotCollectMixedNonSpatialRecipe()
    {
        pile.SetRecipe(new[] { new GrainLayer(100, GrainType.Corn, GrainType.Wheat) });
        var collected = new GrainCargo();
        double before = pile.Kilograms;
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float amount = pile.ScoopCompatibleWith(point, .3f, .2f, 5, collected, new GrainCargo(), out bool blocked);
        Assert.IsTrue(blocked);
        Assert.That(amount, Is.Zero);
        Assert.That(collected.Total, Is.Zero);
        Assert.That(pile.Kilograms, Is.EqualTo(before));
    }

    [Test]
    public void MostlyCornPatchFillsEmptyShovelWithOnlyCornAndLeavesRice()
    {
        var region = new GrainRegion(100, GrainType.Corn, GrainType.Rice);
        typeof(GrainMixture).GetField("parts", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(region.layers[0].mixture, new[] {
                new GrainMixture.Part(GrainType.Corn, 3), new GrainMixture.Part(GrainType.Rice, 1) });
        pile.ConfigureRegions(new[] { region });
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        var surface = new GrainCargo();
        pile.SampleSurfaceGrain(point, surface);
        Assert.That(surface[GrainType.Corn], Is.EqualTo(.75f).Within(.00001f));
        Assert.That(surface[GrainType.Rice], Is.EqualTo(.25f).Within(.00001f));
        double riceBefore = pile.GrainKilograms(GrainType.Rice);
        var collected = new GrainCargo();
        float amount = pile.ScoopCompatibleWith(point, .3f, .01f, 5, collected, new GrainCargo(), out bool blocked);
        Assert.IsFalse(blocked);
        Assert.That(amount, Is.EqualTo(5).Within(.001f));
        Assert.That(collected[GrainType.Corn], Is.EqualTo(amount).Within(.001f));
        Assert.That(collected[GrainType.Rice], Is.Zero);
        Assert.That(pile.GrainKilograms(GrainType.Rice), Is.EqualTo(riceBefore).Within(.001));
    }

    [Test]
    public void LocalAutoSortRaisesSelectedCropWithoutMovingOrLosingStock()
    {
        var field = new GrainHeightField(17, 4, 1, (x, z) => 1);
        var region = new GrainRegion(100) { layers = new[] {
            new GrainStratum(50, GrainType.Rice), new GrainStratum(50, GrainType.Corn) } };
        var spatial = new GrainSpatialField(field, new[] { region }, GrainSpatialField.Arrangement.FrontToBack, 0, 1);
        var beforeHeights = (float[])field.Heights.Clone();
        double corn = spatial.VolumeOf(GrainType.Corn), rice = spatial.VolumeOf(GrainType.Rice);
        Assert.IsTrue(spatial.BringCropToSurface(0, 0, .75f, Vector3.one, GrainType.Corn));
        var sample = new GrainCargo(); spatial.Sample(0, 0, sample);
        Assert.That(sample[GrainType.Corn], Is.EqualTo(1).Within(.000001));
        spatial.Sample(1.5f, 0, sample);
        Assert.That(sample[GrainType.Rice], Is.EqualTo(1).Within(.000001));
        Assert.That(spatial.VolumeOf(GrainType.Corn), Is.EqualTo(corn).Within(1e-8));
        Assert.That(spatial.VolumeOf(GrainType.Rice), Is.EqualTo(rice).Within(1e-8));
        CollectionAssert.AreEqual(beforeHeights, field.Heights);
        Assert.IsFalse(spatial.BringCropToSurface(0, 0, .75f, Vector3.one, GrainType.Corn));
        var depositedRice = new float[GrainCargo.TypeCount]; depositedRice[(int)GrainType.Rice] = 1;
        spatial.Deposit(8 * field.Resolution + 8, .001, depositedRice);
        Assert.IsTrue(spatial.BringCropToSurface(0, 0, .75f, Vector3.one, GrainType.Corn),
            "A new covering layer must invalidate the cached ordering.");
        spatial.Dispose();
    }

    [Test]
    public void MagnetExpandsPastEmptyInnerAreaToReachMatchingStock()
    {
        var field = new GrainHeightField(9, 8, 0);
        var spatial = new GrainSpatialField(field, new[] { new GrainRegion(100, GrainType.Corn) },
            GrainSpatialField.Arrangement.FrontToBack, 0, 1);
        var corn = new float[GrainCargo.TypeCount]; corn[(int)GrainType.Corn] = 1;
        spatial.Deposit(4 * field.Resolution + 7, .25, corn);
        spatial.ReconcileHeights();
        float radius = spatial.CollectionRadius(0, 0, 1.25f, 4.5f, Vector3.one, GrainType.Corn, .1);
        Assert.That(radius, Is.GreaterThan(3).And.LessThanOrEqualTo(4.5f));
        Assert.That(spatial.CollectionRadius(0, 0, 1.25f, 4.5f, Vector3.one, GrainType.Rice, .1), Is.EqualTo(4.5f));
        spatial.Dispose();
    }

    [Test]
    public void SavedSpillRepairsGhostHeightsAndCollectsConsecutiveCornLayers()
    {
        var field = new GrainHeightField(9, 2, 0);
        var spatial = new GrainSpatialField(field, new[] { new GrainRegion(100, GrainType.Corn) },
            GrainSpatialField.Arrangement.FrontToBack, 0, 731);
        int center = 4 * field.Resolution + 4;
        var corn = new float[GrainCargo.TypeCount]; corn[(int)GrainType.Corn] = 1;
        var saved = new System.Collections.Generic.List<GrainColumnSave>();
        for (int i = 0; i < 20; i++) saved.Add(new GrainColumnSave { index = center, volume = .0001, weights = (float[])corn.Clone() });
        field.Heights[center] = .2f; // Old save: independent mesh height drifted.
        field.Heights[center + 1] = .000002f; // No remaining typed grain here.
        spatial.RestoreSave(saved);
        Assert.That(field.Heights[center + 1], Is.Zero);
        Assert.That(field.Volume, Is.EqualTo(.002).Within(1e-9));
        Assert.That(spatial.VolumeOf(GrainType.Corn), Is.EqualTo(.002).Within(1e-9));
        var allowed = new GrainCargo(); allowed.Add(GrainType.Corn, 1);
        Assert.That(spatial.MatchingSurfaceVolume(center, 1, allowed), Is.EqualTo(.002).Within(1e-9));
        var collected = new GrainCargo();
        typeof(GrainSpatialField).GetField("CollectionFilter", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(spatial, allowed);
        field.LimitRemoval = (cell, requested) => spatial.MatchingSurfaceVolume(cell, requested, allowed);
        spatial.BeginCollection(collected, 1);
        float amount = field.Scoop(0, 0, .1f, 1, 1);
        spatial.EndCollection();
        Assert.That(amount, Is.EqualTo(.002).Within(1e-8));
        Assert.That(collected.Total, Is.EqualTo(amount).Within(1e-8));
        Assert.That(field.Heights[center], Is.Zero);
        Assert.That(spatial.VolumeOf(GrainType.Corn), Is.Zero.Within(1e-12));
        spatial.Dispose();
    }

    [Test]
    public void MagnetCutDoesNotImmediatelyRefillDuringSettling()
    {
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Corn) });
        var selected = new GrainCargo(); selected.Add(GrainType.Corn, 1);
        var point = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float removed = pile.ScoopCompatibleWith(point, .3f, .2f, 5, new GrainCargo(), selected, out _);
        Assert.Greater(removed, 0);
        float cutHeight = pile.SurfaceHeight(point);
        double cutMass = pile.Kilograms;
        var settle = typeof(GrainPile).GetMethod("AdvanceSettling", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 120; i++) settle.Invoke(pile, new object[] { 1f / 60f });
        Assert.That(pile.SurfaceHeight(point), Is.LessThanOrEqualTo(cutHeight + .001f));
        Assert.That(pile.Kilograms, Is.EqualTo(cutMass).Within(.001));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SurfaceRefreshTiming(bool clipped)
    {
        if (clipped) Set("excludedFootprint", new Rect(1f, -1f, 2f, 2f));
        pile.RebuildPreview();
        var refresh = typeof(GrainPile).GetMethod("RefreshSurface", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 5; i++) refresh.Invoke(pile, null);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 40; i++) refresh.Invoke(pile, null);
        timer.Stop();
        TestContext.WriteLine($"Surface refresh clipped={clipped}: {timer.Elapsed.TotalMilliseconds / 40:F3} ms average");
        Assert.Greater(root.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
    }

    [Test]
    public void ScoopCrossingIntoAnotherRecipeLayerDoesNotExcavateEitherLayer()
    {
        pile.SetRecipe(new[] { new GrainLayer(.01f, GrainType.Corn), new GrainLayer(100f, GrainType.Wheat) });
        var existing = new GrainCargo(); existing.Add(GrainType.Corn, 1f);
        var collected = new GrainCargo();
        var point = new Vector3(0f, pile.SurfaceHeight(Vector3.zero), 0f);
        double before = pile.Kilograms;
        pile.ScoopCompatibleWith(point, 1f, .2f, 5f, collected, existing, out bool blocked);
        Assert.IsTrue(blocked);
        Assert.AreEqual(before, pile.Kilograms);
        Assert.AreEqual(0f, collected.Total);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DepositOnAuthoredEdgeClosesSurfaceWithoutChangingMass(bool clipped)
    {
        Set("initialHeight", 0f);
        if (clipped) Set("excludedFootprint", new Rect(1f, -1f, 2f, 2f));
        pile.RebuildPreview();
        double before = pile.Kilograms;
        var cargo = new GrainCargo();
        cargo.Add(GrainType.Corn, 10f);
        float edgeX = clipped ? 1f : 3f;
        Assert.IsTrue(pile.Deposit(new Vector3(edgeX - .1f, 0, 0), cargo, 1f));
        Assert.That(pile.Kilograms - before, Is.EqualTo(10 * pile.GrainConsumeMultiplier).Within(.1));
        var mesh = root.GetComponent<MeshFilter>().sharedMesh;
        var points = mesh.vertices;
        var triangles = mesh.triangles;
        bool closed = false;
        bool sloped = false;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            if (a.x < edgeX - .001f || b.x < edgeX - .001f || c.x < edgeX - .001f) continue;
            if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < 0 && Mathf.Max(a.y, Mathf.Max(b.y, c.y)) > 0)
            {
                closed = true;
                sloped |= Mathf.Max(a.x, Mathf.Max(b.x, c.x)) > edgeX + .01f;
            }
        }
        Assert.IsTrue(closed, "Raised stock edge must have a side face reaching the ground.");
        if (!clipped) Assert.IsTrue(sloped, "An exposed edge should slope outward instead of forming a vertical wall.");
    }

    [Test]
    public void DroppedGrainFollowsSlopedGroundAndRestoresWithoutAddingMass()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GrainPile dropped = null, restored = null;
        try
        {
            ground.transform.position = new Vector3(10000, 0, 10000);
            ground.transform.localScale = new Vector3(30, .2f, 30);
            ground.transform.rotation = Quaternion.Euler(0, 0, 12);
            Physics.SyncTransforms();
            var collider = ground.GetComponent<Collider>();
            Assert.IsTrue(collider.Raycast(new Ray(ground.transform.position + Vector3.up * 10, Vector3.down), out var center, 20));
            dropped = GrainPile.CreateDroppedPile(center.point, pile, 6.5f);
            var cargo = new GrainCargo(); cargo.Add(GrainType.Corn, 6.5f);
            Assert.IsTrue(dropped.Deposit(center.point, cargo, GrainPile.DropRadius(6.5f)));
            var saved = dropped.CaptureSave();
            restored = GrainPile.CreateDroppedPile(center.point, pile);
            restored.RestoreSave(saved);
            foreach (var target in new[] { dropped, restored })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = center.point + Vector3.right * (target.Size * .4f * side);
                    Assert.IsTrue(collider.Raycast(new Ray(p + Vector3.up * 10, Vector3.down), out var hit, 20));
                    float thickness = target.SampleLocalHeight(p.x - center.point.x, 0);
                    Assert.That(target.SurfaceHeight(p) - thickness, Is.EqualTo(hit.point.y).Within(.002f));
                }
                Assert.That(target.CollectibleKilograms, Is.EqualTo(6.5).Within(.002));
                var collision = target.GetComponent<MeshCollider>().sharedMesh;
                var vertices = collision.vertices;
                foreach (int index in collision.triangles)
                {
                    Vector3 p = target.transform.TransformPoint(vertices[index]);
                    Assert.That(p.y, Is.EqualTo(target.SurfaceHeight(p)).Within(.002));
                }
                Vector3 contact = center.point; contact.y = target.SurfaceHeight(contact);
                Assert.Greater(target.Scoop(contact, .5f, .2f, 1f, new GrainCargo()), 0);
            }
        }
        finally
        {
            if (dropped != null) Object.DestroyImmediate(dropped.gameObject);
            if (restored != null) Object.DestroyImmediate(restored.gameObject);
            Object.DestroyImmediate(ground);
        }
    }

    [TestCase(200f)]
    [TestCase(900f)]
    [TestCase(3000f)]
    public void ScoopDoesNotRemoveGrainOutsideToolRadius(float multiplier)
    {
        economy.grainConsumeMultiplier = multiplier;
        Vector3 remote = new Vector3(1.5f, 0, 0);
        float before = pile.SurfaceHeight(remote);
        Vector3 contact = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float collected = pile.Scoop(contact, .4f, .18f, 2.5f, new GrainCargo());
        Assert.Greater(collected, 0);
        Assert.Less(pile.SurfaceHeight(Vector3.zero), contact.y);
        Assert.AreEqual(before, pile.SurfaceHeight(remote));
    }

    [TestCase(1f, false)]
    [TestCase(10f, false)]
    [TestCase(200f, false)]
    [TestCase(1f, true)]
    [TestCase(10f, true)]
    [TestCase(200f, true)]
    public void CollectionConsumesMultipliedStockButFillsOnlyRequestedCargo(float multiplier, bool spatial)
    {
        economy.grainConsumeMultiplier = multiplier;
        if (spatial) pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Wheat, GrainType.Rice) });
        double before = pile.Kilograms;
        var cargo = new GrainCargo();
        Vector3 contact = new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0);
        float collected = pile.Scoop(contact, 100, 10, 10, cargo);
        Assert.That(collected, Is.EqualTo(10).Within(.002));
        Assert.That(cargo.Total, Is.EqualTo(collected).Within(.002));
        Assert.That(before - pile.Kilograms, Is.EqualTo(collected * multiplier).Within(.1));
        Assert.That(pile.CollectibleKilograms, Is.EqualTo(pile.Kilograms / multiplier).Within(.001));
        Assert.That(pile.HarvestedKg, Is.EqualTo(collected).Within(.002));
        if (spatial)
        {
            Assert.That(cargo[GrainType.Wheat], Is.EqualTo(collected * .5).Within(.002));
            Assert.That(cargo[GrainType.Rice], Is.EqualTo(collected * .5).Within(.002));
        }
    }

    [TestCase(10f)]
    [TestCase(200f)]
    public void DepositingBackIntoAuthoredStockPreservesRecoverableMass(float multiplier)
    {
        economy.grainConsumeMultiplier = multiplier;
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Wheat) });
        double initial = pile.Kilograms;
        var cargo = new GrainCargo();
        pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, 10, cargo);
        Assert.IsTrue(pile.Deposit(Vector3.zero, cargo, 10));
        Assert.That(cargo.Total, Is.EqualTo(0));
        Assert.That(pile.Kilograms, Is.EqualTo(initial).Within(.2));
        Assert.That(pile.GrainKilograms(GrainType.Wheat), Is.EqualTo(initial).Within(.2));
        Assert.That(pile.ClearedPercent, Is.EqualTo(0).Within(.01));
    }

    [Test]
    public void DroppedCargoIsRecoveredOneForOneEvenWithAnEconomySource()
    {
        economy.grainConsumeMultiplier = 200;
        var dropped = GrainPile.CreateDroppedPile(new Vector3(100, 0, 100), pile, 10);
        try
        {
            Assert.That(dropped.GrainConsumeMultiplier, Is.EqualTo(pile.GrainConsumeMultiplier));
            var cargo = new GrainCargo();
            double before = pile.Kilograms / 720.0;
            pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, 10, cargo);
            double removedVolume = before - pile.Kilograms / 720.0;
            var expected = new float[GrainCargo.TypeCount];
            for (int i = 0; i < expected.Length; i++) expected[i] = cargo[(GrainType)i];
            Assert.IsTrue(dropped.Deposit(dropped.transform.position, cargo));
            Assert.That(dropped.Kilograms / 720.0, Is.EqualTo(removedVolume).Within(.002));
            Assert.That(dropped.CollectibleKilograms, Is.EqualTo(10).Within(.002));
            Vector3 point = dropped.transform.position;
            point.y = dropped.SurfaceHeight(point);
            float collected = dropped.Scoop(point, 100, 10, 10, cargo);
            Assert.That(collected, Is.EqualTo(10).Within(.002));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(cargo[(GrainType)i], Is.EqualTo(expected[i]).Within(.002));
        }
        finally { Object.DestroyImmediate(dropped.gameObject); }
    }

    [TestCase(6.5f)]
    [TestCase(25f)]
    public void PouredGrainSettlesAsAMoundWithBareEdgesAndPreservesBothCrops(float kilograms)
    {
        economy.grainConsumeMultiplier = 220;
        Set("kilogramsPerCubicMetre", 360f);
        var dropped = GrainPile.CreateDroppedPile(new Vector3(10000, 0, 10000), pile, kilograms);
        try
        {
            var cargo = new GrainCargo();
            cargo.Add(GrainType.Corn, kilograms * .4f);
            cargo.Add(GrainType.Wheat, kilograms * .6f);
            Assert.IsTrue(dropped.Deposit(dropped.transform.position, cargo, GrainPile.DropRadius(kilograms)));
            var field = (GrainHeightField)typeof(GrainPile).GetField("field", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dropped);
            for (int i = 0; i < 600; i++) field.Settle(32, .45f);
            Assert.Greater(field.Sample(0, 0), field.Sample(field.Size * .3f, 0) * 1.5f, "Spilled grain should keep a mound, not flatten into a slab.");
            for (int i = 0; i < field.Resolution; i++)
            {
                Assert.Less(field.Heights[i], .0001f);
                Assert.Less(field.Heights[(field.Resolution - 1) * field.Resolution + i], .0001f);
                Assert.Less(field.Heights[i * field.Resolution], .0001f);
                Assert.Less(field.Heights[i * field.Resolution + field.Resolution - 1], .0001f);
            }
            Assert.AreEqual(kilograms, dropped.CollectibleKilograms, .002);
            Assert.AreEqual(kilograms * .4f, dropped.GrainKilograms(GrainType.Corn) / 220, .002);
            Assert.AreEqual(kilograms * .6f, dropped.GrainKilograms(GrainType.Wheat) / 220, .002);
        }
        finally { Object.DestroyImmediate(dropped.gameObject); }
    }

    [TestCase(0f, 1f)]
    [TestCase(-10f, 1f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(100000f, 10000f)]
    public void InvalidMultipliersCannotProduceNegativeOrInfiniteCargo(float value, float expected)
    {
        economy.grainConsumeMultiplier = value;
        Assert.That(pile.GrainConsumeMultiplier, Is.EqualTo(expected));
    }

    [Test]
    public void LargerStartingShovelFillsFreeBagInTwoLoadsWithoutLosingTheRemainder()
    {
        var settings=GrainEconomySettings.Default.shovelSettings;
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");
        var bag=Object.Instantiate(prefab).GetComponent<GrainContainer>();
        try
        {
            Assert.That(settings.Capacity(0),Is.EqualTo(2.5f));
            Assert.That(bag.CapacityKg,Is.EqualTo(10f));
            economy.grainConsumeMultiplier=200;
            double before=pile.Kilograms;
            var cargo=new GrainCargo();
            for(int i=0;i<4;i++)
            {
                Vector3 point=new Vector3(0,pile.SurfaceHeight(Vector3.zero),0);
                float collected=pile.Scoop(point,100,10,settings.Capacity(0),cargo);
                Assert.That(collected,Is.EqualTo(2.5f).Within(.002));
                cargo.PourInto(bag,settings.Capacity(0));
                if(i<3)Assert.IsFalse(bag.IsFull);
            }
            Assert.That(bag.Kilograms,Is.EqualTo(10).Within(.002));
            Assert.IsTrue(bag.IsFull);
            Assert.That(cargo.Total,Is.EqualTo(0).Within(.002));
            Assert.That(before-pile.Kilograms,Is.EqualTo(2000).Within(.2));
        }
        finally { Object.DestroyImmediate(bag.gameObject); }
    }

    [Test]
    public void ManualShovelUpgradeLoadsAreTripledWhileVacuumCapacityIsPreserved()
    {
        var settings = GrainEconomySettings.Default.shovelSettings;
        float[] previousLoads = { 2.5f, 8.5f, 14.5f, 20f };
        for (int type = 0; type < previousLoads.Length; type++)
            for (int dot = 0; dot < ShovelUpgradeSettings.UpgradesPerShovel; dot++)
                Assert.That(settings.Capacity(type * 4 + dot),
                    Is.EqualTo(previousLoads[type] + dot).Within(.001));
        Assert.That(settings.Capacity(16), Is.EqualTo(150));
        Assert.That(settings.Capacity(24), Is.EqualTo(600));
        Assert.That(GrainEconomySettings.Default.ConsumeMultiplier, Is.EqualTo(900));
        Assert.That(settings.Capacity(0) * GrainEconomySettings.Default.ConsumeMultiplier,
            Is.EqualTo(7.5f * 200f * 1.5f));
    }

    [Test]
    public void ExhaustingStockCannotMintMoreCargoThanItsCollectibleMass()
    {
        economy.grainConsumeMultiplier = 200;
        pile.ConfigureRegions(new[] { new GrainRegion(100, GrainType.Barley) });
        double available = pile.CollectibleKilograms;
        var cargo = new GrainCargo();
        float collected = pile.Scoop(new Vector3(0, pile.SurfaceHeight(Vector3.zero), 0), 100, 10, 100000, cargo);
        Assert.That(collected, Is.EqualTo(available).Within(.002));
        Assert.That(cargo.Total, Is.EqualTo(available).Within(.002));
        Assert.That(pile.Kilograms, Is.LessThan(.01));
        Assert.That(pile.Scoop(Vector3.zero, 100, 10, 1000, cargo), Is.EqualTo(0));
    }
}
