using System;
using UnityEngine;

/// <summary>Mass fractions shared by collection, sorting and the procedural grain shader.</summary>
[Serializable]
public sealed class GrainMixture
{
    [Serializable]
    public struct Part
    {
        public GrainType grain;
        [Min(0)] public float weight;
        public Part(GrainType grain, float weight) { this.grain = grain; this.weight = weight; }
    }
    [SerializeField] private Part[] parts = { new Part(GrainType.Corn, 1) };
    public GrainMixture(params GrainType[] types)
    {
        parts = new Part[types.Length];
        for (int i = 0; i < types.Length; i++) parts[i] = new Part(types[i], 1);
    }
    public GrainMixture() { }
    public float Weight(int type)
    {
        float total = 0, value = 0;
        foreach (var part in parts ?? Array.Empty<Part>())
        {
            if ((int)part.grain < 0 || (int)part.grain >= GrainCargo.TypeCount || !float.IsFinite(part.weight)) continue;
            float w = Mathf.Max(0, part.weight); total += w;
            if ((int)part.grain == type) value += w;
        }
        return total > 0 ? value / total : type == 0 ? 1 : 0;
    }
    public void AddTo(GrainCargo cargo, float kg)
    {
        for (int i = 0; i < GrainCargo.TypeCount; i++) cargo.Add((GrainType)i, kg * Weight(i));
    }
}

[Serializable]
public sealed class GrainLayer
{
    [Tooltip("Kilograms collected before revealing the next layer, across the whole pile. 52 kg = eight full 6.5 kg scoops. The final layer fills the rest of the pile.")]
    [Min(.01f)] public float kilograms = 52;
    public GrainMixture mixture = new GrainMixture();
    public GrainLayer() { }
    public GrainLayer(float kg, params GrainType[] types) { kilograms = kg; mixture = new GrainMixture(types); }
}

/// <summary>Fixed-size cargo; containers accept one species and leave the rest on the tool.</summary>
public sealed class GrainCargo
{
    public const int TypeCount = 10;
    private readonly float[] kilograms = new float[TypeCount];
    public int Revision { get; private set; }
    public float Total { get { float sum = 0; for (int i = 0; i < TypeCount; i++) sum += kilograms[i]; return sum; } }
    public float this[GrainType grain] => (int)grain >= 0 && (int)grain < TypeCount ? kilograms[(int)grain] : 0;
    public GrainType Dominant
    {
        get { int best = 0; for (int i = 1; i < TypeCount; i++) if (kilograms[i] > kilograms[best]) best = i; return (GrainType)best; }
    }
    public void Add(GrainType grain, float kg)
    {
        if ((int)grain < 0 || (int)grain >= TypeCount || !float.IsFinite(kg) || kg <= 0) return;
        kilograms[(int)grain] += kg; Revision++;
    }
    public void Clear() { Array.Clear(kilograms, 0, TypeCount); Revision++; }
    public float PourInto(GrainContainer container, float maximumKg)
    {
        if (container == null || !float.IsFinite(maximumKg) || maximumKg <= 0) return 0;
        GrainType type = container.HasAssignedGrain ? container.AcceptedGrain : Dominant;
        float accepted = container.Add(type, Mathf.Min(this[type], maximumKg));
        if (accepted > 0) { kilograms[(int)type] = Mathf.Max(0, kilograms[(int)type] - accepted); Revision++; }
        return accepted;
    }
    public void TransferTo(GrainCargo destination, float maximumKg)
    {
        float total = Total;
        if (destination == null || destination == this || total <= 0 || maximumKg <= 0) return;
        float fraction = Mathf.Clamp01(maximumKg / total);
        for (int i = 0; i < TypeCount; i++)
        {
            float amount = kilograms[i] * fraction;
            kilograms[i] -= amount;
            destination.Add((GrainType)i, amount);
        }
        Revision++;
    }
    public float Weight(int type) { float total = Total; return total > .000001f ? kilograms[type] / total : type == 0 ? 1 : 0; }
    public GrainType Sample(float seed)
    {
        float remaining = Mathf.Clamp01(seed) * Total;
        if (Total <= .000001f) return GrainType.Corn;
        for (int i = 0; i < TypeCount; i++) { remaining -= kilograms[i]; if (remaining < 0) return (GrainType)i; }
        return Dominant;
    }
}
