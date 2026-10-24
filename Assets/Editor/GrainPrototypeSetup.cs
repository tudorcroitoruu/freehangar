using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates an isolated, undoable prototype using the project's existing player and sack.</summary>
public static class GrainPrototypeSetup
{
    private const string Folder = "Assets/GrainPrototype";

    [MenuItem("Tools/Grain/Create playable prototype")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play Mode before authoring.");
        if (GameObject.Find("Grain Prototype") != null) throw new InvalidOperationException("Grain Prototype already exists.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new InvalidOperationException("Open a scene containing PlayerController.");
        var playerData = new SerializedObject(player);
        var hands = playerData.FindProperty("heldInstrument").objectReferenceValue as Transform;
        if (hands == null) throw new InvalidOperationException("Assign the player's held instrument first.");
        var shovelMesh = hands.GetComponentInChildren<MeshFilter>();
        if (shovelMesh == null) throw new InvalidOperationException("Held shovel mesh is missing.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Material material = CreateGrainMaterial();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create grain prototype");
        var root = new GameObject("Grain Prototype");
        Undo.RegisterCreatedObjectUndo(root, "Create grain prototype");
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        var pile = new GameObject("Corn pile - scoop with left click", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        pile.transform.SetParent(root.transform);
        Vector3 position = player.transform.position + forward * 4.4f + right * 3f;
        position.y = GroundHeight(position, player.transform.position.y) + .015f;
        pile.transform.position = position;
        pile.AddComponent<GrainPile>();
        pile.GetComponent<MeshRenderer>().sharedMaterial = material;
        var field = new GrainHeightField(33, 6, 1.8f);
        Mesh preview = CreateSurface(field);
        AssetDatabase.CreateAsset(preview, AssetDatabase.GenerateUniqueAssetPath(Folder + "/PilePreview.asset"));
        pile.GetComponent<MeshFilter>().sharedMesh = preview;
        pile.GetComponent<MeshCollider>().sharedMesh = preview;

        var sackAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Bag/Sack.fbx");
        if (sackAsset == null) throw new InvalidOperationException("Sack.fbx is missing.");
        var sackObject = (GameObject)PrefabUtility.InstantiatePrefab(sackAsset);
        Undo.RegisterCreatedObjectUndo(sackObject, "Create test sack");
        sackObject.name = "50 kg sack - fill with loaded shovel";
        sackObject.transform.SetParent(root.transform);
        Vector3 sackPosition = player.transform.position + forward * 1.65f - right * .9f;
        sackPosition.y = GroundHeight(sackPosition, player.transform.position.y);
        sackObject.transform.position = sackPosition;
        var sack = sackObject.GetComponent<SackController>();
        if (sack == null) sack = Undo.AddComponent<SackController>(sackObject);
        sack.Fill = 0;
        sack.Close = 0;
        var container = Undo.AddComponent<GrainContainer>(sackObject);
        var containerData = new SerializedObject(container);
        containerData.FindProperty("sack").objectReferenceValue = sack;
        containerData.ApplyModifiedProperties();
        var box = Undo.AddComponent<BoxCollider>(sackObject);
        // Skinned renderer bounds deliberately cover all morphs; use the imported mesh for the trigger target.
        Bounds localBounds = new Bounds(Vector3.zero, new Vector3(.8f, 1f, .8f));
        box.center = new Vector3(0, .45f, 0);
        box.size = localBounds.size;

        var tool = hands.GetComponent<ShovelTool>();
        if (tool == null) tool = Undo.AddComponent<ShovelTool>(hands.gameObject);
        var tip = new GameObject("Shovel contact point").transform;
        Undo.RegisterCreatedObjectUndo(tip.gameObject, "Add shovel contact");
        tip.SetParent(shovelMesh.transform, false);
        tip.localPosition = new Vector3(0, -.03f, shovelMesh.sharedMesh.bounds.min.z);
        var load = new GameObject("Shovel grain load", typeof(MeshFilter), typeof(MeshRenderer));
        Undo.RegisterCreatedObjectUndo(load, "Add shovel load");
        load.transform.SetParent(shovelMesh.transform, false);
        // The imported shovel's bowl opens along local -Y.
        load.transform.localPosition = new Vector3(0, -.025f, .1f);
        load.transform.localRotation = Quaternion.Euler(180f, 0, 0);
        var loadField = new GrainHeightField(17, 1f, .25f);
        Mesh loadMesh = CreateSurface(loadField);
        var loadUV = loadMesh.uv;
        for (int i = 0; i < loadUV.Length; i++) loadUV[i] = Vector2.Scale(loadUV[i], new Vector2(.34f, .38f));
        loadMesh.uv = loadUV;
        AssetDatabase.CreateAsset(loadMesh, AssetDatabase.GenerateUniqueAssetPath(Folder + "/ShovelLoad.asset"));
        load.GetComponent<MeshFilter>().sharedMesh = loadMesh;
        load.GetComponent<MeshRenderer>().sharedMaterial = material;
        load.transform.localScale = new Vector3(.34f, .4f, .38f);
        load.SetActive(false);
        var toolData = new SerializedObject(tool);
        toolData.FindProperty("bladeTip").objectReferenceValue = tip;
        toolData.FindProperty("loadVisual").objectReferenceValue = load.transform;
        toolData.ApplyModifiedProperties();
        playerData.FindProperty("shovelTool").objectReferenceValue = tool;
        playerData.ApplyModifiedProperties();
        CornPresentationSetup.Upgrade();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = pile;
    }

    private static float GroundHeight(Vector3 position, float fallback)
    {
        return Physics.Raycast(position + Vector3.up * 2, Vector3.down, out RaycastHit hit, 5f,
            ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
    }

    private static Mesh CreateSurface(GrainHeightField field)
    {
        var vertices = new Vector3[field.Heights.Length];
        var uv = new Vector2[vertices.Length];
        for (int z = 0; z < field.Resolution; z++)
        for (int x = 0; x < field.Resolution; x++)
        {
            int i = z * field.Resolution + x;
            vertices[i] = new Vector3(x * field.Spacing - field.Size * .5f, field.Heights[i], z * field.Spacing - field.Size * .5f);
            uv[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        var mesh = new Mesh { name = "Grain surface" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        var triangles = new System.Collections.Generic.List<int>();
        for (int t = 0; t < field.Triangles.Length; t += 3)
        {
            int a = field.Triangles[t], b = field.Triangles[t + 1], c = field.Triangles[t + 2];
            if (field.Heights[a] <= .00001f && field.Heights[b] <= .00001f && field.Heights[c] <= .00001f) continue;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateGrainMaterial()
    {
        string materialPath = Folder + "/Corn.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (existing != null) return existing;
        const int pixels = 256;
        var albedo = new Texture2D(pixels, pixels, TextureFormat.RGB24, true);
        var normal = new Texture2D(pixels, pixels, TextureFormat.RGB24, true, true);
        // A seamless staggered kernel pattern: decorative shading, never the source of physics.
        for (int y = 0; y < pixels; y++)
        for (int x = 0; x < pixels; x++)
        {
            int row = y / 32;
            float u = ((x + (row % 2) * 16) % 32 - 15.5f) / 15f;
            float v = (y % 32 - 15.5f) / 16f;
            float r = u * u + v * v;
            float h = Mathf.Sqrt(Mathf.Max(0, 1f - r));
            float tint = .9f + .1f * Mathf.Sin(row * 23f + (x + (row % 2) * 16) / 32 * 7f);
            albedo.SetPixel(x, y, Color.Lerp(new Color(.29f, .12f, .015f), new Color(1f, .64f, .055f) * tint, Mathf.Clamp01(h * 2f)));
            Vector3 n = r < 1 ? new Vector3(u * .65f, v * .65f, Mathf.Max(.15f, h)).normalized : Vector3.forward;
            normal.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f));
        }
        File.WriteAllBytes(Folder + "/CornColor.png", albedo.EncodeToPNG());
        File.WriteAllBytes(Folder + "/CornNormal.png", normal.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(albedo);
        UnityEngine.Object.DestroyImmediate(normal);
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/CornNormal.png");
        importer.textureType = TextureImporterType.NormalMap;
        importer.SaveAndReimport();
        Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Corn" };
        Texture colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/CornColor.png");
        Texture normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/CornNormal.png");
        foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
            if (material.HasProperty(property)) { material.SetTexture(property, colorMap); material.SetTextureScale(property, Vector2.one * 4f); }
        foreach (string property in new[] { "_NormalMap", "_BumpMap" })
            if (material.HasProperty(property)) { material.SetTexture(property, normalMap); material.SetTextureScale(property, Vector2.one * 4f); }
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }
}
