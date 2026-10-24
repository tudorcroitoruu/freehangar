using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Instanced geometry on the volume surface; no per-kernel scene objects or colliders.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CornSurfaceKernels : MonoBehaviour
{
    [SerializeField] private GrainPile pile;
    [SerializeField] private Mesh kernelMesh;
    [SerializeField] private Material kernelMaterial;
    [SerializeField] private Material[] colorVariants;
    [SerializeField, Range(.025f, .12f)] private float spacing = .0315f;
    [SerializeField] private float drawDistance = 22f;
    [Tooltip("Advisory kernel budget. Exceeding this never spreads kernels farther apart; surface coverage is preserved.")]
    [SerializeField, Range(10000, 1000000)] private int maximumSurfaceKernels = 500000;
    public bool ExceedsKernelBudget => KernelCount > maximumSurfaceKernels;
    private Vector3 sampledScale;
    public float EffectiveSpacing { get; private set; }
    private bool rebuildRequested;
    private Vector2[] samples;
    private Quaternion[] rotations;
    private float[] sizes;
    private Matrix4x4[][] batches;
    private int[] counts;
    private int[] paletteCounts;
    private int[] paletteIndices;
    private int batchesPerColor, colorCount;
    private int revision = -1;
    private Matrix4x4 lastTransform;

    public int KernelCount { get; private set; }

    private void OnEnable()
    {
        // Unity hot reload can restore flat arrays but not jagged draw batches. Rebuild them together.
        samples = null;
        if (pile == null) pile = GetComponent<GrainPile>();
        Build();
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

    public void Rebuild() { samples = null; Build(); }

    private void Build()
    {
        if (pile == null || kernelMesh == null || kernelMaterial == null) return;
        float actualSpacing = Mathf.Max(.025f, spacing);
        Vector3 worldScale = pile.WorldScale;
        if (worldScale.x <= 0 || worldScale.y <= 0 || worldScale.z <= 0 || Vector3.Dot(pile.transform.up, Vector3.up) < .9999f)
        { samples = null; batches = null; KernelCount = 0; return; }
        if (sampledScale != worldScale || (!Application.isPlaying && revision != pile.SurfaceRevision)) samples = null;
        sampledScale = worldScale;
        if (samples == null)
        {
            samples = SampleSurface(actualSpacing);
            rotations = new Quaternion[samples.Length]; sizes = new float[samples.Length];
            colorCount = colorVariants != null && colorVariants.Length > 0 ? colorVariants.Length : 1;
            paletteCounts = new int[colorCount]; paletteIndices = new int[samples.Length];
            batchesPerColor = Mathf.Max(1, ((samples.Length + colorCount - 1) / colorCount + 1022) / 1023);
            batches = new Matrix4x4[batchesPerColor * colorCount][]; counts = new int[batches.Length];
            for (int i = 0; i < batches.Length; i++) batches[i] = new Matrix4x4[1023];
            var random = new System.Random(731);
            for (int i = 0; i < samples.Length; i++)
            {
                rotations[i] = Quaternion.Euler((float)random.NextDouble() * 28f - 14f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 26f - 13f);
                sizes[i] = .9f + (float)random.NextDouble() * .22f; paletteIndices[i] = i % colorCount;
            }
            for (int i = paletteIndices.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temporary = paletteIndices[i]; paletteIndices[i] = paletteIndices[j]; paletteIndices[j] = temporary;
            }
        }
        Array.Clear(counts, 0, counts.Length);
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        KernelCount = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            Vector2 p = samples[i];
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(new Vector3(p.x, 0, p.y)), .04f)) continue;
            float height = pile.SampleLocalHeight(p.x, p.y);
            if (height < .001f) continue;
            float dx = pile.SampleLocalHeight(p.x + .03f, p.y, false) - pile.SampleLocalHeight(p.x - .03f, p.y, false);
            float dz = pile.SampleLocalHeight(p.x, p.y + .03f, false) - pile.SampleLocalHeight(p.x, p.y - .03f, false);
            Vector3 normal = pile.transform.worldToLocalMatrix.transpose.MultiplyVector(new Vector3(-dx, .06f, -dz)).normalized;
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, normal);
            int color = paletteIndices[i];
            int batch = color * batchesPerColor + paletteCounts[color] / 1023, slot = paletteCounts[color] % 1023;
            batches[batch][slot] = Matrix4x4.TRS(pile.transform.TransformPoint(new Vector3(p.x, height, p.y)) + Vector3.up * .003f,
                slope * Quaternion.AngleAxis(pile.transform.eulerAngles.y, Vector3.up) * rotations[i],
                new Vector3(.0255f, .014f, .029f) * sizes[i]);
            paletteCounts[color]++;
            counts[batch]++;
            KernelCount++;
        }
        revision = pile.SurfaceRevision;
        lastTransform = transform.localToWorldMatrix;
    }

    private void LateUpdate()
    {
        if (rebuildRequested) { rebuildRequested = false; Rebuild(); }
        if (pile != null && (revision != pile.SurfaceRevision || lastTransform != transform.localToWorldMatrix)) Build();
    }

    private Vector2[] SampleSurface(float requestedSpacing)
    {
        Mesh surface = pile.GetComponent<MeshFilter>().sharedMesh;
        if (surface == null) return Array.Empty<Vector2>();
        Vector3[] vertices = surface.vertices; int[] triangles = surface.triangles;
        float area = 0;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = pile.transform.TransformPoint(vertices[i]);
        for (int i = 0; i < triangles.Length; i += 3)
            area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * .5f;
        // Preserve world-space coverage even beyond the advisory budget. Never silently
        // expose the substrate by increasing spacing when the author enlarges a pile.
        float step = requestedSpacing;
        EffectiveSpacing = step;
        var points = new List<Vector2>(Mathf.CeilToInt(area / (step * step * .866f)));
        var random = new System.Random(193);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            // Put the longest edge along X so skinny clipped triangles have small sample bounds.
            if ((c - b).sqrMagnitude > (b - a).sqrMagnitude && (c - b).sqrMagnitude > (c - a).sqrMagnitude) { Vector3 old = a; a = b; b = c; c = old; }
            else if ((c - a).sqrMagnitude > (b - a).sqrMagnitude) { Vector3 old = b; b = c; c = old; }
            float length = (b - a).magnitude;
            if (length < .00001f) continue;
            Vector3 right = (b - a) / length, up = Vector3.ProjectOnPlane(c - a, right).normalized;
            float cx = Vector3.Dot(c - a, right), cy = Vector3.Dot(c - a, up);
            if (cy < .00001f) continue;
            int before = points.Count;
            float rowStep = step * .866f;
            int rows = Mathf.CeilToInt(cy / rowStep);
            for (int row = 0; row < rows; row++)
            {
                float y = (row + .5f) * rowStep;
                if (y > cy) continue;
                float x0 = cx * y / cy, x1 = length + (cx - length) * y / cy;
                int columns = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / step));
                for (int col = 0; col < columns; col++)
                {
                    float x = x0 + (col + .35f + (float)random.NextDouble() * .3f) * (x1 - x0) / columns;
                    Vector3 local = pile.transform.InverseTransformPoint(a + right * x + up * y);
                    points.Add(new Vector2(local.x, local.z));
                }
            }
            if (points.Count == before)
            {
                Vector3 local = pile.transform.InverseTransformPoint((a + b + c) / 3f);
                points.Add(new Vector2(local.x, local.z));
            }
        }
        return points.ToArray();
    }

    private void OnValidate() => rebuildRequested = true;

    private void Draw(ScriptableRenderContext context, Camera camera)
    {
        if (batches == null || kernelMesh == null || kernelMaterial == null || camera.cameraType == CameraType.Preview) return;
        var renderer = pile.GetComponent<MeshRenderer>();
        if (renderer != null && (renderer.bounds.ClosestPoint(camera.transform.position) - camera.transform.position).sqrMagnitude > drawDistance * drawDistance) return;
        for (int i = 0; i < batches.Length; i++)
            if (counts[i] > 0)
                Graphics.DrawMeshInstanced(kernelMesh, 0,
                    colorVariants != null && colorVariants.Length == colorCount && colorVariants[i / batchesPerColor] != null
                        ? colorVariants[i / batchesPerColor] : kernelMaterial, batches[i], counts[i], null,
                    ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
    }
}

