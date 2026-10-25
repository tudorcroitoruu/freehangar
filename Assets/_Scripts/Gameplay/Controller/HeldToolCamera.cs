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
    private ShovelTool shovel;

    protected override void AggregateCullingParameters(ref ScriptableCullingParameters parameters, HDCamera camera)
    {
        if (camera.camera != worldCamera) return;
        parameters.cullingMask |= (uint)toolLayer.value;
        parameters.cullingOptions &= ~CullingOptions.OcclusionCull;
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (ctx.hdCamera.camera != worldCamera || toolCamera == null) return;
        // Neither draw path populated the world AO buffer with the shovel's geometry.
        // Apply the same neutral AO during insertion, or the pile is imprinted on the metal.
        Texture worldAO = Shader.GetGlobalTexture(AmbientOcclusionTexture);
        ctx.cmd.SetGlobalTexture(AmbientOcclusionTexture, TextureXR.GetBlackTexture());
        if (shovel == null) shovel = worldCamera.GetComponentInChildren<ShovelTool>();
        if (shovel != null && shovel.IsScooping)
        {
            // The cutting blade and its load must disappear behind the pile while buried.
            // Use the world camera's projection with its existing depth for this action.
            CustomPassUtils.RenderFromCamera(ctx, worldCamera, ctx.cameraColorBuffer,
                ctx.cameraDepthBuffer, ClearFlag.None, toolLayer,
                overrideRenderState: new RenderStateBlock(RenderStateMask.Depth)
                { depthState = new DepthState(true, CompareFunction.LessEqual) });
        }
        else
        {
            // Only clear the separate tool depth during ordinary carrying/pouring.
            CustomPassUtils.RenderFromCamera(ctx, toolCamera, ctx.cameraColorBuffer,
                ctx.customDepthBuffer.Value, ClearFlag.Depth, toolLayer,
                overrideRenderState: new RenderStateBlock(RenderStateMask.Depth)
                { depthState = new DepthState(true, CompareFunction.LessEqual) });
        }
        if (worldAO != null) ctx.cmd.SetGlobalTexture(AmbientOcclusionTexture, worldAO);
    }
}
