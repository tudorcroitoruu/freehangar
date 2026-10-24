using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public sealed class GrainPile : MonoBehaviour
{
    [SerializeField, Range(9, 65)] private int resolution = 33;
    [SerializeField, Min(1f)] private float size = 6f;
    [SerializeField, Min(0f)] private float initialHeight = 1.8f;
    [SerializeField, Min(1f)] private float kilogramsPerCubicMetre = 720f;
    [SerializeField, Range(5f, 60f)] private float reposeAngle = 32f;
    [SerializeField, Min(0f)] private float settlingMargin = .45f;
    [SerializeField, Range(1f, 30f)] private float simulationRate = 15f;
    [SerializeField, Range(1f, 15f)] private float colliderRate = 5f;
    private GrainHeightField field;
    private Mesh mesh;
    private Mesh collisionMesh;
    private MeshCollider meshCollider;
    private Vector3[] vertices;
    private List<int> activeTriangles;
    private bool settling;
    private bool colliderDirty;
    private float nextStep, nextCollider;
    private GrainHeightField previewField;
    public int SurfaceRevision { get; private set; }
    public float Size => size;
    public float SampleLocalHeight(float x, float z)
    {
        if (Application.isPlaying) { Initialize(); return field == null ? 0 : field.Sample(x, z); }
        if (previewField == null) previewField = new GrainHeightField(resolution, size, initialHeight);
        return previewField.Sample(x, z);
    }

    public double Kilograms => field == null ? 0 : field.Volume * kilogramsPerCubicMetre;
    public float SurfaceHeight(Vector3 worldPoint)
    {
        Initialize();
        if (field == null) return worldPoint.y;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        return transform.TransformPoint(new Vector3(p.x, field.Sample(p.x, p.z), p.z)).y;
    }

    private void Awake() => Initialize();

    public void Initialize()
    {
        if (field != null) return;
        // Volumes and gravity assume metres, horizontal ground and unit world scale.
        if ((transform.lossyScale - Vector3.one).sqrMagnitude > .0001f || Vector3.Dot(transform.up, Vector3.up) < .9999f)
        {
            Debug.LogError("GrainPile requires unit world scale and an upright transform (yaw is allowed).", this);
            enabled = false;
            return;
        }
        field = new GrainHeightField(resolution, size, initialHeight);
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
        mesh = new Mesh { name = "Grain surface (runtime)" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = field.Triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        collisionMesh = Instantiate(mesh);
        collisionMesh.name = "Grain collision (runtime)";
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = collisionMesh;
        RefreshSurface();
        RefreshCollider();
        settling = false;
    }

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg)
    {
        if (!isActiveAndEnabled || !(capacityKg > 0f)) return 0;
        Initialize();
        if (field == null) return 0;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        // Reject a stale hit well above/below the actual surface.
        if (Mathf.Abs(p.y - field.Sample(p.x, p.z)) > depth + .15f) return 0;
        float removed = field.Scoop(p.x, p.z, radius, depth, capacityKg / kilogramsPerCubicMetre, settlingMargin);
        if (removed > 0)
        {
            settling = true;
            RefreshSurface();
            if (Time.time >= nextCollider) RefreshCollider();
        }
        return removed * kilogramsPerCubicMetre;
    }

    private void Update()
    {
        if (field == null || Time.deltaTime <= 0) return;
        if (settling && Time.time >= nextStep)
        {
            nextStep = Time.time + 1f / simulationRate;
            settling = field.Settle(reposeAngle, .45f);
            if (settling) RefreshSurface();
        }
        if (colliderDirty && Time.time >= nextCollider) RefreshCollider();
    }

    private void RefreshSurface()
    {
        SurfaceRevision++;
        // The volume skin sits beneath the visible kernels. Bury its zero-height apron
        // below the ground instead of exposing large orange triangles around the pile.
        for (int i = 0; i < vertices.Length; i++) vertices[i].y = field.Heights[i] - .045f;
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

    private void OnValidate() { previewField = null; SurfaceRevision++; }

    private void RefreshCollider()
    {
        meshCollider.sharedMesh = null;
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

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (collisionMesh != null) Destroy(collisionMesh);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .7f, .1f, .6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * initialHeight * .5f, new Vector3(size, initialHeight, size));
    }
}
