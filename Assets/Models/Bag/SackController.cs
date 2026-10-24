using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Attach to the Sack FBX root. Fill and closure are independent.</summary>
[DisallowMultipleComponent]
public sealed class SackController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float fill;
    [SerializeField, Range(0f, 1f)] private float close;

    private struct Binding
    {
        public SkinnedMeshRenderer renderer;
        public int fillIndex, closeIndex;
        public bool grain;
        public FitBinding[] corrections;
    }

    private struct FitBinding
    {
        public int index;
        public float left, peak, right, applied;
        public bool needsClosure;
    }

    private readonly List<Binding> bindings = new List<Binding>();
    private float appliedFill = -1f, appliedClose = -1f;

    public float Fill
    {
        get => fill;
        set { fill = Mathf.Clamp01(value); Apply(); }
    }

    public float Close
    {
        get => close;
        set { close = Mathf.Clamp01(value); Apply(); }
    }

    private void OnEnable() => RefreshBindings();

    // Authoring tools also bake disabled sacks without activating their GameObjects.
    public void RefreshBindings()
    {
        CacheRenderers();
        Apply();
    }

    private void CacheRenderers()
    {
        bindings.Clear();
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) continue;
            int fillIndex = FindShape(mesh, "Fill");
            int closeIndex = FindShape(mesh, "Close");
            if (fillIndex < 0 && closeIndex < 0) continue;
            bindings.Add(new Binding {
                renderer = renderer, fillIndex = fillIndex, closeIndex = closeIndex,
                grain = renderer.name.StartsWith("Sack_Grain", StringComparison.Ordinal),
                corrections = FindFitBindings(mesh)
            });
            renderer.localBounds = CalculateMorphBounds(mesh, fillIndex, closeIndex);
        }
        appliedFill = appliedClose = -1f;
    }

    private static Bounds CalculateMorphBounds(Mesh mesh, int fillIndex, int closeIndex)
    {
        if (!mesh.isReadable)
        {
            // Keep Read/Write disabled on memory-constrained targets. Conservative
            // bounds cover this asset's complete morph range without a CPU mesh copy.
            Bounds fallback = mesh.bounds;
            Vector3 size = fallback.size;
            fallback.Expand(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 2.5f);
            return fallback;
        }
        // Work in imported mesh coordinates, including FBX unit/axis conversion.
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length == 0) return mesh.bounds;
        var fillDelta = new Vector3[vertices.Length];
        var closeDelta = new Vector3[vertices.Length];
        if (fillIndex >= 0)
            mesh.GetBlendShapeFrameVertices(fillIndex, 0, fillDelta, null, null);
        if (closeIndex >= 0)
            mesh.GetBlendShapeFrameVertices(closeIndex, 0, closeDelta, null, null);
        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 0; i < vertices.Length; i++)
        {
            bounds.Encapsulate(vertices[i]);
            bounds.Encapsulate(vertices[i] + fillDelta[i]);
            bounds.Encapsulate(vertices[i] + closeDelta[i]);
            bounds.Encapsulate(vertices[i] + fillDelta[i] + closeDelta[i]);
        }
        // Small contour-fitting correctives can extend beyond the endpoint poses.
        bounds.Expand(bounds.size.magnitude * .08f);
        return bounds;
    }

    private static int FindShape(Mesh mesh, string name)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string candidate = mesh.GetBlendShapeName(i);
            if (candidate == name || candidate.EndsWith("." + name, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private static FitBinding[] FindFitBindings(Mesh mesh)
    {
        var result = new List<FitBinding>();
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            int separator = name.LastIndexOf('.');
            if (separator >= 0) name = name.Substring(separator + 1);
            if (!name.StartsWith("FitFill_", StringComparison.Ordinal) &&
                !name.StartsWith("FitClose_", StringComparison.Ordinal)) continue;
            string[] fields = name.Split('_');
            if (fields.Length != 4 || !int.TryParse(fields[1], out int peak) ||
                !int.TryParse(fields[2], out int left) || !int.TryParse(fields[3], out int right)) continue;
            result.Add(new FitBinding {
                index = i, left = left / 1000000f, peak = peak / 1000000f,
                right = right / 1000000f, needsClosure = fields[0] == "FitClose", applied = float.NaN
            });
        }
        return result.ToArray();
    }

    private static float FitWeight(float value, FitBinding fit)
    {
        if (value <= fit.peak)
            return fit.peak == fit.left ? 1f : Mathf.Clamp01((value - fit.left) / (fit.peak - fit.left));
        return fit.right == fit.peak ? 1f : Mathf.Clamp01((fit.right - value) / (fit.right - fit.peak));
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        fill = Mathf.Clamp01(fill);
        close = Mathf.Clamp01(close);
        if (appliedFill == fill && appliedClose == close) return;
        foreach (var binding in bindings)
        {
            if (binding.renderer == null) continue;
            if (binding.fillIndex >= 0)
                binding.renderer.SetBlendShapeWeight(binding.fillIndex, fill * 100f);
            if (binding.closeIndex >= 0)
                binding.renderer.SetBlendShapeWeight(binding.closeIndex, close * 100f);
            for (int i = 0; i < binding.corrections.Length; i++)
            {
                FitBinding correction = binding.corrections[i];
                float weight = FitWeight(fill, correction) * (correction.needsClosure ? close : 1f) * 100f;
                if (correction.applied == weight) continue;
                binding.renderer.SetBlendShapeWeight(correction.index, weight);
                correction.applied = weight;
                binding.corrections[i] = correction;
            }
            if (binding.grain)
                binding.renderer.enabled = fill > .0001f;
        }
        appliedFill = fill;
        appliedClose = close;
    }

    public void SetFill(float amount) => Fill = amount;
    public void SetClosure(float amount) => Close = amount;
    public void CloseSack() => Close = 1f;
    public void OpenSack() => Close = 0f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        fill = Mathf.Clamp01(fill);
        close = Mathf.Clamp01(close);
        appliedFill = appliedClose = -1f;
    }
#endif
}
