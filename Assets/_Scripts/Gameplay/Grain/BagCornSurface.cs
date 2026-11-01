using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

/// <summary>Real kernels following the sack's existing fill morph, without per-kernel physics objects.</summary>
[ExecuteAlways, DefaultExecutionOrder(150), DisallowMultipleComponent]
public sealed class BagCornSurface : MonoBehaviour
{
    [SerializeField] private SackController sack;
    [SerializeField] private SkinnedMeshRenderer grainSurface;
    [SerializeField] private Mesh kernelMesh;
    [SerializeField] private Material kernelMaterial;
    [SerializeField] private Vector3 surfaceUp = Vector3.forward;
    [SerializeField, Range(.022f, .05f)] private float spacing = .026f;
    private Mesh baked;
    private readonly List<Vector3> vertices = new List<Vector3>(256);
    private readonly List<int> triangles = new List<int>(1200);
    private readonly Matrix4x4[] first = new Matrix4x4[1023], second = new Matrix4x4[1023];
    private readonly Matrix4x4[] localFirst = new Matrix4x4[1023], localSecond = new Matrix4x4[1023];
    private const int SampleGridSize = 16;
    private readonly List<int>[] sampleCells = new List<int>[SampleGridSize * SampleGridSize];
    private Bounds samplingBounds;
    private Vector2 sampleGridScale;
    private Vector3 lastScale;
    private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("BagGrain.BakeAndSample");
    public int SurfaceBakeCount { get; private set; }
    private float lastFill = -1, lastClose = -1;
    private float nextSurfaceUpdate;
    private Matrix4x4 lastTransform;
    private uint randomState;
    private GrainContainer container;
    private readonly GrainAppearance grainAppearance = new GrainAppearance();
    public int KernelCount { get; private set; }

    private void OnEnable()
    {
        lastFill = -1;
        container = GetComponent<GrainContainer>();
        if (sack != null) sack.RefreshBindings();
        RenderPipelineManager.beginCameraRendering += Draw;
    }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; KernelCount = 0; }
    private void OnValidate() => lastFill = -1;

    private void LateUpdate()
    {
        if (sack == null || grainSurface == null || kernelMesh == null || kernelMaterial == null) return;
        if (lastFill == sack.Fill && lastClose == sack.Close && lastScale == grainSurface.transform.lossyScale)
        {
            if (lastTransform != grainSurface.transform.localToWorldMatrix) UpdateWorldMatrices();
            return;
        }
        // Filling animates every frame, but surface baking and triangle sampling need not.
        // Empty/closed bags still hide their kernels immediately.
        bool hide = sack.Fill < .0001f || sack.Close >= .4f;
        if (Application.isPlaying && !hide && Time.unscaledTime < nextSurfaceUpdate) return;
        nextSurfaceUpdate = Time.unscaledTime + .05f;
        Rebuild();
    }

    public void Rebuild()
    {
        using (RebuildMarker.Auto()) RebuildSurface();
    }

    private void UpdateWorldMatrices()
    {
        lastTransform = grainSurface.transform.localToWorldMatrix;
        for (int i = 0; i < KernelCount; i++)
            if (i < 1023) first[i] = lastTransform * localFirst[i];
            else second[i - 1023] = lastTransform * localSecond[i - 1023];
    }

    private void RebuildSurface()
    {
        KernelCount = 0;
        if (sack == null || grainSurface == null) return;
        lastFill = sack.Fill; lastClose = sack.Close; lastTransform = grainSurface.transform.localToWorldMatrix;
        lastScale = grainSurface.transform.lossyScale;
        if (sack.Fill < .0001f || sack.Close >= .4f) return;
        if (baked == null) baked = new Mesh { name = "Bag corn surface sampling", hideFlags = HideFlags.DontSave };
        grainSurface.BakeMesh(baked);
        SurfaceBakeCount++;
        baked.GetVertices(vertices); baked.GetTriangles(triangles, 0);
        if (vertices.Count == 0) return;
        Quaternion frame = Quaternion.FromToRotation(Vector3.up, surfaceUp.normalized);
        Quaternion inverse = Quaternion.Inverse(frame);
        Bounds bounds = new Bounds(inverse * vertices[0], Vector3.zero);
        for (int i = 0; i < vertices.Count; i++) { vertices[i] = inverse * vertices[i]; bounds.Encapsulate(vertices[i]); }
        BuildSampleGrid(bounds);
        float sx = grainSurface.transform.TransformVector(frame * Vector3.right).magnitude;
        float sz = grainSurface.transform.TransformVector(frame * Vector3.forward).magnitude;
        if (sx < .0001f || sz < .0001f) return;
        float step = Mathf.Max(spacing, Mathf.Sqrt(bounds.size.x * sx * bounds.size.z * sz / 1800f));
        int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x * sx / step));
        int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z * sz / step));
        randomState = 371;
        float reveal = Mathf.Clamp01(sack.Fill * 30f) * Mathf.Clamp01(1f - sack.Close / .4f);
        Matrix4x4 worldToLocal = grainSurface.transform.worldToLocalMatrix;
        for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
        {
            if (KernelCount >= 2046) return;
            float px = Mathf.Lerp(bounds.min.x, bounds.max.x, (x + .2f + Random01() * .6f) / columns);
            float pz = Mathf.Lerp(bounds.min.z, bounds.max.z, (z + .2f + Random01() * .6f) / rows);
            // Keep each whole kernel inside the morph's perimeter, including when the mouth narrows.
            float insetX = Mathf.Max(0, 1f - .019f / Mathf.Max(.019f, bounds.extents.x * sx));
            float insetZ = Mathf.Max(0, 1f - .019f / Mathf.Max(.019f, bounds.extents.z * sz));
            if (!Sample(px, pz, out float height)) continue;
            px = Mathf.Lerp(bounds.center.x, px, insetX); pz = Mathf.Lerp(bounds.center.z, pz, insetZ);
            if (!Sample(px, pz, out height)) continue;
            Vector3 position = grainSurface.transform.TransformPoint(frame * new Vector3(px, height, pz));
            Vector3 up = grainSurface.transform.TransformDirection(surfaceUp).normalized;
            position += up * (.008f * reveal);
            Quaternion rotation = grainSurface.transform.rotation * frame * Quaternion.Euler(
                Random01() * 24f - 12f, Random01() * 360f, Random01() * 24f - 12f);
            float variation = .88f + Random01() * .24f;
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, new Vector3(.016f, .010f, .019f) * (variation * reveal));
            if (KernelCount < 1023) { first[KernelCount] = matrix; localFirst[KernelCount] = worldToLocal * matrix; }
            else { second[KernelCount - 1023] = matrix; localSecond[KernelCount - 1023] = worldToLocal * matrix; }
            KernelCount++;
        }
    }

    private float Random01()
    {
        randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
        return (randomState & 0xFFFFFF) / 16777216f;
    }

    private void BuildSampleGrid(Bounds bounds)
    {
        samplingBounds = bounds;
        sampleGridScale = new Vector2(SampleGridSize / Mathf.Max(.00001f, bounds.size.x), SampleGridSize / Mathf.Max(.00001f, bounds.size.z));
        for (int i = 0; i < sampleCells.Length; i++)
        {
            if (sampleCells[i] == null) sampleCells[i] = new List<int>(8);
            else sampleCells[i].Clear();
        }
        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            int minX = SampleCell(Mathf.Min(a.x, Mathf.Min(b.x, c.x)), bounds.min.x, sampleGridScale.x);
            int maxX = SampleCell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)), bounds.min.x, sampleGridScale.x);
            int minZ = SampleCell(Mathf.Min(a.z, Mathf.Min(b.z, c.z)), bounds.min.z, sampleGridScale.y);
            int maxZ = SampleCell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)), bounds.min.z, sampleGridScale.y);
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++) sampleCells[z * SampleGridSize + x].Add(i);
        }
    }

    private static int SampleCell(float value, float minimum, float scale) => Mathf.Clamp(Mathf.FloorToInt((value - minimum) * scale), 0, SampleGridSize - 1);

    private bool Sample(float x, float z, out float height)
    {
        height = float.NegativeInfinity;
        var candidates = sampleCells[SampleCell(z, samplingBounds.min.z, sampleGridScale.y) * SampleGridSize + SampleCell(x, samplingBounds.min.x, sampleGridScale.x)];
        if (candidates == null) return false;
        foreach (int i in candidates)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < .0000001f) continue;
            float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
            float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
            if (u < 0 || v < 0 || u + v > 1) continue;
            height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
        }
        return !float.IsNegativeInfinity(height);
    }

    private void Draw(ScriptableRenderContext context, Camera camera)
    {
        if (KernelCount == 0 || kernelMesh == null || kernelMaterial == null || camera.cameraType == CameraType.Preview) return;
        grainAppearance.Set(container != null ? container.AcceptedGrain : GrainType.Corn);
        if (grainSurface != null) grainSurface.SetPropertyBlock(grainAppearance.Block);
        Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, first, Mathf.Min(1023, KernelCount), grainAppearance.Block,
            ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
        if (KernelCount > 1023) Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, second, KernelCount - 1023, grainAppearance.Block,
            ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
    }
    private void OnDestroy()
    {
        if (baked != null) { if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); }
    }
}
