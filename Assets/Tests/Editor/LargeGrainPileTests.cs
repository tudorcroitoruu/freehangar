using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class LargeGrainPileTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    GrainPile pile;
    T Get<T>(string name) => (T)typeof(GrainPile).GetField(name, Private).GetValue(pile);
    void Set(string name, object value) => typeof(GrainPile).GetField(name, Private).SetValue(pile, value);
    void Call(string name) => typeof(GrainPile).GetMethod(name, Private).Invoke(pile, null);

    [SetUp] public void Setup()
    {
        root = new GameObject("Large clipped pile regression"); root.SetActive(false);
        root.transform.position = Vector3.up * 100;
        pile = root.AddComponent<GrainPile>();
        Set("fitToWalls", false); Set("size", 24f); Set("initialHeight", 4f);
        // Fractional grid intersections exercise clipped-edge interpolation.
        Set("excludedFootprint", new Rect(3.17f, -7.13f, 4.21f, 14.27f));
        root.SetActive(true);
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

    [Test] public void ShovelKeepsScrapingItsPileSectionsButStillRejectsWalls()
    {
        var actor = new GameObject("Shovel actor");
        var wall = new GameObject("Obstruction");
        try
        {
            var tool = actor.AddComponent<ShovelTool>();
            var camera = actor.AddComponent<Camera>(); camera.enabled = false;
            actor.transform.position = root.transform.position + Vector3.up * 5;
            void SetTool(string name, object value) => typeof(ShovelTool).GetField(name, Private).SetValue(tool, value);
            SetTool("actionCamera", camera); SetTool("actor", actor.transform);
            SetTool("pendingPile", pile); SetTool("targetPoint", root.transform.position);
            // The stock drops before the smoothed skin/collider catches up.
            var field = Get<GrainHeightField>("field");
            field.Heights[field.Heights.Length / 2] -= .3f;
            Physics.SyncTransforms();
            bool CanScrape() => (bool)typeof(ShovelTool).GetMethod("CanScrapeTarget", Private).Invoke(tool, null);
            Assert.IsFalse(root.GetComponent<MeshCollider>().enabled);
            Assert.IsTrue(CanScrape(), "The pile's own collision sections must not block the rest of a scoop.");
            wall.transform.position = actor.transform.position - Vector3.up * .5f;
            wall.AddComponent<BoxCollider>().size = new Vector3(1, .1f, 1);
            Physics.SyncTransforms();
            Assert.IsFalse(CanScrape(), "Other obstacles must still block collection.");
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(wall); }
    }

    [Test] public void ShovelPoursEveryBatchIntoSectionedPile()
    {
        var actor = new GameObject("Pouring actor");
        try
        {
            var tool = actor.AddComponent<ShovelTool>();
            var camera = actor.AddComponent<Camera>(); camera.enabled = false;
            actor.transform.position = root.transform.position + Vector3.up * 6;
            void SetTool(string name, object value) => typeof(ShovelTool).GetField(name, Private).SetValue(tool, value);
            SetTool("actionCamera", camera); SetTool("actor", actor.transform);
            SetTool("dropReceiver", pile); SetTool("targetPoint", root.transform.position + Vector3.up * 4);
            SetTool("availableAtStart", 6f);
            var surface = Array.Find(root.GetComponents<MeshCollider>(), c => c.enabled);
            SetTool("dropSurface", surface);
            tool.Cargo.Add(GrainType.Corn, 6f);
            Physics.SyncTransforms();
            var commit = typeof(ShovelTool).GetMethod("CommitDrop", Private);
            for (int i = 0; i < 6; i++)
            {
                commit.Invoke(tool, new object[] { 1f });
                Assert.That(tool.Kilograms, Is.EqualTo(5f - i).Within(.0001f), "Pour batch " + i);
            }
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test] public void ClippedLocalUpdatesMatchFullRebuildIncludingNormalsAndSkirts()
    {
        var field = Get<GrainHeightField>("field");
        var visual = Get<GrainHeightField>("visualField");
        foreach (var position in new[] { Vector2.zero, new Vector2(3.1f, 0), new Vector2(7.5f, 1) })
        {
            field.Scoop(position.x, position.y, .55f, .07f, .04f);
            Array.Copy(field.Heights, visual.Heights, field.Heights.Length);
            bool local = (bool)typeof(GrainPile).GetMethod("TryRefreshLocalSurface", Private).Invoke(pile, new object[] { visual });
            Assert.IsTrue(local, "Clipped interior edits must use the local update path.");
            var mesh = Get<Mesh>("mesh");
            var vertices = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
            Set("detailOffsets", null); Set("renderedHeights", null);
            Call("RefreshSurface");
            CollectionAssert.AreEqual(triangles, mesh.triangles);
            var expectedVertices = mesh.vertices; var expectedNormals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(Vector3.Distance(vertices[i], expectedVertices[i]), Is.LessThan(.00002f), "Vertex " + i);
                Assert.That(Vector3.Distance(normals[i], expectedNormals[i]), Is.LessThan(.001f), "Normal " + i);
            }
        }
    }

    [Test] public void CollisionSectionsPreserveRayHitsAcrossScoopClearAndRefill()
    {
        Assert.Greater(Get<IList>("collisionChunks").Count, 1);
        var field = Get<GrainHeightField>("field");
        var original = (float[])field.Heights.Clone();
        var reference = new GameObject("Reference skin collision");
        reference.transform.position = root.transform.position;
        var expected = reference.AddComponent<MeshCollider>();
        var referenceMesh = new Mesh();
        try
        {
            for (int phase = 0; phase < 4; phase++)
            {
                if (phase == 1) field.Scoop(0, 0, 1f, .4f, 2f);
                if (phase == 2)
                    for (int z = 55; z < 65; z++) for (int x = 55; x < 65; x++) field.Heights[z * field.Resolution + x] = 0;
                if (phase == 3) Array.Copy(original, field.Heights, original.Length);
                Call("RefreshSurface"); Call("RefreshCollider");
                expected.sharedMesh = null;
                referenceMesh.Clear();
                referenceMesh.SetVertices(Get<List<Vector3>>("collisionVertices"));
                referenceMesh.SetTriangles(Get<List<int>>("collisionIndices"), 0);
                expected.sharedMesh = referenceMesh;
                Physics.SyncTransforms();
                var colliders = root.GetComponents<MeshCollider>();
                for (float z = -9.937f; z < 10; z += .713f)
                for (float x = -9.913f; x < 10; x += .731f)
                {
                    var ray = new Ray(root.transform.position + new Vector3(x, 10, z), Vector3.down);
                    bool hit = expected.Raycast(ray, out var expectedHit, 20);
                    float nearest = float.PositiveInfinity;
                    foreach (var collider in colliders)
                        if (collider.enabled && collider.Raycast(ray, out var actual, 20)) nearest = Mathf.Min(nearest, actual.distance);
                    Assert.AreEqual(hit, !float.IsPositiveInfinity(nearest), $"Coverage phase={phase} at {x},{z}");
                    if (hit) Assert.That(nearest, Is.EqualTo(expectedHit.distance).Within(.002f));
                }
            }
        }
        finally { Object.DestroyImmediate(reference); Object.DestroyImmediate(referenceMesh); }
    }
}



