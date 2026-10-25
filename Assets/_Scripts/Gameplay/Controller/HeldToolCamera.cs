using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>HDRP viewmodel camera. Its custom pass renders over the world with independent depth.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class HeldToolCamera : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera toolCamera;
    [SerializeField] private LayerMask toolLayer;

    private void OnEnable() => RenderPipelineManager.beginCameraRendering += Prepare;
    private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Prepare;

    private void Prepare(ScriptableRenderContext context, Camera camera)
    {
        if (camera != worldCamera || toolCamera == null) return;
        // Rendered explicitly by HeldToolPass, not as a second full HDRP scene.
        toolCamera.enabled = false;
        toolCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        toolCamera.fieldOfView = worldCamera.fieldOfView;
        toolCamera.aspect = worldCamera.aspect;
        toolCamera.orthographic = worldCamera.orthographic;
        toolCamera.orthographicSize = worldCamera.orthographicSize;
        toolCamera.nearClipPlane = .01f;
        toolCamera.farClipPlane = worldCamera.farClipPlane;
        toolCamera.cullingMask = toolLayer;
    }
}

[System.Serializable]
public sealed class HeldToolPass : CustomPass
{
    private static readonly int AmbientOcclusionTexture = Shader.PropertyToID("_AmbientOcclusionTexture");
    public Camera worldCamera;
    public Camera toolCamera;
    public LayerMask toolLayer;

    protected override void AggregateCullingParameters(ref ScriptableCullingParameters parameters, HDCamera camera)
    {
        if (camera.camera != worldCamera) return;
        parameters.cullingMask |= (uint)toolLayer.value;
        parameters.cullingOptions &= ~CullingOptions.OcclusionCull;
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (ctx.hdCamera.camera != worldCamera || toolCamera == null) return;
        // Screen-space AO describes world geometry behind the overlay, not the tool.
        // Sampling it here prints the background onto otherwise opaque materials.
        Texture worldAO = Shader.GetGlobalTexture(AmbientOcclusionTexture);
        ctx.cmd.SetGlobalTexture(AmbientOcclusionTexture, TextureXR.GetBlackTexture());
        // Preserve scene colour and scene depth. Only clear the separate tool depth,
        // so the shovel can occlude its own contents without intersecting world geometry.
        CustomPassUtils.RenderFromCamera(ctx, toolCamera, ctx.cameraColorBuffer,
            ctx.customDepthBuffer.Value, ClearFlag.Depth, toolLayer,
            overrideRenderState: new RenderStateBlock(RenderStateMask.Depth)
            { depthState = new DepthState(true, CompareFunction.LessEqual) });
        if (worldAO != null) ctx.cmd.SetGlobalTexture(AmbientOcclusionTexture, worldAO);
    }
}
