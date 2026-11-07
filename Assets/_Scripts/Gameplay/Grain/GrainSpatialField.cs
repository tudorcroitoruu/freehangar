using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GrainStratum
{
    [Min(.01f)] public float percent = 100;
    public GrainMixture mixture = new GrainMixture();
    public GrainStratum() { }
    public GrainStratum(float percent, params GrainType[] crops) { this.percent = percent; mixture = new GrainMixture(crops); }
}

[Serializable]
public sealed class GrainRegion
{
    [Tooltip("Share of the pile's starting mass. Region percentages normalize to 100; repeated crops are allowed.")]
    [Min(.01f)] public float percent = 20;
    [Tooltip("Local layers, top first. Each percentage is a share of this region's mass.")]
    public GrainStratum[] layers = { new GrainStratum() };
    public GrainRegion() { }
    public GrainRegion(float percent, params GrainType[] crops) { this.percent = percent; layers = new[] { new GrainStratum(100, crops) }; }
}

/// <summary>Per-column strata. Scoop and settling move the same material that the surface displays.</summary>
public sealed class GrainSpatialField
{
    public enum Arrangement { FrontToBack, LeftToRight, Patches, CurvedSections, ChunksAndCurves }
    private struct Segment
    {
        public double volume;
        public float[] weights;
        public Segment(double volume, float[] weights) { this.volume = volume; this.weights = weights; }
    }
    private readonly GrainHeightField field;
    private readonly List<Segment>[] columns;
    private readonly int[] order;
    private readonly Color[][] pixels;
    private readonly Texture2D[] textures = new Texture2D[3];
    private GrainCargo destination;
    internal GrainCargo CollectionFilter;
    private float density;
    private bool dirty = true;
    private readonly List<Segment> sortedLayers = new List<Segment>(32);
    private readonly int[] sortedCrop;
    private readonly double[] collectionRings = new double[5];

    public float CollectionRadius(float x, float z, float minimum, float maximum, Vector3 metric,
        GrainType crop, double requestedVolume)
    {
        Array.Clear(collectionRings, 0, collectionRings.Length);
        float step = (maximum - minimum) / (collectionRings.Length - 1);
        if (step <= 0) return minimum;
        for (int cell = 0; cell < columns.Length; cell++)
        {
            if (field.AreaAt(cell) <= 0) continue;
            float dx = (cell % field.Resolution * field.Spacing - field.Size * .5f - x) * metric.x;
            float dz = (cell / field.Resolution * field.Spacing - field.Size * .5f - z) * metric.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance >= maximum) continue;
            int ring = Mathf.Clamp(Mathf.CeilToInt((distance - minimum) / step), 0, collectionRings.Length - 1);
            foreach (var layer in columns[cell]) collectionRings[ring] += layer.volume * layer.weights[(int)crop];
        }
        double available = 0;
        for (int ring = 0; ring < collectionRings.Length; ring++)
        {
            available += collectionRings[ring];
            if (available >= requestedVolume) return Mathf.Min(maximum, minimum + step * ring + .001f);
        }
        return maximum;
    }

    public bool BringCropToSurface(float x, float z, float radius, Vector3 metric, GrainType crop)
    {
        bool changed = false;
        int type = (int)crop;
        float radiusSquared = radius * radius;
        for (int cell = 0; cell < columns.Length; cell++)
        {
            float dx = (cell % field.Resolution * field.Spacing - field.Size * .5f - x) * metric.x;
            float dz = (cell / field.Resolution * field.Spacing - field.Size * .5f - z) * metric.z;
            if (dx * dx + dz * dz >= radiusSquared || field.AreaAt(cell) <= 0) continue;
            if (sortedCrop[cell] == type) continue;
            var stack = columns[cell];
            bool otherAbove = false, needsSorting = false;
            for (int s = stack.Count - 1; s >= 0; s--)
            {
                var layer = stack[s];
                if (layer.volume <= 0) continue;
                float share = layer.weights[type];
                if (share < 1) otherAbove = true;
                if (share > 0 && otherAbove) { needsSorting = true; break; }
            }
            sortedCrop[cell] = type;
            if (!needsSorting) continue;
            sortedLayers.Clear();
            double selectedVolume = 0;
            foreach (var layer in stack)
            {
                double selected = layer.volume * layer.weights[type];
                selectedVolume += selected;
                if (selected <= 0) { sortedLayers.Add(layer); continue; }
                double remaining = layer.volume - selected;
                if (remaining <= 0) continue;
                var weights = new float[GrainCargo.TypeCount];
                for (int t = 0; t < weights.Length; t++)
                    if (t != type) weights[t] = (float)(layer.weights[t] * layer.volume / remaining);
                sortedLayers.Add(new Segment(remaining, weights));
            }
            var selectedWeights = new float[GrainCargo.TypeCount]; selectedWeights[type] = 1;
            sortedLayers.Add(new Segment(selectedVolume, selectedWeights));
            stack.Clear(); stack.AddRange(sortedLayers);
            changed = true;
        }
        if (changed) dirty = true;
        return changed;
    }
    public double InitialVolume { get; }

    public List<GrainColumnSave> CaptureSave()
    {
        var result = new List<GrainColumnSave>();
        for (int i = 0; i < columns.Length; i++) foreach (var segment in columns[i])
            result.Add(new GrainColumnSave { index = i, volume = segment.volume, weights = (float[])segment.weights.Clone() });
        return result;
    }

    public void RestoreSave(List<GrainColumnSave> saved)
    {
        if (saved == null) return;
        foreach (var column in columns) column.Clear();
        for (int i = 0; i < sortedCrop.Length; i++) sortedCrop[i] = -1;
        foreach (var segment in saved)
            if (segment != null && segment.index >= 0 && segment.index < columns.Length && segment.volume > 0 &&
                double.IsFinite(segment.volume) && segment.weights?.Length == GrainCargo.TypeCount)
                columns[segment.index].Add(new Segment(segment.volume, (float[])segment.weights.Clone()));
        ReconcileHeights();
        dirty = true;
    }

    // Typed stock is authoritative. Old saves and float subtraction can retain a
    // positive height after the last grain segment has already been collected.
    public void ReconcileHeights()
    {
        for (int cell = 0; cell < columns.Length; cell++) SyncHeight(cell);
    }

    private void SyncHeight(int cell)
    {
        float area = field.AreaAt(cell);
        if (area <= 0) return;
        double volume = 0;
        foreach (var segment in columns[cell]) volume += segment.volume;
        field.Heights[cell] = (float)(volume / area);
    }

    public GrainSpatialField(GrainHeightField field, GrainRegion[] regions, Arrangement arrangement, float waviness, int seed, Vector2? sectionCenter = null, float sectionRotation = 0)
    {
        this.field = field;
        int count = field.Heights.Length;
        columns = new List<Segment>[count]; order = new int[count];
        sortedCrop = new int[count];
        for (int i = 0; i < count; i++) sortedCrop[i] = -1;
        pixels = new[] { new Color[count], new Color[count], new Color[count] };
        var scores = new float[count];
        InitialVolume = field.Volume;
        for (int i = 0; i < count; i++)
        {
            columns[i] = new List<Segment>(4); order[i] = i;
            float x = (i % field.Resolution) / (float)(field.Resolution - 1), z = (i / field.Resolution) / (float)(field.Resolution - 1);
            float noise = Mathf.PerlinNoise(x * 3.7f + seed * .137f, z * 3.7f + seed * .219f);
            scores[i] = (arrangement == Arrangement.LeftToRight ? x : z) +
                waviness * (Mathf.Sin(x * 8 + seed) + noise - .5f) + (arrangement == Arrangement.Patches ? noise * .85f : 0);
            if (arrangement == Arrangement.CurvedSections)
            {
                // Angular sectors share one junction. A smooth radial twist bends their
                // borders without fragmenting them into noise patches or horizontal bands.
                Vector2 offset = new Vector2(x, z) - (sectionCenter ?? new Vector2(.55f, .48f));
                float radius = offset.magnitude;
                float bend = waviness * (radius * 3f + radius * radius * Mathf.Sin(seed * .137f));
                scores[i] = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (2 * Mathf.PI)
                    - sectionRotation / 360f + bend, 1);
            }
        }
        Array.Sort(scores, order);
        if (regions == null || regions.Length == 0) regions = new[] { new GrainRegion(100, GrainType.Corn) };
        if (arrangement == Arrangement.ChunksAndCurves)
            OrderChunks(0, count, 0, regions.Length, 0, regions, waviness, seed, sectionRotation);
        double sum = 0;
        foreach (var region in regions) sum += Positive(region?.percent ?? 0);
        var weights = new float[regions.Length][][];
        var shares = new double[regions.Length][];
        for (int r = 0; r < regions.Length; r++)
        {
            var strata = regions[r]?.layers;
            if (strata == null || strata.Length == 0) strata = new[] { new GrainStratum() };
            weights[r] = new float[strata.Length][]; shares[r] = new double[strata.Length];
            double layerSum = 0; foreach (var layer in strata) layerSum += Positive(layer?.percent ?? 0);
            for (int s = 0; s < strata.Length; s++)
            {
                shares[r][s] = Positive(strata[s]?.percent ?? 0) / layerSum;
                weights[r][s] = new float[GrainCargo.TypeCount];
                for (int t = 0; t < GrainCargo.TypeCount; t++) weights[r][s][t] = strata[s]?.mixture != null ? strata[s].mixture.Weight(t) : t == 0 ? 1 : 0;
            }
        }
        int regionIndex = 0;
        double budget = InitialVolume * Positive(regions[0]?.percent ?? 0) / sum;
        foreach (int i in order)
        {
            double remaining = field.Heights[i] * (double)field.AreaAt(i);
            while (remaining > 1e-12)
            {
                if (budget <= 1e-12 && regionIndex < regions.Length - 1)
                {
                    regionIndex++; budget = InitialVolume * Positive(regions[regionIndex]?.percent ?? 0) / sum;
                    continue;
                }
                double part = regionIndex == regions.Length - 1 ? remaining : Math.Min(remaining, budget);
                int previousSegments = columns[i].Count;
                for (int s = shares[regionIndex].Length - 1; s >= 0; s--) Push(i, part * shares[regionIndex][s], weights[regionIndex][s]);
                // A boundary column can straddle regions. Keep the earlier/front region on top,
                // so clearing its exact mass share does not consume the next crop first.
                if (previousSegments > 0)
                {
                    var stack = columns[i];
                    var nextRegion = stack.GetRange(previousSegments, stack.Count - previousSegments);
                    stack.RemoveRange(previousSegments, stack.Count - previousSegments);
                    stack.InsertRange(0, nextRegion);
                }
                remaining -= part; budget -= part;
            }
        }
        field.Removed = Remove;
        field.Transferred = Transfer;
    }
    // Divide each existing area again, making staggered T junctions and broad chunks.
    // Alternating straight and bowed cuts avoids the shared center of radial sectors.
    // The final allocation still consumes exact mass budgets, including boundary columns.
    private void OrderChunks(int start, int count, int firstRegion, int regionCount, int depth,
        GrainRegion[] regions, float bend, int seed, float rotation)
    {
        if (regionCount <= 1 || count <= 1) return;
        int leftRegions = regionCount / 2;
        double totalShare = 0, leftShare = 0;
        for (int r = 0; r < regionCount; r++)
        {
            double share = Positive(regions[firstRegion + r]?.percent ?? 0);
            totalShare += share; if (r < leftRegions) leftShare += share;
        }
        float angle = rotation * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
        float Score(int cell)
        {
            float x = (cell % field.Resolution) / (float)(field.Resolution - 1) - .5f;
            float z = (cell / field.Resolution) / (float)(field.Resolution - 1) - .5f;
            float u = x * cos - z * sin, v = x * sin + z * cos;
            float along = depth % 2 == 0 ? u : v, across = depth % 2 == 0 ? v : u;
            // One branch stays square-edged; its neighbors bulge and bend.
            float curve = depth == 1 && firstRegion == 0 ? 0 : bend;
            return along + curve * Mathf.Sin(across * 6f + firstRegion * 1.7f + seed * .013f);
        }
        Array.Sort(order, start, count, Comparer<int>.Create((a,b) =>
        { int comparison = Score(a).CompareTo(Score(b)); return comparison != 0 ? comparison : a.CompareTo(b); }));
        double volume = 0;
        for (int i = start; i < start + count; i++) volume += field.Heights[order[i]] * (double)field.AreaAt(order[i]);
        double target = volume * leftShare / totalShare, accumulated = 0;
        int split = 0;
        while (split < count - 1 && accumulated < target)
        {
            int cell = order[start + split++];
            accumulated += field.Heights[cell] * (double)field.AreaAt(cell);
        }
        split = Math.Max(1, split);
        OrderChunks(start, split, firstRegion, leftRegions, depth + 1, regions, bend, seed, rotation);
        OrderChunks(start + split, count - split, firstRegion + leftRegions, regionCount - leftRegions, depth + 1, regions, bend, seed, rotation);
    }
    private static double Positive(float value) => float.IsFinite(value) ? Math.Max(.01, value) : .01;
    public void BeginCollection(GrainCargo cargo, float kilogramsPerVolume) { destination = cargo; density = kilogramsPerVolume; }
    public void EndCollection() => destination = null;
    public double MatchingSurfaceVolume(int cell, double requested, GrainCargo allowed)
    {
        var stack = columns[cell];
        if (stack.Count == 0 || requested <= 0) return 0;
        double available = 0;
        for (int s = stack.Count - 1; s >= 0 && available < requested; s--)
        {
            var layer = stack[s];
            if (layer.volume <= 0) continue;
            double share = 0;
            bool otherCrop = false;
            for (int t = 0; t < GrainCargo.TypeCount; t++)
                if (allowed[(GrainType)t] > .0001f) share += layer.weights[t];
                else if (layer.weights[t] > 0) otherCrop = true;
            available += Math.Min(requested - available, layer.volume * share);
            // Consecutive deposits of the same crop are one collectible surface,
            // not tiny caps requiring a new stroke for every settling segment.
            if (otherCrop) break;
        }
        return available;
    }
    public double CompatibleSurfaceVolume(int cell, double requested, GrainCargo cargo)
    {
        double result = 0;
        var stack = columns[cell];
        for (int s = stack.Count - 1; s >= 0 && result < requested; s--)
        {
            var segment = stack[s];
            for (int t = 0; t < GrainCargo.TypeCount; t++)
                if (segment.weights[t] > 0f && cargo[(GrainType)t] <= .0001f) return result;
            result += Math.Min(requested - result, segment.volume);
        }
        return result;
    }
    public bool CanCollectInto(int cell, double volume, GrainCargo cargo)
    {
        var stack = columns[cell];
        for (int s = stack.Count - 1; s >= 0 && volume > 1e-12; s--)
        {
            var segment = stack[s];
            double taken = Math.Min(volume, segment.volume);
            if (taken > 0)
                for (int t = 0; t < GrainCargo.TypeCount; t++)
                    if (segment.weights[t] > 0f && cargo[(GrainType)t] <= .0001f) return false;
            volume -= taken;
        }
        return true;
    }
    private void Remove(int cell, double volume) => Take(cell, volume, -1);
    private void Transfer(int from, int to, double volume) => Take(from, volume, to);
    private void Take(int cell, double volume, int receiver)
    {
        var stack = columns[cell];
        if (receiver < 0 && CollectionFilter != null && stack.Count > 0)
        {
            while (volume > 0 && stack.Count > 0)
            {
            int top = stack.Count - 1;
            var segment = stack[top];
            if (segment.volume <= 0) { stack.RemoveAt(top); continue; }
            double share = 0;
            for (int t = 0; t < GrainCargo.TypeCount; t++)
                if (CollectionFilter[(GrainType)t] > .0001f) share += segment.weights[t];
            double taken = Math.Min(volume, segment.volume * share);
            if (taken <= 0 || share <= 0) break;
            double remaining = segment.volume - taken;
            // Weights may be shared with other columns; replace rather than mutate.
            var weights = share == 1 ? segment.weights : new float[GrainCargo.TypeCount];
            for (int t = 0; t < GrainCargo.TypeCount; t++)
            {
                double amount = CollectionFilter[(GrainType)t] > .0001f ? taken * segment.weights[t] / share : 0;
                destination?.Add((GrainType)t, (float)(amount * density));
                if (share != 1) weights[t] = remaining > 1e-12 ? (float)Math.Max(0, (segment.volume * segment.weights[t] - amount) / remaining) : 0;
            }
            if (remaining <= 1e-12) stack.RemoveAt(top);
            else stack[top] = new Segment(remaining, weights);
            volume -= taken;
            if (remaining > 1e-12) break;
            }
            SyncHeight(cell);
            dirty = true;
            return;
        }
        while (volume > 1e-12 && stack.Count > 0)
        {
            int top = stack.Count - 1; var segment = stack[top];
            double taken = Math.Min(volume, segment.volume);
            if (receiver >= 0) Push(receiver, taken, segment.weights);
            else if (destination != null)
                for (int t = 0; t < GrainCargo.TypeCount; t++) destination.Add((GrainType)t, (float)(taken * density * segment.weights[t]));
            segment.volume -= taken; volume -= taken;
            if (segment.volume <= 1e-12) stack.RemoveAt(top); else stack[top] = segment;
        }
        dirty = true;
    }
    private void Push(int cell, double volume, float[] weights)
    {
        if (volume <= 1e-12) return;
        sortedCrop[cell] = -1;
        var stack = columns[cell]; int top = stack.Count - 1;
        if (top >= 0 && ReferenceEquals(stack[top].weights, weights))
        { var segment = stack[top]; segment.volume += volume; stack[top] = segment; }
        else stack.Add(new Segment(volume, weights));
        // Bound settling history while retaining every crop's mass. Only deeply buried segments merge.
        if (stack.Count > 32)
        {
            var a = stack[0]; var b = stack[1]; var mix = new float[GrainCargo.TypeCount];
            for (int t = 0; t < mix.Length; t++) mix[t] = (float)((a.weights[t] * a.volume + b.weights[t] * b.volume) / (a.volume + b.volume));
            stack[0] = new Segment(a.volume + b.volume, mix); stack.RemoveAt(1);
        }
    }
    public void Deposit(int cell, double volume, float[] weights)
    {
        Push(cell, volume, weights);
        dirty = true;
    }
    public double VolumeOf(GrainType grain)
    {
        double sum = 0; foreach (var column in columns) foreach (var layer in column) sum += layer.volume * layer.weights[(int)grain]; return sum;
    }
    public void PreviewClear(float percent)
    {
        double remaining = InitialVolume * Mathf.Clamp01(percent / 100);
        foreach (int cell in order)
        {
            if (remaining <= 0) break;
            float area = field.AreaAt(cell); if (area <= 0) continue;
            double volume = Math.Min(remaining, field.Heights[cell] * (double)area);
            Take(cell, volume, -1); field.Heights[cell] = Mathf.Max(0, field.Heights[cell] - (float)(volume / area)); remaining -= volume;
        }
    }
    public void Sample(float x, float z, GrainCargo cargo)
    {
        cargo.Clear();
        float gx = Mathf.Clamp((x / field.Size + .5f) * (field.Resolution - 1), 0, field.Resolution - 1);
        float gz = Mathf.Clamp((z / field.Size + .5f) * (field.Resolution - 1), 0, field.Resolution - 1);
        int ix = Mathf.Min((int)gx, field.Resolution - 2), iz = Mathf.Min((int)gz, field.Resolution - 2);
        float u = gx - ix, v = gz - iz;
        AddTop(iz * field.Resolution + ix, (1-u)*(1-v), cargo); AddTop(iz * field.Resolution + ix + 1, u*(1-v), cargo);
        AddTop((iz+1)*field.Resolution+ix, (1-u)*v, cargo); AddTop((iz+1)*field.Resolution+ix+1, u*v, cargo);
    }
    public void SampleNearestAvailable(float x, float z, float radius, Vector3 metric, GrainCargo cargo)
    {
        cargo.Clear();
        double best = radius * radius;
        int nearest = -1;
        for (int i = 0; i < columns.Length; i++)
        {
            if (columns[i].Count == 0 || field.Heights[i] <= .000001f || field.AreaAt(i) <= 0) continue;
            float dx = (i % field.Resolution * field.Spacing - field.Size * .5f - x) * metric.x;
            float dz = (i / field.Resolution * field.Spacing - field.Size * .5f - z) * metric.z;
            double distance = dx * dx + dz * dz;
            if (distance >= best) continue;
            best = distance; nearest = i;
        }
        if (nearest >= 0) AddTop(nearest, 1, cargo);
    }
    private void AddTop(int cell, float amount, GrainCargo cargo)
    {
        var stack = columns[cell]; if (stack.Count == 0) return;
        var weights = stack[stack.Count - 1].weights;
        for (int t = 0; t < GrainCargo.TypeCount; t++) cargo.Add((GrainType)t, weights[t] * amount);
    }
    public void Bind(MaterialPropertyBlock block, Transform transform)
    {
        if (dirty)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                var stack = columns[i]; var w = stack.Count > 0 ? stack[stack.Count-1].weights : null;
                for (int channel = 0; channel < 3; channel++)
                {
                    Color color = Color.clear;
                    for (int c = 0; c < 4; c++) if (w != null && channel*4+c < GrainCargo.TypeCount) color[c] = w[channel*4+c];
                    pixels[channel][i] = color;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                if (textures[i] == null) textures[i] = new Texture2D(field.Resolution, field.Resolution, TextureFormat.RGBAHalf, false, true)
                { name = "Spatial grain weights " + i, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                textures[i].filterMode = FilterMode.Bilinear;
                textures[i].SetPixels(pixels[i]); textures[i].Apply(false, false);
            }
            dirty = false;
        }
        block.SetFloat("_UseSpatialGrain", 1);
        block.SetMatrix("_GrainWorldToLocal", transform.worldToLocalMatrix);
        block.SetVector("_GrainGrid", new Vector4(field.Size, field.Resolution, 0, 0));
        block.SetTexture("_GrainMapA", textures[0]); block.SetTexture("_GrainMapB", textures[1]); block.SetTexture("_GrainMapC", textures[2]);
    }
    public void Dispose()
    {
        field.Removed = null; field.Transferred = null;
        ReleaseTextures();
    }
    public void ReleaseTextures()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(textures[i]); else UnityEngine.Object.DestroyImmediate(textures[i]); }
            textures[i] = null;
        }
        dirty = true;
    }
}
