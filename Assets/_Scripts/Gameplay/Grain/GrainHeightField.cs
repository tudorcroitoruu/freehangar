using System;
using UnityEngine;

/// <summary>Metres and cubic metres. A fixed grid; no individual grain physics.</summary>
public sealed class GrainHeightField
{
    public int Resolution { get; }
    public float Size { get; }
    public float Spacing { get; }
    public float[] Heights { get; }
    public int[] Triangles { get; }
    private readonly float[] areas;
    private readonly float[] removal;
    private readonly float[] referenceHeights;
    private readonly bool[] disturbed;
    private int minX, minZ, maxX, maxZ;
    public Action<int, double> Removed;
    public Action<int, int, double> Transferred;
    public float AreaAt(int index) => areas[index];

    public GrainHeightField(int resolution, float size, float height, Func<float, float, float> profile = null)
    {
        Resolution = Mathf.Clamp(resolution, 9, 129);
        Size = Mathf.Max(1f, size);
        Spacing = Size / (Resolution - 1);
        Heights = new float[Resolution * Resolution];
        areas = new float[Heights.Length];
        removal = new float[Heights.Length];
        referenceHeights = new float[Heights.Length];
        disturbed = new bool[Heights.Length];
        minX = minZ = Resolution;
        Triangles = new int[(Resolution - 1) * (Resolution - 1) * 6];
        int t = 0;
        for (int z = 0; z < Resolution - 1; z++)
        for (int x = 0; x < Resolution - 1; x++)
        {
            int i = z * Resolution + x;
            Triangles[t++] = i; Triangles[t++] = i + Resolution; Triangles[t++] = i + 1;
            Triangles[t++] = i + 1; Triangles[t++] = i + Resolution; Triangles[t++] = i + Resolution + 1;
        }
        // Integrating each triangle's linear surface gives this exact volume weight.
        foreach (int i in Triangles) areas[i] += Spacing * Spacing / 6f;
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            float px = x * Spacing - Size * .5f, pz = z * Spacing - Size * .5f;
            float radius = new Vector2(px, pz).magnitude;
            Heights[z * Resolution + x] = profile != null ? Mathf.Max(0, profile(px, pz)) :
                Mathf.Max(0f, height) * Mathf.Clamp01(1f - radius / (Size * .45f));
        }
        Array.Copy(Heights, referenceHeights, Heights.Length);
    }

    public double Volume
    {
        get
        {
            double volume = 0;
            for (int i = 0; i < Heights.Length; i++) volume += Heights[i] * (double)areas[i];
            return volume;
        }
    }

    public void SetAreaWeights(float[] clippedAreas) => Array.Copy(clippedAreas, areas, areas.Length);

    public float Sample(float x, float z)
    {
        float gx = (x + Size * .5f) / Spacing;
        float gz = (z + Size * .5f) / Spacing;
        if (gx < 0 || gz < 0 || gx > Resolution - 1 || gz > Resolution - 1) return 0;
        int ix = Mathf.Min((int)gx, Resolution - 2), iz = Mathf.Min((int)gz, Resolution - 2), i = iz * Resolution + ix;
        float u = gx - ix, v = gz - iz;
        // Same diagonal as the rendered and collision mesh, not bilinear interpolation.
        if (u + v <= 1f)
            return Heights[i] * (1f - u - v) + Heights[i + 1] * u + Heights[i + Resolution] * v;
        return Heights[i + Resolution + 1] * (u + v - 1f) +
            Heights[i + Resolution] * (1f - u) + Heights[i + 1] * (1f - v);
    }

    public float Scoop(float x, float z, float radius, float depth, float maxVolume, float settlingMargin = .45f, Vector3 metric = default)
    {
        if (!(radius > 0) || !(depth > 0) || !(maxVolume > 0) ||
            float.IsInfinity(maxVolume) || float.IsInfinity(radius) || float.IsNaN(x) || float.IsNaN(z)) return 0;
        Array.Clear(removal, 0, removal.Length);
        if (metric == default) metric = Vector3.one;
        double available = 0;
        for (int iz = 0; iz < Resolution; iz++)
        for (int ix = 0; ix < Resolution; ix++)
        {
            float dx = ix * Spacing - Size * .5f - x, dz = iz * Spacing - Size * .5f - z;
            float weight = Mathf.Clamp01(1f - (dx * dx * metric.x * metric.x + dz * dz * metric.z * metric.z) / (radius * radius));
            int i = iz * Resolution + ix;
            if (areas[i] <= .00000001f) continue;
            removal[i] = Mathf.Min(Heights[i], depth * weight);
            available += removal[i] * (double)areas[i];
        }
        if (available <= 0) return 0;
        float fraction = (float)Math.Min(1, maxVolume / available);
        double removed = 0;
        for (int i = 0; i < Heights.Length; i++)
        {
            float previous = Heights[i];
            Heights[i] = Mathf.Max(0, previous - removal[i] * fraction);
            if (previous > Heights[i]) Removed?.Invoke(i, (previous - Heights[i]) * (double)areas[i]);
            removed += (previous - Heights[i]) * (double)areas[i];
        }
        if (removed > 0) WakeNeighborhood(x, z, radius + Mathf.Max(0, settlingMargin), metric);
        return (float)removed;
    }

    private void WakeNeighborhood(float x, float z, float radius, Vector3 metric)
    {
        int left = Mathf.Clamp(Mathf.FloorToInt((x - radius / metric.x + Size * .5f) / Spacing), 0, Resolution - 1);
        int right = Mathf.Clamp(Mathf.CeilToInt((x + radius / metric.x + Size * .5f) / Spacing), 0, Resolution - 1);
        int bottom = Mathf.Clamp(Mathf.FloorToInt((z - radius / metric.z + Size * .5f) / Spacing), 0, Resolution - 1);
        int top = Mathf.Clamp(Mathf.CeilToInt((z + radius / metric.z + Size * .5f) / Spacing), 0, Resolution - 1);
        minX = Mathf.Min(minX, left); maxX = Mathf.Max(maxX, right);
        minZ = Mathf.Min(minZ, bottom); maxZ = Mathf.Max(maxZ, top);
        for (int iz = bottom; iz <= top; iz++)
        for (int ix = left; ix <= right; ix++)
        {
            float dx = ix * Spacing - Size * .5f - x, dz = iz * Spacing - Size * .5f - z;
            if (dx * dx * metric.x * metric.x + dz * dz * metric.z * metric.z <= radius * radius) disturbed[iz * Resolution + ix] = true;
        }
    }

    public bool Settle(float angleDegrees, float fraction, Vector3 metric = default)
    {
        if (minX > maxX || minZ > maxZ) return false;
        if (metric == default) metric = Vector3.one;
        float threshold = Mathf.Tan(Mathf.Clamp(angleDegrees, 1f, 80f) * Mathf.Deg2Rad) * Spacing / metric.y;
        bool changed = false;
        // An authored pile starts at rest. Only scoop-disturbed neighborhoods can move.
        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            int i = z * Resolution + x;
            if (x < Resolution - 1) changed |= Transfer(i, i + 1, threshold * metric.x, fraction);
            if (z < Resolution - 1) changed |= Transfer(i, i + Resolution, threshold * metric.z, fraction);
            if (x < Resolution - 1 && z < Resolution - 1)
            {
                float diagonal = threshold * new Vector2(metric.x, metric.z).magnitude;
                changed |= Transfer(i, i + Resolution + 1, diagonal, fraction);
                changed |= Transfer(i + 1, i + Resolution, diagonal, fraction);
            }
        }
        if (!changed)
        {
            Array.Clear(disturbed, 0, disturbed.Length);
            minX = minZ = Resolution;
            maxX = maxZ = 0;
        }
        return changed;
    }

    private bool Transfer(int a, int b, float threshold, float fraction)
    {
        if (areas[a] <= .00000001f || areas[b] <= .00000001f) return false;
        if (!disturbed[a] || !disturbed[b]) return false;
        float difference = Heights[a] - Heights[b];
        if (Mathf.Abs(difference) <= .0001f) return false;
        int high = difference > 0 ? a : b, low = difference > 0 ? b : a;
        float donorDeficit = referenceHeights[high] - Heights[high];
        float receiverDeficit = referenceHeights[low] - Heights[low];
        // Spread the missing material locally instead of flattening the original slope.
        // The reference is a ceiling/shape guide, never a source of replacement grain.
        float retainedDrop = Mathf.Min(.0025f, threshold * .02f);
        float relief = receiverDeficit - donorDeficit - retainedDrop;
        if (relief <= .0001f) return false;
        // A lower neighbor cannot feed uphill, even when it has more material remaining.
        float excess = Mathf.Min(relief, Mathf.Abs(difference));
        float volume = excess / (1f / areas[high] + 1f / areas[low]) * Mathf.Clamp01(fraction);
        volume = Mathf.Min(volume, Heights[high] * areas[high]);
        if (volume <= .000000001f) return false;
        Transferred?.Invoke(high, low, volume);
        Heights[high] -= volume / areas[high];
        Heights[low] += volume / areas[low];
        return true;
    }
}
