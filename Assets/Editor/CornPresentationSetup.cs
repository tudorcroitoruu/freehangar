using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CornPresentationSetup
{
    [MenuItem("Tools/Grain/Upgrade to 3D kernels and grain motion")]
    public static void Upgrade()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play Mode first.");
        const string folder = "Assets/GrainPrototype";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/CornKernel.asset");
        if (mesh == null) { mesh = CreateKernel(); AssetDatabase.CreateAsset(mesh, folder + "/CornKernel.asset"); }
        else
        {
            var replacement = CreateKernel();
            EditorUtility.CopySerialized(replacement, mesh);
            Object.DestroyImmediate(replacement);
            EditorUtility.SetDirty(mesh);
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/CornKernel.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit")) { name = "Golden corn kernels", enableInstancing = true };
            material.SetColor("_BaseColor", new Color(1f, .68f, .16f));
            material.SetFloat("_Smoothness", .3f);
            AssetDatabase.CreateAsset(material, folder + "/CornKernel.mat");
        }
        material.enableInstancing = true;
        material.SetColor("_BaseColor", new Color(.70f, .55f, .23f));
        material.SetFloat("_Smoothness", .22f);
        EditorUtility.SetDirty(material);
        var palette = new Material[3];
        palette[0] = material;
        for (int i = 1; i < palette.Length; i++)
        {
            string path = folder + "/CornKernelTone" + i + ".mat";
            palette[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (palette[i] == null) { palette[i] = new Material(material); AssetDatabase.CreateAsset(palette[i], path); }
            palette[i].CopyPropertiesFromMaterial(material);
            palette[i].SetColor("_BaseColor", i == 1 ? new Color(.62f, .47f, .18f) : new Color(.79f, .63f, .30f));
            EditorUtility.SetDirty(palette[i]);
        }
        var substrate = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Corn.mat");
        if (substrate != null)
        {
            Undo.RecordObject(substrate, "Remove flat corn pattern");
            substrate.SetTexture("_BaseColorMap", null); substrate.SetTexture("_NormalMap", null);
            substrate.DisableKeyword("_NORMALMAP");
            substrate.SetColor("_BaseColor", new Color(.62f, .47f, .18f));
            substrate.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(substrate);
        }
        foreach (var pile in Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
        {
            var detail = pile.GetComponent<CornSurfaceKernels>();
            if (detail == null) detail = Undo.AddComponent<CornSurfaceKernels>(pile.gameObject);
            var data = new SerializedObject(detail);
            data.FindProperty("pile").objectReferenceValue = pile;
            data.FindProperty("kernelMesh").objectReferenceValue = mesh;
            data.FindProperty("kernelMaterial").objectReferenceValue = material;
            data.FindProperty("spacing").floatValue = .0315f;
            var colors = data.FindProperty("colorVariants");
            colors.arraySize = palette.Length;
            for (int i = 0; i < palette.Length; i++) colors.GetArrayElementAtIndex(i).objectReferenceValue = palette[i];
            data.ApplyModifiedProperties();
            detail.Rebuild();
            // Recess the saved editor preview too; keep its collider at the physical surface.
            var filter = pile.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null)
            {
                var preview = Object.Instantiate(filter.sharedMesh);
                var points = preview.vertices;
                for (int i = 0; i < points.Length; i++) points[i].y = pile.SampleLocalHeight(points[i].x, points[i].z) - .045f;
                preview.vertices = points; preview.RecalculateNormals(); preview.RecalculateBounds();
                string path = folder + "/RecessedPilePreview.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { saved = preview; AssetDatabase.CreateAsset(saved, path); }
                else { EditorUtility.CopySerialized(preview, saved); Object.DestroyImmediate(preview); EditorUtility.SetDirty(saved); }
                Undo.RecordObject(filter, "Recess grain visual skin");
                filter.sharedMesh = saved;
            }
        }
        foreach (var tool in Object.FindObjectsByType<ShovelTool>(FindObjectsSortMode.None))
        {
            var data = new SerializedObject(tool);
            var tip = (Transform)data.FindProperty("bladeTip").objectReferenceValue;
            var grip = tip.parent.Find("Shovel grip anchor");
            if (grip == null)
            {
                grip = new GameObject("Shovel grip anchor").transform;
                Undo.RegisterCreatedObjectUndo(grip.gameObject, "Anchor shovel handle");
                grip.SetParent(tip.parent, false);
            }
            grip.localPosition = new Vector3(0, 0, .96f);
            data.FindProperty("gripAnchor").objectReferenceValue = grip;
            data.FindProperty("maximumGripTravel").floatValue = .16f;
            var legacy = (Transform)data.FindProperty("loadVisual").objectReferenceValue;
            if (legacy != null) { Undo.RecordObject(legacy.gameObject, "Hide old grain sheet"); legacy.gameObject.SetActive(false); }
            var bowl = tip.parent.Find("Simulated corn in bowl");
            if (bowl == null)
            {
                bowl = new GameObject("Simulated corn in bowl").transform;
                Undo.RegisterCreatedObjectUndo(bowl.gameObject, "Add simulated corn");
                bowl.SetParent(tip.parent, false);
            }
            bowl.localPosition = new Vector3(0, -.02f, .1f);
            bowl.localRotation = Quaternion.Euler(180, 0, 0);
            bowl.localScale = Vector3.one;
            var physics = bowl.GetComponent<ShovelGrainPhysics>();
            if (physics == null) physics = Undo.AddComponent<ShovelGrainPhysics>(bowl.gameObject);
            var simulation = new SerializedObject(physics);
            simulation.FindProperty("kernelMesh").objectReferenceValue = mesh;
            simulation.FindProperty("kernelMaterial").objectReferenceValue = material;
            var floor = simulation.FindProperty("bowlFloor");
            floor.arraySize = 289;
            // Bake the imported metal interior once. Runtime particles only sample this grid.
            var measuring = new GameObject("Temporary shovel interior measurement");
            try
            {
                var collider = measuring.AddComponent<MeshCollider>();
                collider.sharedMesh = tip.parent.GetComponent<MeshFilter>().sharedMesh;
                for (int z = 0; z < 17; z++)
                for (int x = 0; x < 17; x++)
                {
                    Vector3 local = new Vector3(-.2f + x * .4f / 16f, .3f, -.18f + z * .37f / 16f);
                    var ray = new Ray(bowl.localPosition + bowl.localRotation * local, bowl.localRotation * Vector3.down);
                    float height = .12f;
                    if (collider.Raycast(ray, out RaycastHit hit, .6f))
                        height = (Quaternion.Inverse(bowl.localRotation) * (hit.point - bowl.localPosition)).y;
                    floor.GetArrayElementAtIndex(z * 17 + x).floatValue = height;
                }
            }
            finally { Object.DestroyImmediate(measuring); }
            simulation.ApplyModifiedProperties();
            data.FindProperty("grainPhysics").objectReferenceValue = physics;
            data.FindProperty("actionDuration").floatValue = 1.25f;
            data.ApplyModifiedProperties();
        }
        // The prominent imported sack in this project's composition should accept the visible stream too.
        var mainSack = GameObject.Find("Sack");
        if (mainSack != null && mainSack.GetComponentInChildren<SkinnedMeshRenderer>() != null)
        {
            var sack = mainSack.GetComponent<SackController>();
            if (sack == null) sack = Undo.AddComponent<SackController>(mainSack);
            var container = mainSack.GetComponent<GrainContainer>();
            if (container == null) container = Undo.AddComponent<GrainContainer>(mainSack);
            var data = new SerializedObject(container);
            data.FindProperty("sack").objectReferenceValue = sack;
            data.ApplyModifiedProperties();
            if (mainSack.GetComponent<Collider>() == null)
            {
                var collider = Undo.AddComponent<BoxCollider>(mainSack);
                collider.center = new Vector3(0, .45f, 0);
                collider.size = new Vector3(.7f, .87f, .6f);
            }
        }
        foreach (var container in Object.FindObjectsByType<GrainContainer>(FindObjectsSortMode.None)) FitSackCollider(container);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void FitSackCollider(GrainContainer container)
    {
        SkinnedMeshRenderer body = null;
        foreach (var renderer in container.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (renderer.name == "Sack_Body") body = renderer;
        if (body == null) return;
        int index = body.sharedMesh.GetBlendShapeIndex("Fill");
        if (index < 0) return;
        var collider = container.GetComponent<BoxCollider>();
        if (collider == null) collider = Undo.AddComponent<BoxCollider>(container.gameObject);
        float original = body.GetBlendShapeWeight(index);
        var baked = new Mesh();
        try
        {
            body.SetBlendShapeWeight(index, 0); body.BakeMesh(baked); baked.RecalculateBounds();
            Bounds empty = InContainerSpace(baked.bounds, body.transform, container.transform);
            body.SetBlendShapeWeight(index, 100); body.BakeMesh(baked); baked.RecalculateBounds();
            Bounds full = InContainerSpace(baked.bounds, body.transform, container.transform);
            var data = new SerializedObject(container);
            data.FindProperty("fitFillCollider").boolValue = true;
            data.FindProperty("emptyBounds").boundsValue = empty;
            data.FindProperty("fullBounds").boundsValue = full;
            data.ApplyModifiedProperties();
            Undo.RecordObject(collider, "Fit rotated sack collider");
            collider.center = Vector3.Lerp(empty.center, full.center, container.Kilograms / container.CapacityKg);
            collider.size = Vector3.Lerp(empty.size, full.size, container.Kilograms / container.CapacityKg);
        }
        finally { body.SetBlendShapeWeight(index, original); Object.DestroyImmediate(baked); }
    }

    private static Bounds InContainerSpace(Bounds source, Transform renderer, Transform container)
    {
        Bounds result = default;
        for (int i = 0; i < 8; i++)
        {
            var corner = source.center + Vector3.Scale(source.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 point = container.InverseTransformPoint(renderer.TransformPoint(corner));
            if (i == 0) result = new Bounds(point, Vector3.zero); else result.Encapsulate(point);
        }
        return result;
    }

    private static Mesh CreateKernel()
    {
        const int segments = 8, rings = 4;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        vertices.Add(Vector3.up);
        for (int r = 1; r <= rings; r++)
        {
            float latitude = Mathf.PI * r / (rings + 1);
            for (int s = 0; s < segments; s++)
            {
                float angle = 2 * Mathf.PI * s / segments;
                float radius = Mathf.Sin(latitude);
                // A broad rounded crown and a narrower germ end, with a flatter face.
                // The outline reads as a corn kernel instead of a stretched sphere.
                float x = Mathf.Sign(Mathf.Cos(angle)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle)), .9f);
                float z = Mathf.Sign(Mathf.Sin(angle)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(angle)), .9f);
                float taper = Mathf.Lerp(.66f, 1f, (z + 1f) * .5f);
                vertices.Add(new Vector3(x * radius * taper, Mathf.Cos(latitude), z * radius));
            }
        }
        int bottom = vertices.Count;
        vertices.Add(Vector3.down);
        for (int s = 0; s < segments; s++)
        {
            int next = (s + 1) % segments;
            triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + s);
            for (int r = 0; r < rings - 1; r++)
            {
                int a = 1 + r * segments + s, b = 1 + r * segments + next;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + segments);
                triangles.Add(b); triangles.Add(b + segments); triangles.Add(a + segments);
            }
            triangles.Add(bottom); triangles.Add(1 + (rings - 1) * segments + s); triangles.Add(1 + (rings - 1) * segments + next);
        }
        var mesh = new Mesh { name = "Rounded corn kernel (64 triangles)" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
