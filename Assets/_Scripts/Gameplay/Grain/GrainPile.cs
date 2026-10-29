using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public sealed class GrainPile : MonoBehaviour
{
    [Header("Spatial grain regions (percent of starting mass)")]
    [SerializeField] private bool useSpatialLayout;
    [SerializeField] private GrainSpatialField.Arrangement arrangement;
    [SerializeField, Range(0,.5f)] private float regionWaviness = .12f;
    [Tooltip("Curved Sections junction in normalized footprint coordinates (0..1).")]
    [SerializeField] private Vector2 sectionCenter = new Vector2(.55f, .48f);
    [SerializeField, Range(-180,180)] private float sectionRotation;
    [SerializeField] private int grainLayoutSeed = 731;
    [SerializeField] private GrainRegion[] regions = { new GrainRegion(100, GrainType.Corn) };
    [SerializeField, Range(0,100), Tooltip("Edit Mode only. Clear this percentage from the front of the region sequence to inspect later crops. Play Mode always starts intact.")]
    private float previewClearedPercent;
    private GrainSpatialField spatial;
    public bool HasSpatialLayout => useSpatialLayout;
    public double GrainKilograms(GrainType grain) => spatial == null ? 0 : spatial.VolumeOf(grain) * kilogramsPerCubicMetre * VolumeScale;
    public void ConfigureRegions(GrainRegion[] value, GrainSpatialField.Arrangement pattern = GrainSpatialField.Arrangement.FrontToBack, float waviness = .12f)
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Author spatial stock outside Play Mode; changing it would replace stored grain.");
        useSpatialLayout = true; regions = value; arrangement = pattern; regionWaviness = waviness;
        previewClearedPercent = 0; RebuildPreview();
    }
    public void ApplySpatialAppearance(MaterialPropertyBlock block)
    {
        if (useSpatialLayout && spatial != null) spatial.Bind(block, transform);
        else block.SetFloat("_UseSpatialGrain", 0);
    }
    public void SampleSurfaceGrain(Vector3 worldPoint, GrainCargo output)
    {
        Initialize();
        if (spatial != null)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            spatial.Sample(local.x, local.z, output);
        }
        else { output.Clear(); CollectRecipe(Application.isPlaying ? harvestedKg : previewHarvestedKg, 1, output); }
    }
    [Header("Grain recipe — top to bottom")]
    [SerializeField] private GrainLayer[] grainLayers = { new GrainLayer(52, GrainType.Corn) };
    [SerializeField, Min(0), Tooltip("Editor only: preview the recipe after collecting this many kg. Does not excavate or change gameplay mass.")]
    private float previewHarvestedKg;
    private double harvestedKg;
    private readonly GrainCargo surfaceGrain = new GrainCargo();
    private readonly GrainAppearance appearance = new GrainAppearance();
    public double HarvestedKg => harvestedKg;
    public GrainCargo SurfaceGrain { get { RefreshGrainAppearance(); return surfaceGrain; } }
    private double lastAppearanceKg = -1;
    private MeshRenderer surfaceRenderer;

    public void SetRecipe(GrainLayer[] layers)
    {
        if (layers == null || layers.Length == 0) return;
        grainLayers = layers;
        harvestedKg = 0;
        lastAppearanceKg = -1;
        SurfaceRevision++;
        RefreshGrainAppearance();
    }

    public void CollectRecipe(double offsetKg, float amountKg, GrainCargo destination)
    {
        if (destination == null || !float.IsFinite(amountKg) || amountKg <= 0) return;
        if (grainLayers == null || grainLayers.Length == 0) { destination.Add(GrainType.Corn, amountKg); return; }
        double offset = System.Math.Max(0, offsetKg);
        float remaining = amountKg;
        for (int i = 0; i < grainLayers.Length && remaining > 0; i++)
        {
            var layer = grainLayers[i];
            float limit = layer != null && float.IsFinite(layer.kilograms) ? Mathf.Max(.01f, layer.kilograms) : 52;
            bool last = i == grainLayers.Length - 1;
            if (!last && offset >= limit) { offset -= limit; continue; }
            float part = last ? remaining : Mathf.Min(remaining, (float)(limit - offset));
            if (layer?.mixture != null) layer.mixture.AddTo(destination, part); else destination.Add(GrainType.Corn, part);
            remaining -= part;
            offset = 0;
        }
    }

    private void RefreshGrainAppearance()
    {
        double offset = Application.isPlaying ? harvestedKg : previewHarvestedKg;
        if (lastAppearanceKg == offset && spatial == null) return;
        lastAppearanceKg = offset;
        surfaceGrain.Clear();
        // Show a one-kilogram transition at layer boundaries, also in the substrate shader.
        if (spatial != null) spatial.Sample(0, 0, surfaceGrain); else CollectRecipe(offset, 1, surfaceGrain);
        appearance.Set(surfaceGrain);
        ApplySpatialAppearance(appearance.Block);
        if (surfaceRenderer == null) surfaceRenderer = GetComponent<MeshRenderer>();
        if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(appearance.Block);
    }
    [SerializeField, Range(9, 65)] private int resolution = 33;
    [SerializeField, Min(1f)] private float size = 6f;
    [SerializeField, Min(0f)] private float initialHeight = 1.8f;
    public enum PileShape { Cone, UnevenMounds, WallBank }
    [System.Serializable]
    public struct Mound
    {
        public Vector2 center;
        public Vector2 radius;
        [Min(0)] public float height;
        public Mound(Vector2 center, Vector2 radius, float height) { this.center = center; this.radius = radius; this.height = height; }
    }
    [SerializeField] private PileShape shape;
    [SerializeField, Range(0, .5f)] private float unevenness = .16f;
    [SerializeField] private int shapeSeed = 731;
    [Tooltip("Mound positions/radii use normalized coordinates: -1 to 1 spans the pile footprint. Heights multiply Initial Height.")]
    [SerializeField] private Mound[] mounds = {
        new Mound(new Vector2(-.4f, .18f), new Vector2(.57f, .7f), .85f),
        new Mound(new Vector2(.27f, .36f), new Vector2(.64f, .59f), 1f),
        new Mound(new Vector2(.18f, -.36f), new Vector2(.6f, .53f), .62f)
    };
    [SerializeField, Min(1f)] private float kilogramsPerCubicMetre = 720f;
    [SerializeField, Range(5f, 60f)] private float reposeAngle = 32f;
    [SerializeField, Min(0f)] private float settlingMargin = .45f;
    [SerializeField, Range(1f, 30f)] private float simulationRate = 15f;
    [SerializeField, Range(1f, 15f)] private float colliderRate = 5f;
    [Header("Wall fitting")]
    [SerializeField] private bool fitToWalls = true;
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField, Range(.002f, .1f)] private float wallClearance = .01f;
    private GrainWallBoundary boundary;
    private readonly List<Plane> detectedWalls = new List<Plane>(12);
    private readonly RaycastHit[] wallHits = new RaycastHit[128];
    private Matrix4x4 lastPlacement;
    public int DetectedWallCount => boundary == null ? 0 : boundary.Walls.Count;
    public bool IsInsideBoundary(Vector3 worldPoint, float radius = 0) => boundary == null || boundary.Contains(worldPoint, wallClearance + radius);
    private GrainHeightField field;
    internal GrainHeightField HeightField => field;
    private Mesh mesh;
    private Mesh collisionMesh;
    private MeshCollider meshCollider;
    private Vector3[] vertices;
    private List<int> activeTriangles;
    private bool settling;
    private bool colliderDirty;
    private float nextStep, nextCollider;
    private bool rebuildRequested;
    private Vector3 lastScale;
    public PileShape Shape => shape;
    public Vector3 WorldScale => transform.lossyScale;
    public int SurfaceRevision { get; private set; }
    public float Size => size;
    public float SampleLocalHeight(float x, float z, bool respectWalls = true)
    {
        Initialize();
        if (respectWalls && !IsInsideBoundary(transform.TransformPoint(new Vector3(x, 0, z)))) return 0;
        return field == null ? 0 : field.Sample(x, z);
    }

    private float VolumeScale => Mathf.Abs(transform.localToWorldMatrix.determinant);
    public double Kilograms => field == null ? 0 : field.Volume * kilogramsPerCubicMetre * VolumeScale;
    public float SurfaceHeight(Vector3 worldPoint)
    {
        Initialize();
        if (field == null) return worldPoint.y;
        if (!IsInsideBoundary(worldPoint)) return transform.position.y;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        return transform.TransformPoint(new Vector3(p.x, field.Sample(p.x, p.z), p.z)).y;
    }

    /// <summary>Find a real grain surface near the blade, rather than the distant aim point.</summary>
    public bool TryNearestSurface(Vector3 worldPoint, float maximumDistance, out Vector3 surface)
    {
        Initialize();
        surface = default;
        if (field == null || !isActiveAndEnabled) return false;
        float best = maximumDistance * maximumDistance;
        bool found = false;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float heightScale = WorldScale.y;
        // Include the point directly below the blade, then the height-field vertices.
        for (int i = -1; i < field.Heights.Length; i++)
        {
            float x = i < 0 ? local.x : (i % field.Resolution) * field.Spacing - field.Size * .5f;
            float z = i < 0 ? local.z : (i / field.Resolution) * field.Spacing - field.Size * .5f;
            if (Mathf.Abs(x) > field.Size * .5f || Mathf.Abs(z) > field.Size * .5f) continue;
            float height = i < 0 ? field.Sample(x, z) : field.Heights[i];
            if (height * heightScale < .000001f) continue;
            Vector3 candidate = transform.TransformPoint(new Vector3(x, height, z));
            float distance = (candidate - worldPoint).sqrMagnitude;
            if (distance >= best || !IsInsideBoundary(candidate, .025f)) continue;
            surface = candidate; best = distance; found = true;
        }
        return found;
    }

    private void Awake() => Initialize();
    private void OnEnable() => Initialize();
    private void OnDisable() => spatial?.ReleaseTextures();

    private float Profile(float x, float z)
    {
        float u = x / (size * .5f), v = z / (size * .5f);
        float h = 0;
        if (mounds != null) foreach (var mound in mounds)
        {
            float dx = (u - mound.center.x) / Mathf.Max(.05f, mound.radius.x);
            float dz = (v - mound.center.y) / Mathf.Max(.05f, mound.radius.y);
            float dome = Mathf.Max(0, 1f - dx * dx - dz * dz);
            h = Mathf.Max(h, mound.height * dome * dome);
        }
        if (shape == PileShape.WallBank)
        {
            float toe = -.62f + .18f * Mathf.Sin(u * 5f + shapeSeed * .13f);
            float bank = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(toe, .9f, v));
            h = Mathf.Max(h * .72f, bank * (.76f + .18f * Mathf.Sin(u * 4f + .6f)));
        }
        float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .16f, 1f - Mathf.Abs(u))) *
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .16f, shape == PileShape.WallBank ? v + 1f : 1f - Mathf.Abs(v)));
        float noise = Mathf.PerlinNoise(u * 2.7f + shapeSeed * .137f, v * 2.7f + shapeSeed * .219f);
        return initialHeight * h * edge * (1f + (noise * 2f - 1f) * unevenness);
    }

    public void Initialize()
    {
        if (field != null) return;
        Vector3 scale = WorldScale;
        // A height field can bank against a wall, but cannot hang vertically or represent overhangs.
        if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0 || Vector3.Dot(transform.up, Vector3.up) < .9999f)
        {
            return;
        }
        int grid = Mathf.Clamp(Mathf.Max(resolution, Mathf.CeilToInt(size * Mathf.Max(scale.x, scale.z) / .2f) + 1), 9, 129);
        field = new GrainHeightField(grid, size, initialHeight, shape == PileShape.Cone ? null : Profile);
        DetectWalls();
        boundary = new GrainWallBoundary();
        if (detectedWalls.Count > 0) boundary.Build(field, transform, detectedWalls, wallClearance);
        if (useSpatialLayout)
        {
            spatial = new GrainSpatialField(field, regions, arrangement, regionWaviness, grainLayoutSeed, sectionCenter, sectionRotation);
            if (!Application.isPlaying && previewClearedPercent > 0) spatial.PreviewClear(previewClearedPercent);
        }
        lastPlacement = transform.localToWorldMatrix;
        lastScale = scale;
        activeTriangles = new List<int>(field.Triangles.Length);
        vertices = new Vector3[field.Heights.Length];
        var uv = new Vector2[vertices.Length];
        for (int z = 0; z < field.Resolution; z++)
        for (int x = 0; x < field.Resolution; x++)
        {
            int i = z * field.Resolution + x;
            vertices[i] = new Vector3(x * field.Spacing - field.Size * .5f, field.Heights[i], z * field.Spacing - field.Size * .5f);
            uv[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        mesh = new Mesh { name = "Grain surface (generated)", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = field.Triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        collisionMesh = Instantiate(mesh);
        collisionMesh.hideFlags = HideFlags.DontSave;
        collisionMesh.name = "Grain collision (runtime)";
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = collisionMesh;
        RefreshSurface();
        RefreshCollider();
        settling = false;
        rebuildRequested = false;
    }

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg) => Scoop(worldPoint, radius, depth, capacityKg, null);

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg, GrainCargo destination)
    {
        if (!isActiveAndEnabled || !float.IsFinite(capacityKg) || !(capacityKg > 0f)) return 0;
        Initialize();
        if (field == null || !IsInsideBoundary(worldPoint)) return 0;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        // Reject a stale hit well above/below the actual surface.
        Vector3 scale = WorldScale;
        if (Mathf.Abs(p.y - field.Sample(p.x, p.z)) * scale.y > depth + .15f) return 0;
        float density = kilogramsPerCubicMetre * VolumeScale;
        if (spatial != null) spatial.BeginCollection(destination, density);
        float removed;
        try { removed = field.Scoop(p.x, p.z, radius, depth / scale.y, capacityKg / density, settlingMargin, scale); }
        finally { if (spatial != null) spatial.EndCollection(); }
        if (removed > 0)
        {
            float kg = removed * density;
            if (destination != null && spatial == null) CollectRecipe(harvestedKg, kg, destination);
            harvestedKg += kg;
            RefreshGrainAppearance();
            settling = true;
            RefreshSurface();
            if (Time.time >= nextCollider) RefreshCollider();
        }
        return removed * density;
    }

    /// <summary>Adds typed material locally without replacing the existing stock.</summary>
    public bool Deposit(Vector3 worldPoint, GrainCargo cargo, float spreadRadius = 0)
    {
        Initialize();
        if (field == null || cargo == null || cargo.Total <= 0 || !IsInsideBoundary(worldPoint)) return false;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(local.x) > size * .5f || Mathf.Abs(local.z) > size * .5f) return false;
        var weights = new float[field.Heights.Length];
        double sum = 0;
        float radius = Mathf.Max(spreadRadius, Mathf.Max(.25f, field.Spacing * Mathf.Max(WorldScale.x, WorldScale.z) * 1.5f));
        for (int i = 0; i < weights.Length; i++)
        {
            Vector3 point = new Vector3(i % field.Resolution * field.Spacing - size * .5f, 0,
                i / field.Resolution * field.Spacing - size * .5f);
            if (field.AreaAt(i) <= 0 || !IsInsideBoundary(transform.TransformPoint(point))) continue;
            float distance = Vector3.Scale(point - new Vector3(local.x, 0, local.z), WorldScale).magnitude;
            weights[i] = Mathf.Max(0, 1 - distance / radius);
            sum += weights[i] * field.AreaAt(i);
        }
        if (sum <= 0) return false;
        if (spatial == null)
        {
            // Convert the remaining legacy recipe into local strata before covering it.
            var layers = new List<GrainStratum>();
            double skip = harvestedKg, remaining = Kilograms;
            foreach (var layer in grainLayers)
            {
                double amount = layer == grainLayers[grainLayers.Length - 1] ? remaining : System.Math.Min(remaining, System.Math.Max(0, layer.kilograms - skip));
                skip = System.Math.Max(0, skip - layer.kilograms);
                if (amount <= 0) continue;
                layers.Add(new GrainStratum { percent = (float)amount, mixture = layer.mixture });
                remaining -= amount;
            }
            spatial = new GrainSpatialField(field, new[] { new GrainRegion { percent = 100, layers = layers.ToArray() } }, arrangement, 0, grainLayoutSeed);
            useSpatialLayout = true;
        }
        var mixture = new float[GrainCargo.TypeCount];
        for (int t = 0; t < mixture.Length; t++) mixture[t] = cargo.Weight(t);
        double volume = cargo.Total / (kilogramsPerCubicMetre * VolumeScale);
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0) continue;
            float height = (float)(volume * weights[i] / sum);
            field.Heights[i] += height;
            spatial.Deposit(i, height * (double)field.AreaAt(i), mixture);
        }
        cargo.Clear();
        field.WakeNeighborhood(local.x, local.z, radius + settlingMargin, WorldScale);
        settling = true;
        RefreshGrainAppearance(); RefreshSurface(); RefreshCollider();
        return true;
    }

    public static float DropRadius(float kilograms) => Mathf.Max(.3f,
        Mathf.Pow(3f * kilograms / (720f * Mathf.PI * Mathf.Tan(32f * Mathf.Deg2Rad)), 1f / 3f));

    public static GrainPile CreateDroppedPile(Vector3 position, GrainPile appearanceSource, float expectedKg = 0)
    {
        var go = new GameObject("Dropped grain pile");
        go.SetActive(false);
        go.transform.position = position;
        var pile = go.AddComponent<GrainPile>();
        pile.initialHeight = 0; pile.size = Mathf.Max(1.5f, DropRadius(expectedKg) * 2f + .5f); pile.resolution = 65;
        pile.fitToWalls = true; pile.useSpatialLayout = true;
        if (appearanceSource != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterials = appearanceSource.GetComponent<MeshRenderer>().sharedMaterials;
            var sourceKernels = appearanceSource.GetComponent<CornSurfaceKernels>();
            if (sourceKernels != null) go.AddComponent<CornSurfaceKernels>().CopyAppearanceFrom(sourceKernels);
        }
        go.SetActive(true);
        return pile;
    }

    private void Update()
    {
        RefreshGrainAppearance();
        if (!Application.isPlaying)
        {
            DetectWalls();
            if (rebuildRequested || lastScale != WorldScale || lastPlacement != transform.localToWorldMatrix || boundary == null || !boundary.Matches(detectedWalls)) RebuildPreview();
            return;
        }
        if (field == null || Time.deltaTime <= 0) return;
        if (settling && Time.time >= nextStep)
        {
            nextStep = Time.time + 1f / simulationRate;
            settling = field.Settle(reposeAngle, .45f, WorldScale);
            if (settling) RefreshSurface();
        }
        if (colliderDirty && Time.time >= nextCollider) RefreshCollider();
    }

    private void RefreshSurface()
    {
        SurfaceRevision++;
        if (DetectedWallCount > 0)
        {
            boundary.UpdateHeights(field, .045f / WorldScale.y);
            mesh.Clear(); mesh.SetVertices(boundary.Positions); mesh.SetUVs(0, boundary.UV); mesh.SetTriangles(boundary.Triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            colliderDirty = true;
            return;
        }
        // The volume skin sits beneath the visible kernels. Bury its zero-height apron
        // below the ground instead of exposing large orange triangles around the pile.
        for (int i = 0; i < vertices.Length; i++) vertices[i].y = field.Heights[i] - .045f / WorldScale.y;
        mesh.vertices = vertices;
        activeTriangles.Clear();
        for (int t = 0; t < field.Triangles.Length; t += 3)
        {
            int a = field.Triangles[t], b = field.Triangles[t + 1], c = field.Triangles[t + 2];
            if (field.Heights[a] <= .00001f && field.Heights[b] <= .00001f && field.Heights[c] <= .00001f) continue;
            activeTriangles.Add(a); activeTriangles.Add(b); activeTriangles.Add(c);
        }
        // Bare ground must not retain a golden rectangle or a phantom grain collider.
        mesh.SetTriangles(activeTriangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        colliderDirty = true;
    }

    private void OnValidate() { rebuildRequested = true; lastAppearanceKg = -1; SurfaceRevision++; }

    public void RebuildPreview()
    {
        if (Application.isPlaying) return; // Never reset excavated material during gameplay.
        ReleaseMeshes(); field = null;
        Initialize();
    }

    private void RefreshCollider()
    {
        meshCollider.sharedMesh = null;
        if (DetectedWallCount > 0)
        {
            boundary.UpdateHeights(field, 0);
            collisionMesh.Clear(); collisionMesh.SetVertices(boundary.Positions); collisionMesh.SetTriangles(boundary.Triangles, 0); collisionMesh.RecalculateBounds();
            meshCollider.enabled = boundary.Triangles.Count > 0;
            if (meshCollider.enabled) meshCollider.sharedMesh = collisionMesh;
            nextCollider = Time.time + 1f / colliderRate; colliderDirty = false;
            return;
        }
        // Collision and mass use the actual surface, never the recessed visual skin.
        for (int i = 0; i < vertices.Length; i++) vertices[i].y = field.Heights[i];
        collisionMesh.vertices = vertices;
        collisionMesh.SetTriangles(activeTriangles, 0);
        collisionMesh.RecalculateBounds();
        meshCollider.enabled = activeTriangles.Count > 0;
        if (meshCollider.enabled) meshCollider.sharedMesh = collisionMesh;
        nextCollider = Time.time + 1f / colliderRate;
        colliderDirty = false;
    }

    private void OnDestroy() => ReleaseMeshes();

    private void DetectWalls()
    {
        if (fitToWalls) GrainWallBoundary.Detect(transform, size, initialHeight, wallMask, detectedWalls, wallHits);
        else detectedWalls.Clear();
    }

    private void ReleaseMeshes()
    {
        spatial?.Dispose(); spatial = null;
        if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if (collisionMesh != null) { if (Application.isPlaying) Destroy(collisionMesh); else DestroyImmediate(collisionMesh); }
        mesh = collisionMesh = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .7f, .1f, .6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * initialHeight * .5f, new Vector3(size, initialHeight, size));
    }
}
