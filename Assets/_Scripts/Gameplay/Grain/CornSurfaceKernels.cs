using System;
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
        int side = Mathf.CeilToInt(pile.Size / actualSpacing);
        if (samples == null)
        {
            samples = new Vector2[side * side];
            rotations = new Quaternion[samples.Length];
            sizes = new float[samples.Length];
            colorCount = colorVariants != null && colorVariants.Length > 0 ? colorVariants.Length : 1;
            paletteCounts = new int[colorCount];
            paletteIndices = new int[samples.Length];
            batchesPerColor = ((samples.Length + colorCount - 1) / colorCount + 1022) / 1023;
            batches = new Matrix4x4[batchesPerColor * colorCount][];
            counts = new int[batches.Length];
            for (int i = 0; i < batches.Length; i++) batches[i] = new Matrix4x4[1023];
            var random = new System.Random(731);
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                int i = z * side + x;
                samples[i] = new Vector2((x + .05f + (float)random.NextDouble() * .9f) * actualSpacing - pile.Size * .5f,
                    (z + .05f + (float)random.NextDouble() * .9f) * actualSpacing - pile.Size * .5f);
                rotations[i] = Quaternion.Euler((float)random.NextDouble() * 28f - 14f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 26f - 13f);
                sizes[i] = .9f + (float)random.NextDouble() * .22f;
                paletteIndices[i] = i % colorCount;
            }
            // Keep equal batch budgets without repeating the three tones in visible rows.
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
            float height = pile.SampleLocalHeight(p.x, p.y);
            if (height < .001f) continue;
            float dx = pile.SampleLocalHeight(p.x + .03f, p.y) - pile.SampleLocalHeight(p.x - .03f, p.y);
            float dz = pile.SampleLocalHeight(p.x, p.y + .03f) - pile.SampleLocalHeight(p.x, p.y - .03f);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, new Vector3(-dx, .06f, -dz).normalized);
            int color = paletteIndices[i];
            int batch = color * batchesPerColor + paletteCounts[color] / 1023, slot = paletteCounts[color] % 1023;
            batches[batch][slot] = transform.localToWorldMatrix * Matrix4x4.TRS(new Vector3(p.x, height + .003f, p.y),
                slope * rotations[i], new Vector3(.0255f, .014f, .029f) * sizes[i]);
            paletteCounts[color]++;
            counts[batch]++;
            KernelCount++;
        }
        revision = pile.SurfaceRevision;
        lastTransform = transform.localToWorldMatrix;
    }

    private void LateUpdate()
    {
        if (pile != null && (revision != pile.SurfaceRevision || lastTransform != transform.localToWorldMatrix)) Build();
    }

    private void Draw(ScriptableRenderContext context, Camera camera)
    {
        if (batches == null || kernelMesh == null || kernelMaterial == null || camera.cameraType == CameraType.Preview ||
            (camera.transform.position - transform.position).sqrMagnitude > drawDistance * drawDistance) return;
        for (int i = 0; i < batches.Length; i++)
            if (counts[i] > 0)
                Graphics.DrawMeshInstanced(kernelMesh, 0,
                    colorVariants != null && colorVariants.Length == colorCount && colorVariants[i / batchesPerColor] != null
                        ? colorVariants[i / batchesPerColor] : kernelMaterial, batches[i], counts[i], null,
                    ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
    }
}
