using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public sealed class GrainPile : MonoBehaviour
{
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

    private void Awake() => Initialize();
    private void OnEnable() => Initialize();

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

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg)
    {
        if (!isActiveAndEnabled || !(capacityKg > 0f)) return 0;
        Initialize();
        if (field == null || !IsInsideBoundary(worldPoint)) return 0;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        // Reject a stale hit well above/below the actual surface.
        Vector3 scale = WorldScale;
        if (Mathf.Abs(p.y - field.Sample(p.x, p.z)) * scale.y > depth + .15f) return 0;
        float density = kilogramsPerCubicMetre * VolumeScale;
        float removed = field.Scoop(p.x, p.z, radius, depth / scale.y, capacityKg / density, settlingMargin, scale);
        if (removed > 0)
        {
            settling = true;
            RefreshSurface();
            if (Time.time >= nextCollider) RefreshCollider();
        }
        return removed * density;
    }

    private void Update()
    {
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

    private void OnValidate() { rebuildRequested = true; SurfaceRevision++; }

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
