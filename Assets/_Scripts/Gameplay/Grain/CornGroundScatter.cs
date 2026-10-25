using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Decorative loose kernels near the outer toe; never part of collectible mass.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CornGroundScatter : MonoBehaviour
{
    [SerializeField] private GrainPile pile;
    [SerializeField] private Mesh kernelMesh;
    [SerializeField] private Material kernelMaterial;
    [SerializeField, Range(.2f, 2f)] private float scatterWidth = .9f;
    [SerializeField, Range(64, 256)] private int perimeterSamples = 192;
    private readonly Matrix4x4[] matrices = new Matrix4x4[1023];
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private Matrix4x4 lastTransform;
    private int revision = -1;
    private float nextRefresh;
    public int KernelCount { get; private set; }

    private void OnEnable()
    {
        // Draw buffers are transient. Recreate them at the mode/enable boundary,
        // even if gameplay time is paused or still at zero on the first frame.
        revision = -1;
        nextRefresh = 0;
        if (pile == null) pile = GetComponent<GrainPile>();
        Rebuild();
        RenderPipelineManager.beginCameraRendering += Draw;
    }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; KernelCount = 0; }
    private void OnValidate() => revision = -1;
    private void LateUpdate()
    {
        if (pile == null) pile = GetComponent<GrainPile>();
        if (pile == null) return;
        // Floor scatter is decoration, so excavating the pile must not rescan the
        // entire perimeter and raycast the floor on every settling revision.
        if (((!Application.isPlaying || revision < 0) && revision != pile.SurfaceRevision || lastTransform != transform.localToWorldMatrix) &&
            (!Application.isPlaying || revision < 0 || Time.time >= nextRefresh)) Rebuild();
    }
    public void Rebuild()
    {
        KernelCount = 0;
        if (pile == null || kernelMesh == null || kernelMaterial == null) return;
        var random = new System.Random(829);
        Vector3 scale = pile.WorldScale;
        float radius = pile.Size * .5f * new Vector2(scale.x, scale.z).magnitude;
        float searchStep = Mathf.Max(.08f, radius / 128f);
        Physics.SyncTransforms();
        for (int ray = 0; ray < perimeterSamples; ray++)
        {
            float angle = (ray + (float)random.NextDouble() * .6f) * Mathf.PI * 2f / perimeterSamples;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float edge = -1;
            for (float distance = 0; distance <= radius; distance += searchStep)
            {
                Vector3 local = pile.transform.InverseTransformPoint(pile.transform.position + direction * distance);
                if (pile.SampleLocalHeight(local.x, local.z) * scale.y > .005f) edge = distance;
            }
            if (edge < 0) continue;
            int count = random.Next(1, 5);
            for (int j = 0; j < count && KernelCount < matrices.Length; j++)
            {
                float offset = .07f + Mathf.Pow((float)random.NextDouble(), 2f) * scatterWidth;
                Vector3 position = pile.transform.position + direction * (edge + searchStep + offset);
                position += Vector3.Cross(Vector3.up, direction) * (((float)random.NextDouble() - .5f) * .13f);
                if (!pile.IsInsideBoundary(position, .04f)) continue;
                Vector3 local = pile.transform.InverseTransformPoint(position);
                if (pile.SampleLocalHeight(local.x, local.z) * scale.y > .003f) continue;
                int found = Physics.RaycastNonAlloc(position + Vector3.up * .35f, Vector3.down, hits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
                if (found == hits.Length) continue;
                float nearest = float.PositiveInfinity; RaycastHit ground = default;
                for (int i = 0; i < found; i++)
                {
                    var hit = hits[i];
                    if (hit.collider.GetComponentInParent<GrainPile>() != null || hit.collider.attachedRigidbody != null ||
                        hit.collider.GetComponentInParent<GrainContainer>() != null || hit.collider.GetComponentInParent<PlayerController>() != null ||
                        hit.normal.y < .7f || hit.point.y > pile.transform.position.y + .12f || hit.distance >= nearest) continue;
                    nearest = hit.distance; ground = hit;
                }
                if (float.IsPositiveInfinity(nearest)) continue;
                float variation = .9f + (float)random.NextDouble() * .22f;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, ground.normal) *
                    Quaternion.Euler((float)random.NextDouble() * 20 - 10, (float)random.NextDouble() * 360, (float)random.NextDouble() * 20 - 10);
                matrices[KernelCount++] = Matrix4x4.TRS(ground.point + ground.normal * (.014f * variation), rotation,
                    new Vector3(.0255f, .014f, .029f) * variation);
            }
        }
        revision = pile.SurfaceRevision; lastTransform = transform.localToWorldMatrix; nextRefresh = Time.time + 1f;
    }
    private void Draw(ScriptableRenderContext context, Camera camera)
    {
        if (KernelCount == 0 || kernelMesh == null || kernelMaterial == null || camera.cameraType == CameraType.Preview) return;
        if ((camera.transform.position - transform.position).sqrMagnitude > 35f * 35f) return;
        Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, matrices, KernelCount, null,
            ShadowCastingMode.Off, true, gameObject.layer, camera, LightProbeUsage.Off);
    }
}
