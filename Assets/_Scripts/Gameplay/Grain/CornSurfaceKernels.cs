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
    public bool ExceedsKernelBudget => KernelCount + SideKernelCount > maximumSurfaceKernels;
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
    private Texture2D surfaceHeights;
    private Vector2[] surfacePixels;
    private bool gpuSurface;
    private int uploadedRevision = -1;
    private bool[] dirtyCells;
    private int[] sampleCells, sampleBatches, sampleSlots;
    private int[] cellSampleHeads, nextCellSample;
    private int[] pendingCells, activeBatchCounts;
    private bool[] visibleSamples;
    private int pendingHead, pendingCount, pendingSample = -1;
    private float[] initialTriangleAreas;
    private readonly Dictionary<int, CoveragePatch> coveragePatches = new Dictionary<int, CoveragePatch>();
    private IEnumerator<Matrix4x4> pendingCoverage;
    private readonly List<Matrix4x4> coverageMatrices = new List<Matrix4x4>();
    private int coverageCell;
    private CoveragePatch[] edgePatches;
    private Vector4[] edgeHeights;
    private Vector3[] edgeCorners;
    private Matrix4x4 edgeTransform;
    private float edgeSize;
    private int nextEdge;
    public int SideKernelCount { get; private set; }
    private sealed class CoveragePatch
    {
        public Matrix4x4[][] batches;
        public Bounds bounds;
        public int count;
    }
    private const int KernelUpdatesPerFrame = 4096;
    private const double UpdateBudgetSeconds = .001;
    public bool HasPendingSurfaceUpdates => pendingCount > 0 || pendingSample >= 0 || pendingCoverage != null;
    private Bounds[] batchBounds, surfaceBounds;
    private bool[] liveBatches;
    private int[] drawOrder;
    private readonly Plane[] cameraPlanes = new Plane[6];
    private Mesh nearMesh, mediumMesh, distantMesh;
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
        if (pile == null) pile = GetComponent<GrainPile>();
        if (!Application.isPlaying || !TryReuseBatches()) Rebuild();
        rebuildRequested = false;
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    private bool TryReuseBatches()
    {
        // Hot reload can restore flat arrays without their jagged draw batches.
        if (rebuildRequested || batches == null || samples == null || previousHeights == null ||
            pile == null || pile.VisualHeightField == null || previousHeights.Length != pile.VisualHeightField.Heights.Length ||
            sampledScale != pile.WorldScale || lastTransform != transform.localToWorldMatrix) return false;
        if (revision != pile.SurfaceRevision) UpdateChangedCells();
        return true;
    }

    private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

    public void Rebuild() { samples = null; edgePatches = null; Build(); }

    private void Build()
    {
        if (pile == null || kernelMesh == null || kernelMaterial == null) return;
        // Component OnEnable order is not guaranteed when a scene is loaded.
        // The generated surface and cell cache both depend on the pile's field.
        pile.Initialize();
        gpuSurface = kernelMaterial.HasProperty("_PileSurfaceHeights");
        if (colorVariants != null)
            foreach (var variant in colorVariants)
                if (variant != null && !variant.HasProperty("_PileSurfaceHeights")) gpuSurface = false;
        if (pile.VisualHeightField == null)
        {
            samples = null; batches = null; KernelCount = 0;
            return;
        }
        if (Application.isPlaying && samples != null && previousHeights != null &&
            sampledScale == pile.WorldScale && lastTransform == transform.localToWorldMatrix)
        {
            // Bare cells have reserved samples, including when settling expands the pile.
            UpdateChangedCells(); return;
        }
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
            float extent = Mathf.Max(.001f, pile.VisualHeightField.Size);
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
        var normalMatrix = pile.transform.worldToLocalMatrix.transpose;
        var yaw = Quaternion.AngleAxis(pile.transform.eulerAngles.y, Vector3.up);
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
            Vector2 p = samples[i];
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(new Vector3(p.x, 0, p.y)), .04f)) continue;
            float height = pile.VisualHeightField.Sample(p.x, p.y);
            Quaternion slope = Quaternion.identity;
            if (!gpuSurface)
            {
                float dx = pile.VisualSurfaceHeight(p.x + .03f, p.y) - pile.VisualSurfaceHeight(p.x - .03f, p.y);
                float dz = pile.VisualSurfaceHeight(p.x, p.y + .03f) - pile.VisualSurfaceHeight(p.x, p.y - .03f);
                Vector3 normal = normalMatrix.MultiplyVector(new Vector3(-dx, .06f, -dz)).normalized;
                slope = Quaternion.FromToRotation(Vector3.up, normal);
            }
            int color = paletteIndices[i];
            int batch = color * batchesPerColor + paletteCounts[color] / 1023, slot = paletteCounts[color] % 1023;
            batches[batch][slot] = Matrix4x4.TRS(pile.transform.TransformPoint(new Vector3(p.x, height + pile.GroundOffset(p.x, p.y), p.y)) + Vector3.up * .003f,
                slope * yaw * rotations[i],
                new Vector3(.0255f, .014f, .029f) * (!gpuSurface && height < .001f ? 0 : sizes[i]));
            paletteCounts[color]++;
            counts[batch]++;
            if (height >= .001f) KernelCount++;
        }
        revision = pile.SurfaceRevision;
        lastTransform = transform.localToWorldMatrix;
        CacheCells();
        uploadedRevision = -1;
        UploadSurface();
    }

    private void UpdateEdgeCoverage()
    {
        if (pile == null || pile.VisualHeightField == null || pile.SideFaceCount == 0)
        { edgePatches=null;SideKernelCount=0;return; }
        var field = pile.VisualHeightField;
        int edgeCount = pile.SideFaceCount;
        if (edgePatches == null || edgePatches.Length != edgeCount || edgeSize != field.Size || edgeTransform != transform.localToWorldMatrix)
        {
            edgePatches = new CoveragePatch[edgeCount]; edgeHeights = new Vector4[edgeCount]; edgeCorners = new Vector3[edgeCount * 4];
            for(int i=0;i<edgeCount;i++) edgeHeights[i]=Vector4.one*float.MinValue;
            edgeTransform=transform.localToWorldMatrix;edgeSize=field.Size;nextEdge=0;SideKernelCount=0;
        }
        // Keep updates bounded while pouring; unchanged sides reuse their instance batches.
        for (int work=0;work<Mathf.Min(8,edgeCount);work++)
        {
            int edge=nextEdge++%edgeCount;
            pile.SideFace(edge, out Vector3 topA, out Vector3 topB, out Vector3 bottomA, out Vector3 bottomB);
            int offset = edge * 4;
            if (edgeHeights[edge].x != float.MinValue &&
                (edgeCorners[offset] - topA).sqrMagnitude < .000001f &&
                (edgeCorners[offset+1] - topB).sqrMagnitude < .000001f &&
                (edgeCorners[offset+2] - bottomA).sqrMagnitude < .000001f &&
                (edgeCorners[offset+3] - bottomB).sqrMagnitude < .000001f) continue;
            edgeHeights[edge] = Vector4.zero;
            edgeCorners[offset] = topA; edgeCorners[offset+1] = topB;
            edgeCorners[offset+2] = bottomA; edgeCorners[offset+3] = bottomB;
            if(edgePatches[edge]!=null)SideKernelCount-=edgePatches[edge].count;
            edgePatches[edge]=null;
            Vector3 normal=Vector3.Cross(bottomA-topA,topB-topA).normalized;
            var matrices=new List<Matrix4x4>();var random=new System.Random(913+edge);
            AddSideTriangle(topA,bottomA,topB,normal,random,matrices);
            AddSideTriangle(topB,bottomA,bottomB,normal,random,matrices);
            if(matrices.Count==0)continue;
            var patch=new CoveragePatch{count=matrices.Count,bounds=new Bounds(topA,Vector3.zero),batches=new Matrix4x4[(matrices.Count+1022)/1023][]};
            patch.bounds.Encapsulate(topB);patch.bounds.Encapsulate(bottomA);patch.bounds.Encapsulate(bottomB);patch.bounds.Expand(.12f);
            for(int batch=0;batch<patch.batches.Length;batch++)
            {
                int count=Mathf.Min(1023,matrices.Count-batch*1023);patch.batches[batch]=new Matrix4x4[count];
                matrices.CopyTo(batch*1023,patch.batches[batch],0,count);
            }
            edgePatches[edge]=patch;SideKernelCount+=patch.count;
        }
    }
    private void AddSideTriangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal,System.Random random,List<Matrix4x4> output)
    {
        var slope=Quaternion.FromToRotation(Vector3.up,normal);
        foreach(var point in SampleTriangle(a,b,c,Mathf.Max(.025f,spacing),random))
        {
            var rotation=Quaternion.Euler((float)random.NextDouble()*28-14,(float)random.NextDouble()*360,(float)random.NextDouble()*26-13);
            float size=.9f+(float)random.NextDouble()*.22f;
            output.Add(Matrix4x4.TRS(point+normal*.003f,slope*rotation,new Vector3(.0255f,.014f,.029f)*size));
        }
    }

    private void UploadSurface()
    {
        if (!gpuSurface || pile == null || uploadedRevision == pile.SurfaceRevision) return;
        var field = pile.VisualHeightField;
        if (surfaceHeights == null || surfaceHeights.width != field.Resolution)
        {
            ReleaseSurfaceTexture();
            surfaceHeights = new Texture2D(field.Resolution, field.Resolution, TextureFormat.RGFloat, false, true)
            { name = "Animated grain surface", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        if (surfacePixels == null || surfacePixels.Length != field.Heights.Length)
            surfacePixels = new Vector2[field.Heights.Length];
        for (int i = 0; i < surfacePixels.Length; i++)
            surfacePixels[i] = new Vector2(field.Heights[i], pile.GroundOffset(i));
        surfaceHeights.SetPixelData(surfacePixels, 0);
        surfaceHeights.Apply(false, false);
        uploadedRevision = pile.SurfaceRevision;
        // GPU displacement can grow above or below the original instance matrices.
        // Give both our culling and Unity's instanced draw the current height envelope.
        float maxHeight = 0, minHeight = 0;
        foreach (var pixel in surfacePixels)
        {
            maxHeight = Mathf.Max(maxHeight, pixel.x + pixel.y);
            minHeight = Mathf.Min(minHeight, pixel.y);
        }
        float bottom = transform.position.y + minHeight * pile.WorldScale.y - .1f, top = transform.position.y + maxHeight * pile.WorldScale.y + .1f;
        for (int b = 0; b < surfaceBounds.Length; b++)
        {
            var bounds = surfaceBounds[b];
            var min = bounds.min; var max = bounds.max;
            min.y = bottom; max.y = top;
            bounds.SetMinMax(min, max);
            surfaceBounds[b] = bounds;
        }
    }

    private void ReleaseSurfaceTexture()
    {
        if (surfaceHeights != null)
        {
            if (Application.isPlaying) Destroy(surfaceHeights); else DestroyImmediate(surfaceHeights);
        }
        surfaceHeights = null; uploadedRevision = -1;
    }

    private void CacheCells()
    {
        var field = pile.VisualHeightField;
        pendingCoverage?.Dispose(); pendingCoverage = null;
        coveragePatches.Clear(); coverageMatrices.Clear();
        initialTriangleAreas = new float[field.Heights.Length * 2];
        for (int z = 0; z < field.Resolution - 1; z++)
        for (int x = 0; x < field.Resolution - 1; x++)
        for (int triangle = 0; triangle < 2; triangle++)
        {
            int cell = z * field.Resolution + x;
            CellTriangle(cell, triangle, out var a, out var b, out var c);
            initialTriangleAreas[cell * 2 + triangle] = Vector3.Cross(b - a, c - a).magnitude * .5f;
        }
        previousHeights = (float[])field.Heights.Clone();
        dirtyCells = new bool[previousHeights.Length];
        pendingCells = new int[previousHeights.Length];
        pendingHead = pendingCount = 0; pendingSample = -1;
        activeBatchCounts = (int[])counts.Clone();
        visibleSamples = new bool[samples.Length];
        sampleCells = new int[samples.Length]; sampleBatches = new int[samples.Length]; sampleSlots = new int[samples.Length];
        cellSampleHeads = new int[previousHeights.Length];
        nextCellSample = new int[samples.Length];
        for (int cell = 0; cell < cellSampleHeads.Length; cell++) cellSampleHeads[cell] = -1;
        batchBounds = new Bounds[batches.Length];
        surfaceBounds = new Bounds[batches.Length];
        liveBatches = new bool[batches.Length];
        Array.Clear(paletteCounts, 0, paletteCounts.Length);
        for (int order = 0; order < drawOrder.Length; order++)
        {
            int i = drawOrder[order];
            Vector2 p = samples[i];
            int x = Mathf.Clamp((int)((p.x + field.Size * .5f) / field.Spacing), 0, field.Resolution - 2);
            int z = Mathf.Clamp((int)((p.y + field.Size * .5f) / field.Spacing), 0, field.Resolution - 2);
            sampleCells[i] = z * field.Resolution + x;
            sampleBatches[i] = -1;
            if (!pile.IsInsideBoundary(pile.transform.TransformPoint(new Vector3(p.x, 0, p.y)), .04f)) continue;
            int color = paletteIndices[i], batch = color * batchesPerColor + paletteCounts[color] / 1023, slot = paletteCounts[color]++ % 1023;
            sampleBatches[i] = batch; sampleSlots[i] = slot;
            visibleSamples[i] = field.Sample(p.x, p.y) >= .001f;
            if (!visibleSamples[i]) activeBatchCounts[batch]--;
            int cell = sampleCells[i];
            nextCellSample[i] = cellSampleHeads[cell];
            cellSampleHeads[cell] = i;
            Vector3 position = batches[batch][slot].GetColumn(3);
            if (slot == 0) batchBounds[batch] = new Bounds(position, Vector3.one * .1f);
            else batchBounds[batch].Encapsulate(position);
        }
        // Bounds retain the initial height envelope as excavation only lowers it.
        for (int b = 0; b < batchBounds.Length; b++)
        {
            surfaceBounds[b] = batchBounds[b]; surfaceBounds[b].Expand(.12f);
            liveBatches[b] = activeBatchCounts[b] > 0;
            Vector3 bottom = batchBounds[b].center; bottom.y = pile.transform.position.y;
            batchBounds[b].Encapsulate(bottom); batchBounds[b].Expand(.12f);
            surfaceBounds[b] = batchBounds[b];
        }
        LastUpdatedKernels = KernelCount;
    }

    private void UpdateChangedCells()
    {
        var field = pile.VisualHeightField;
        int n = field.Resolution;
        for (int i = 0; i < previousHeights.Length; i++)
        {
            if (previousHeights[i] == field.Heights[i]) continue;
            previousHeights[i] = field.Heights[i];
            // Include neighbours used by the surface-normal finite differences.
            int x = i % n, z = i / n;
            int reach = Mathf.CeilToInt(.03f / field.Spacing) + 1;
            for (int dz = Mathf.Max(0, z - reach); dz <= Mathf.Min(n - 1, z + reach); dz++)
            for (int dx = Mathf.Max(0, x - reach); dx <= Mathf.Min(n - 1, x + reach); dx++)
            {
                int cell = dz * n + dx;
                if (dirtyCells[cell]) continue;
                dirtyCells[cell] = true;
                pendingCells[(pendingHead + pendingCount) % pendingCells.Length] = cell;
                pendingCount++;
            }
        }
        revision = pile.SurfaceRevision;
        ProcessPendingCells();
    }

    private void ProcessPendingCells()
    {
        var field = pile.VisualHeightField;
        Matrix4x4 localToWorld = transform.localToWorldMatrix, normalMatrix = transform.worldToLocalMatrix.transpose;
        Quaternion yaw = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up);
        LastUpdatedKernels = 0;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        long budget = (long)(System.Diagnostics.Stopwatch.Frequency * UpdateBudgetSeconds);
        // Resume even within a cell. A broad scoop or prolonged settling must not block a frame.
        while (HasPendingSurfaceUpdates)
        {
            if (pendingSample < 0 && pendingCoverage == null)
            {
                int cell = pendingCells[pendingHead];
                pendingHead = (pendingHead + 1) % pendingCells.Length; pendingCount--;
                // A later edit may enqueue this cell again, including samples already processed.
                dirtyCells[cell] = false;
                pendingSample = cellSampleHeads[cell];
                coverageCell = cell;
                coverageMatrices.Clear();
                pendingCoverage = SampleExtraCoverage(cell).GetEnumerator();
            }
            while (pendingSample >= 0)
            {
                int i = pendingSample;
                pendingSample = nextCellSample[i];
                int batch = sampleBatches[i];
                Vector2 p = samples[i];
                float h = field.Sample(p.x, p.y);
                if (!gpuSurface)
                {
                    float dx = pile.VisualSurfaceHeight(p.x + .03f, p.y) - pile.VisualSurfaceHeight(p.x - .03f, p.y);
                    float dz = pile.VisualSurfaceHeight(p.x, p.y + .03f) - pile.VisualSurfaceHeight(p.x, p.y - .03f);
                    Vector3 normal = normalMatrix.MultiplyVector(new Vector3(-dx, .06f, -dz)).normalized;
                    batches[batch][sampleSlots[i]] = Matrix4x4.TRS(localToWorld.MultiplyPoint3x4(new Vector3(p.x, h + pile.GroundOffset(p.x, p.y), p.y)) + Vector3.up * .003f,
                        Quaternion.FromToRotation(Vector3.up, normal) * yaw * rotations[i],
                        new Vector3(.0255f, .014f, .029f) * (h < .001f ? 0 : sizes[i]));
                }
                LastUpdatedKernels++;
                bool visible = h >= .001f;
                if (visibleSamples[i] != visible)
                {
                    activeBatchCounts[batch] += visible ? 1 : -1;
                    KernelCount += visible ? 1 : -1;
                    visibleSamples[i] = visible;
                    liveBatches[batch] = activeBatchCounts[batch] > 0;
                }
                // Initial bounds already span the surface down to ground. Only deposits
                // growing beyond that envelope need expansion; no 1,023-kernel rescan.
                Vector3 position = batches[batch][sampleSlots[i]].GetColumn(3);
                if (visible && !batchBounds[batch].Contains(position))
                {
                    batchBounds[batch].Encapsulate(position + Vector3.up * .06f);
                    surfaceBounds[batch] = batchBounds[batch];
                }
                if (LastUpdatedKernels >= KernelUpdatesPerFrame ||
                    ((LastUpdatedKernels & 63) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() - started >= budget)) return;
            }
            while (pendingCoverage.MoveNext())
            {
                coverageMatrices.Add(pendingCoverage.Current);
                LastUpdatedKernels++;
                if (LastUpdatedKernels >= KernelUpdatesPerFrame ||
                    ((LastUpdatedKernels & 63) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() - started >= budget)) return;
            }
            pendingCoverage.Dispose(); pendingCoverage = null;
            ReplaceCoveragePatch();
            if (System.Diagnostics.Stopwatch.GetTimestamp() - started >= budget) return;
        }
    }

    private void CellTriangle(int cell, int triangle, out Vector3 a, out Vector3 b, out Vector3 c)
    {
        var field = pile.VisualHeightField;
        int n = field.Resolution;
        Vector3 Vertex(int index) => transform.TransformPoint(new Vector3(
            index % n * field.Spacing - field.Size * .5f, field.Heights[index] + pile.GroundOffset(index),
            index / n * field.Spacing - field.Size * .5f));
        a = Vertex(triangle == 0 ? cell : cell + 1);
        b = Vertex(cell + n);
        c = Vertex(triangle == 0 ? cell + 1 : cell + n + 1);
    }

    private IEnumerable<Matrix4x4> SampleExtraCoverage(int cell)
    {
        var field = pile.VisualHeightField;
        if (cell % field.Resolution >= field.Resolution - 1 || cell / field.Resolution >= field.Resolution - 1) yield break;
        for (int triangle = 0; triangle < 2; triangle++)
        {
            CellTriangle(cell, triangle, out var a, out var b, out var c);
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float area = cross.magnitude * .5f;
            float originalArea = initialTriangleAreas[cell * 2 + triangle];
            // Existing grains still cover the original area. Add only the density lost to stretching.
            if (area <= originalArea) continue;
            float desiredCount = (area - originalArea) / (EffectiveSpacing * EffectiveSpacing * .866f);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, cross.normalized);
            if (gpuSurface) slope = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up);
            // Stable barycentric samples: changing the slope must not reshuffle
            // every extra grain. Only the last grain grows/shrinks with density.
            var random = new System.Random(193 + cell * 2 + triangle);
            for (int i = 0; i < Mathf.CeilToInt(desiredCount); i++)
            {
                float u = Mathf.Sqrt((float)random.NextDouble());
                float v = (float)random.NextDouble();
                Vector3 point = a * (1 - u) + b * (u * (1 - v)) + c * (u * v);
                var local = transform.InverseTransformPoint(point);
                Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 28f - 14f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 26f - 13f);
                float size = .9f + (float)random.NextDouble() * .22f;
                if (field.Sample(local.x, local.z) < .001f || !pile.IsInsideBoundary(point, .04f)) continue;
                yield return Matrix4x4.TRS(point + Vector3.up * .003f, slope * rotation,
                    new Vector3(.0255f, .014f, .029f) * (size * Mathf.Clamp01(desiredCount - i)));
            }
        }
    }

    private void ReplaceCoveragePatch()
    {
        if (coveragePatches.TryGetValue(coverageCell, out var previous)) KernelCount -= previous.count;
        if (coverageMatrices.Count == 0) { coveragePatches.Remove(coverageCell); return; }
        var patch = new CoveragePatch { count = coverageMatrices.Count,
            batches = new Matrix4x4[(coverageMatrices.Count + 1022) / 1023][],
            bounds = new Bounds(coverageMatrices[0].GetColumn(3), Vector3.zero) };
        for (int i = 0; i < patch.batches.Length; i++)
        {
            int count = Mathf.Min(1023, coverageMatrices.Count - i * 1023);
            patch.batches[i] = new Matrix4x4[count];
            coverageMatrices.CopyTo(i * 1023, patch.batches[i], 0, count);
        }
        foreach (var matrix in coverageMatrices) patch.bounds.Encapsulate((Vector3)matrix.GetColumn(3));
        patch.bounds.Expand(.12f);
        coveragePatches[coverageCell] = patch;
        KernelCount += patch.count;
    }

    private static IEnumerable<Vector3> SampleTriangle(Vector3 a, Vector3 b, Vector3 c, float step, System.Random random)
    {
        if ((c - b).sqrMagnitude > (b - a).sqrMagnitude && (c - b).sqrMagnitude > (c - a).sqrMagnitude)
        { Vector3 old = a; a = b; b = c; c = old; }
        else if ((c - a).sqrMagnitude > (b - a).sqrMagnitude) { Vector3 old = b; b = c; c = old; }
        float length = (b - a).magnitude;
        if (length < .00001f) yield break;
        Vector3 right = (b - a) / length, up = Vector3.ProjectOnPlane(c - a, right).normalized;
        float cx = Vector3.Dot(c - a, right), cy = Vector3.Dot(c - a, up);
        if (cy < .00001f) yield break;
        float rowStep = step * .866f;
        bool sampled = false;
        for (int row = 0; row < Mathf.CeilToInt(cy / rowStep); row++)
        {
            float y = (row + .5f) * rowStep;
            if (y > cy) continue;
            float x0 = cx * y / cy, x1 = length + (cx - length) * y / cy;
            int columns = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / step));
            for (int col = 0; col < columns; col++)
            {
                float x = x0 + (col + .35f + (float)random.NextDouble() * .3f) * (x1 - x0) / columns;
                sampled = true;
                yield return a + right * x + up * y;
            }
        }
        if (!sampled) yield return (a + b + c) / 3f;
    }

    private void LateUpdate()
    {
        if (rebuildRequested) { rebuildRequested = false; Rebuild(); }
        if (pile != null && (revision != pile.SurfaceRevision || lastTransform != transform.localToWorldMatrix)) Build();
        else if (HasPendingSurfaceUpdates) ProcessPendingCells();
        else LastUpdatedKernels = 0;
        UploadSurface();
        UpdateEdgeCoverage();
    }

    private Vector2[] SampleSurface(float requestedSpacing)
    {
        // Reserve samples across bare cells too, so deposits can reveal them through
        // the same bounded update queue instead of rebuilding the entire pile.
        var field = pile.VisualHeightField;
        var vertices = new Vector3[field.Heights.Length];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = new Vector3(i % field.Resolution * field.Spacing - field.Size * .5f,
                field.Heights[i] + pile.GroundOffset(i), i / field.Resolution * field.Spacing - field.Size * .5f);
        int[] triangles = field.Triangles;
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
            foreach (var point in SampleTriangle(a, b, c, step, random))
            {
                Vector3 local = pile.transform.InverseTransformPoint(point);
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
        if (nearMesh == null) { nearMesh = CreateLodMesh(8); mediumMesh = CreateLodMesh(4); distantMesh = CreateLodMesh(4); }
        GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
        grainAppearance.Set(pile.SurfaceGrain);
        pile.ApplySpatialAppearance(grainAppearance.Block);
        grainAppearance.Block.SetFloat("_UsePileSurface", gpuSurface ? 1 : 0);
        if (gpuSurface)
        {
            grainAppearance.Block.SetTexture("_PileSurfaceHeights", surfaceHeights);
            grainAppearance.Block.SetVector("_PileSurfaceGrid", new Vector4(pile.Size, pile.VisualHeightField.Resolution, 0, 0));
            grainAppearance.Block.SetMatrix("_PileSurfaceWorldToLocal", transform.worldToLocalMatrix);
            grainAppearance.Block.SetMatrix("_PileSurfaceLocalToWorld", transform.localToWorldMatrix);
        }
        LastDrawTriangles = 0;
        for (int i = 0; i < batches.Length; i++)
            if (counts[i] > 0 && (gpuSurface || liveBatches[i]) && GeometryUtility.TestPlanesAABB(cameraPlanes, surfaceBounds[i]))
            {
                float distance = Vector3.Distance(camera.transform.position, surfaceBounds[i].ClosestPoint(camera.transform.position));
                if (distance > drawDistance) continue;
                Mesh lod = distance < fullDetailDistance ? nearMesh : distance < Mathf.Max(fullDetailDistance, mediumDetailDistance) ? mediumMesh : distantMesh;
                LastDrawTriangles += counts[i] * (int)(lod.GetIndexCount(0) / 3);
                var material = colorVariants != null && colorVariants.Length == colorCount && colorVariants[i / batchesPerColor] != null
                    ? colorVariants[i / batchesPerColor] : kernelMaterial;
                DrawBatch(lod, material, batches[i], counts[i], surfaceBounds[i], camera);
            }
        foreach (var entry in coveragePatches)
        {
            var patch = entry.Value;
            var bounds = patch.bounds;
            if (gpuSurface && surfaceBounds.Length > 0)
            {
                var min = bounds.min; var max = bounds.max;
                min.y = surfaceBounds[0].min.y; max.y = surfaceBounds[0].max.y;
                bounds.SetMinMax(min, max);
            }
            if (!GeometryUtility.TestPlanesAABB(cameraPlanes, bounds)) continue;
            float distance = Vector3.Distance(camera.transform.position, bounds.ClosestPoint(camera.transform.position));
            if (distance > drawDistance) continue;
            Mesh lod = distance < fullDetailDistance ? nearMesh : distance < Mathf.Max(fullDetailDistance, mediumDetailDistance) ? mediumMesh : distantMesh;
            for (int i = 0; i < patch.batches.Length; i++)
            {
                var matrices = patch.batches[i];
                int color = (entry.Key + i) % colorCount;
                var material = colorVariants != null && colorVariants.Length == colorCount && colorVariants[color] != null
                    ? colorVariants[color] : kernelMaterial;
                LastDrawTriangles += matrices.Length * (int)(lod.GetIndexCount(0) / 3);
                DrawBatch(lod, material, matrices, matrices.Length, bounds, camera);
            }
        }
        DrawEdges(camera);
    }

    private void DrawEdges(Camera camera)
    {
        if(edgePatches==null)return;
        // These instances already lie on vertical faces. Height-field displacement
        // would otherwise move them back up onto the top of the pile.
        grainAppearance.Block.SetFloat("_UsePileSurface",0);
        for(int edge=0;edge<edgePatches.Length;edge++)
        {
            var patch=edgePatches[edge];
            if(patch==null || !GeometryUtility.TestPlanesAABB(cameraPlanes,patch.bounds))continue;
            float distance=Vector3.Distance(camera.transform.position,patch.bounds.ClosestPoint(camera.transform.position));
            if(distance>drawDistance)continue;
            var lod=distance<fullDetailDistance?nearMesh:distance<mediumDetailDistance?mediumMesh:distantMesh;
            int color=edge%Mathf.Max(1,colorCount);
            var material=colorVariants!=null && color<colorVariants.Length && colorVariants[color]!=null?colorVariants[color]:kernelMaterial;
            foreach(var matrices in patch.batches)
            {
                LastDrawTriangles+=matrices.Length*(int)(lod.GetIndexCount(0)/3);
                DrawBatch(lod,material,matrices,matrices.Length,patch.bounds,camera);
            }
        }
    }

    private void DrawBatch(Mesh lod, Material material, Matrix4x4[] matrices, int count, Bounds bounds, Camera camera)
    {
        var parameters = new RenderParams(material) { worldBounds = bounds, matProps = grainAppearance.Block,
            shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer,
            camera = camera, lightProbeUsage = LightProbeUsage.Off };
        Graphics.RenderMeshInstanced(parameters, lod, 0, matrices, count);
    }

    // Interleave X/Z bits to put neighboring surface samples into neighboring draw batches.
    private static uint SpreadBits(uint value)
    {
        value = (value | value << 8) & 0x00FF00FF;
        value = (value | value << 4) & 0x0F0F0F0F;
        value = (value | value << 2) & 0x33333333;
        return (value | value << 1) & 0x55555555;
    }

    internal static Mesh CreateLodMesh(int sides)
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
        ReleaseSurfaceTexture();
        if (nearMesh != null) { if (Application.isPlaying) Destroy(nearMesh); else DestroyImmediate(nearMesh); }
        if (mediumMesh != null) { if (Application.isPlaying) Destroy(mediumMesh); else DestroyImmediate(mediumMesh); }
        if (distantMesh != null) { if (Application.isPlaying) Destroy(distantMesh); else DestroyImmediate(distantMesh); }
    }
}

