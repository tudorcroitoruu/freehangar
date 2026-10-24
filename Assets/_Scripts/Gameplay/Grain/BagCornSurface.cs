using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    private float lastFill = -1, lastClose = -1;
    private Matrix4x4 lastTransform;
    private uint randomState;
    public int KernelCount { get; private set; }

    private void OnEnable()
    {
        lastFill = -1;
        if (sack != null) sack.RefreshBindings();
        RenderPipelineManager.beginCameraRendering += Draw;
    }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; KernelCount = 0; }
    private void OnValidate() => lastFill = -1;

    private void LateUpdate()
    {
        if (sack == null || grainSurface == null || kernelMesh == null || kernelMaterial == null) return;
        if (lastFill == sack.Fill && lastClose == sack.Close && lastTransform == grainSurface.transform.localToWorldMatrix) return;
        Rebuild();
    }

    public void Rebuild()
    {
        KernelCount = 0;
        if (sack == null || grainSurface == null) return;
        lastFill = sack.Fill; lastClose = sack.Close; lastTransform = grainSurface.transform.localToWorldMatrix;
        if (sack.Fill < .0001f || sack.Close >= .4f) return;
        if (baked == null) baked = new Mesh { name = "Bag corn surface sampling", hideFlags = HideFlags.DontSave };
        grainSurface.BakeMesh(baked);
        baked.GetVertices(vertices); baked.GetTriangles(triangles, 0);
        if (vertices.Count == 0) return;
        Quaternion frame = Quaternion.FromToRotation(Vector3.up, surfaceUp.normalized);
        Quaternion inverse = Quaternion.Inverse(frame);
        Bounds bounds = new Bounds(inverse * vertices[0], Vector3.zero);
        for (int i = 0; i < vertices.Count; i++) { vertices[i] = inverse * vertices[i]; bounds.Encapsulate(vertices[i]); }
        float sx = grainSurface.transform.TransformVector(frame * Vector3.right).magnitude;
        float sz = grainSurface.transform.TransformVector(frame * Vector3.forward).magnitude;
        if (sx < .0001f || sz < .0001f) return;
        float step = Mathf.Max(spacing, Mathf.Sqrt(bounds.size.x * sx * bounds.size.z * sz / 1800f));
        int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x * sx / step));
        int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z * sz / step));
        randomState = 371;
        float reveal = Mathf.Clamp01(sack.Fill * 30f) * Mathf.Clamp01(1f - sack.Close / .4f);
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
            if (KernelCount < 1023) first[KernelCount] = matrix; else second[KernelCount - 1023] = matrix;
            KernelCount++;
        }
    }

    private float Random01()
    {
        randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
        return (randomState & 0xFFFFFF) / 16777216f;
    }

    private bool Sample(float x, float z, out float height)
    {
        height = float.NegativeInfinity;
        for (int i = 0; i < triangles.Count; i += 3)
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
        Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, first, Mathf.Min(1023, KernelCount), null,
            ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
        if (KernelCount > 1023) Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, second, KernelCount - 1023, null,
            ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
    }
    private void OnDestroy()
    {
        if (baked != null) { if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); }
    }
}
