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
    [SerializeField, Min(.1f)] private float fullDetailDistance = 1.5f;
    [SerializeField, Min(.2f)] private float mediumDetailDistance = 4f;
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
    private float[] previousHeights;
    private bool[] dirtyCells;
    private int[] sampleCells, sampleBatches, sampleSlots;
    private Bounds[] batchBounds, surfaceBounds;
    private bool[] dirtyBatches, liveBatches;
    private int[] drawOrder;
    private readonly Plane[] cameraPlanes = new Plane[6];
    private Mesh mediumMesh, distantMesh;
    private readonly GrainAppearance grainAppearance = new GrainAppearance();
    public int LastUpdatedKernels { get; private set; }
    public int LastDrawTriangles { get; private set; }

    public int KernelCount { get; private set; }

    public void CopyAppearanceFrom(CornSurfaceKernels source)
    {
        kernelMesh = source.kernelMesh; kernelMaterial = source.kernelMaterial;
        colorVariants = source.colorVariants; spacing = source.spacing;
        pile = GetComponent<GrainPile>();
    }

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
        // Component OnEnable order is not guaranteed when a scene is loaded.
        // The generated surface and cell cache both depend on the pile's field.
        pile.Initialize();
        if (pile.HeightField == null)
        {
            samples = null; batches = null; KernelCount = 0;
            return;
        }
        if (Application.isPlaying && samples != null && previousHeights != null &&
            sampledScale == pile.WorldScale && lastTransform == transform.localToWorldMatrix)
        { UpdateChangedCells(); return; }
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
            // Submission order changes only. Seeded positions, colors and rotations stay identical.
            drawOrder = new int[samples.Length];
            var keys = new uint[samples.Length];
            float extent = Mathf.Max(.001f, pile.HeightField.Size);
            for (int i = 0; i < samples.Length; i++)
            {
                drawOrder[i] = i;
                uint x = (uint)Mathf.Clamp((int)((samples[i].x / extent + .5f) * 65535), 0, 65535);
                uint z = (uint)Mathf.Clamp((int)((samples[i].y / extent + .5f) * 65535), 0, 65535);
                keys[i] = SpreadBits(x) | (SpreadBits(z) << 1);
            }
            Array.Sort(keys, drawOrder);
        }
        Array.Clear(counts, 0, counts.Length);
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        KernelCount = 0;
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
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
        CacheCells();
    }

    private void CacheCells()
    {
        var field = pile.HeightField;
        previousHeights = (float[])field.Heights.Clone();
        dirtyCells = new bool[previousHeights.Length];
        sampleCells = new int[samples.Length]; sampleBatches = new int[samples.Length]; sampleSlots = new int[samples.Length];
        batchBounds = new Bounds[batches.Length];
        surfaceBounds = new Bounds[batches.Length];
        dirtyBatches = new bool[batches.Length]; liveBatches = new bool[batches.Length];
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
            Vector2 p = samples[i];
            int x = Mathf.Clamp((int)((p.x + field.Size * .5f) / field.Spacing), 0, field.Resolution - 2);
            int z = Mathf.Clamp((int)((p.y + field.Size * .5f) / field.Spacing), 0, field.Resolution - 2);
            sampleCells[i] = z * field.Resolution + x;
            sampleBatches[i] = -1;
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(new Vector3(p.x, 0, p.y)), .04f) || field.Sample(p.x, p.y) < .001f) continue;
            int color = paletteIndices[i], batch = color * batchesPerColor + paletteCounts[color] / 1023, slot = paletteCounts[color]++ % 1023;
            sampleBatches[i] = batch; sampleSlots[i] = slot;
            Vector3 position = batches[batch][slot].GetColumn(3);
            if (slot == 0) batchBounds[batch] = new Bounds(position, Vector3.one * .1f);
            else batchBounds[batch].Encapsulate(position);
        }
        // Bounds retain the initial height envelope as excavation only lowers it.
        for (int b = 0; b < batchBounds.Length; b++)
        {
            surfaceBounds[b] = batchBounds[b]; surfaceBounds[b].Expand(.12f);
            liveBatches[b] = counts[b] > 0;
            Vector3 bottom = batchBounds[b].center; bottom.y = pile.transform.position.y;
            batchBounds[b].Encapsulate(bottom); batchBounds[b].Expand(.12f);
        }
        LastUpdatedKernels = KernelCount;
    }

    private void UpdateChangedCells()
    {
        var field = pile.HeightField;
        int n = field.Resolution;
        Array.Clear(dirtyCells, 0, dirtyCells.Length);
        for (int i = 0; i < previousHeights.Length; i++)
        {
            if (previousHeights[i] == field.Heights[i]) continue;
            previousHeights[i] = field.Heights[i];
            // Include neighbours used by the surface-normal finite differences.
            int x = i % n, z = i / n;
            int reach = Mathf.CeilToInt(.03f / field.Spacing) + 1;
            for (int dz = Mathf.Max(0, z - reach); dz <= Mathf.Min(n - 1, z + reach); dz++)
            for (int dx = Mathf.Max(0, x - reach); dx <= Mathf.Min(n - 1, x + reach); dx++) dirtyCells[dz * n + dx] = true;
        }
        Matrix4x4 localToWorld = transform.localToWorldMatrix, normalMatrix = transform.worldToLocalMatrix.transpose;
        Quaternion yaw = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up);
        LastUpdatedKernels = 0;
        Array.Clear(dirtyBatches, 0, dirtyBatches.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            int batch = sampleBatches[i];
            if (batch < 0 || !dirtyCells[sampleCells[i]]) continue;
            Vector2 p = samples[i];
            float h = field.Sample(p.x, p.y);
            float dx = field.Sample(p.x + .03f, p.y) - field.Sample(p.x - .03f, p.y);
            float dz = field.Sample(p.x, p.y + .03f) - field.Sample(p.x, p.y - .03f);
            Vector3 normal = normalMatrix.MultiplyVector(new Vector3(-dx, .06f, -dz)).normalized;
            batches[batch][sampleSlots[i]] = Matrix4x4.TRS(localToWorld.MultiplyPoint3x4(new Vector3(p.x, h, p.y)) + Vector3.up * .003f,
                Quaternion.FromToRotation(Vector3.up, normal) * yaw * rotations[i],
                new Vector3(.0255f, .014f, .029f) * (h < .001f ? 0 : sizes[i]));
            LastUpdatedKernels++;
            dirtyBatches[batch] = true;
        }
        for (int b = 0; b < batches.Length; b++)
        {
            if (!dirtyBatches[b]) continue;
            bool found = false; Bounds bounds = default;
            for (int slot = 0; slot < counts[b]; slot++)
            {
                Matrix4x4 matrix = batches[b][slot];
                if (((Vector3)matrix.GetColumn(0)).sqrMagnitude < .00000001f) continue;
                Vector3 point = matrix.GetColumn(3);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
            liveBatches[b] = found;
            if (found) { bounds.Expand(.12f); surfaceBounds[b] = bounds; }
        }
        revision = pile.SurfaceRevision;
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
        LastDrawTriangles = 0;
        if (batches == null || kernelMesh == null || kernelMaterial == null || camera.cameraType == CameraType.Preview) return;
        var renderer = pile.GetComponent<MeshRenderer>();
        if (renderer != null && (renderer.bounds.ClosestPoint(camera.transform.position) - camera.transform.position).sqrMagnitude > drawDistance * drawDistance) return;
        if (mediumMesh == null) { mediumMesh = CreateLodMesh(8); distantMesh = CreateLodMesh(4); }
        GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
        grainAppearance.Set(pile.SurfaceGrain);
        pile.ApplySpatialAppearance(grainAppearance.Block);
        LastDrawTriangles = 0;
        for (int i = 0; i < batches.Length; i++)
            if (counts[i] > 0 && liveBatches[i] && GeometryUtility.TestPlanesAABB(cameraPlanes, surfaceBounds[i]))
            {
                float distance = Vector3.Distance(camera.transform.position, surfaceBounds[i].ClosestPoint(camera.transform.position));
                if (distance > drawDistance) continue;
                Mesh lod = distance < fullDetailDistance ? kernelMesh : distance < Mathf.Max(fullDetailDistance, mediumDetailDistance) ? mediumMesh : distantMesh;
                LastDrawTriangles += counts[i] * (int)(lod.GetIndexCount(0) / 3);
                Graphics.DrawMeshInstanced(lod, 0,
                    colorVariants != null && colorVariants.Length == colorCount && colorVariants[i / batchesPerColor] != null
                        ? colorVariants[i / batchesPerColor] : kernelMaterial, batches[i], counts[i], grainAppearance.Block,
                    ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
            }
    }

    // Interleave X/Z bits to put neighboring surface samples into neighboring draw batches.
    private static uint SpreadBits(uint value)
    {
        value = (value | value << 8) & 0x00FF00FF;
        value = (value | value << 4) & 0x0F0F0F0F;
        value = (value | value << 2) & 0x33333333;
        return (value | value << 1) & 0x55555555;
    }

    private static Mesh CreateLodMesh(int sides)
    {
        var vertices = new Vector3[sides + 2]; var normals = new Vector3[sides + 2];
        var indices = new int[sides * 6];
        vertices[0] = Vector3.up; vertices[1] = Vector3.down;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            vertices[i + 2] = new Vector3(Mathf.Cos(angle) * .79f, 0, Mathf.Sin(angle) * .95f);
            int next = (i + 1) % sides + 2, t = i * 6;
            indices[t] = 0; indices[t + 1] = next; indices[t + 2] = i + 2;
            indices[t + 3] = 1; indices[t + 4] = i + 2; indices[t + 5] = next;
        }
        for (int i = 0; i < vertices.Length; i++) normals[i] = vertices[i].normalized;
        var mesh = new Mesh { name = "Corn LOD " + sides * 2, hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = indices; mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (mediumMesh != null) { if (Application.isPlaying) Destroy(mediumMesh); else DestroyImmediate(mediumMesh); }
        if (distantMesh != null) { if (Application.isPlaying) Destroy(distantMesh); else DestroyImmediate(distantMesh); }
    }
}

