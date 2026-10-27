using UnityEngine;

/// <summary>Reusable property block, with no material instantiation or per-frame arrays.</summary>
public sealed class GrainAppearance
{
    private MaterialPropertyBlock block;
    // Native rendering objects must be created on the main thread, not in MonoBehaviour field initializers.
    public MaterialPropertyBlock Block => block ?? (block = new MaterialPropertyBlock());
    private static readonly int WeightsA = Shader.PropertyToID("_GrainWeightsA");
    private static readonly int WeightsB = Shader.PropertyToID("_GrainWeightsB");
    private static readonly int WeightsC = Shader.PropertyToID("_GrainWeightsC");
    public void Set(GrainCargo cargo)
    {
        Block.SetVector(WeightsA, new Vector4(cargo.Weight(0), cargo.Weight(1), cargo.Weight(2), cargo.Weight(3)));
        Block.SetVector(WeightsB, new Vector4(cargo.Weight(4), cargo.Weight(5), cargo.Weight(6), cargo.Weight(7)));
        Block.SetVector(WeightsC, new Vector4(cargo.Weight(8), cargo.Weight(9), 0, 0));
    }
    public void Set(GrainType grain)
    {
        Vector4 a = Vector4.zero, b = Vector4.zero, c = Vector4.zero;
        int index = Mathf.Clamp((int)grain, 0, 9);
        if (index < 4) a[index] = 1; else if (index < 8) b[index - 4] = 1; else c[index - 8] = 1;
        Block.SetVector(WeightsA, a); Block.SetVector(WeightsB, b); Block.SetVector(WeightsC, c);
    }
}
