using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

/// <summary>URP camera stack / HDRP custom pass with independent held-tool depth.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class HeldToolCamera : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera toolCamera;
    [SerializeField] private LayerMask toolLayer;

    private UniversalAdditionalCameraData worldUrp, toolUrp;
    private ShovelTool shovel;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Prepare;
        ConfigureCameras();
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Prepare;
        if (worldUrp != null && worldUrp.renderType == CameraRenderType.Base)
            worldUrp.cameraStack?.Remove(toolCamera);
        if (toolCamera != null) toolCamera.enabled = false;
    }
    private void LateUpdate() => ConfigureCameras();

    public void ConfigureCameras()
    {
        if (worldCamera == null || toolCamera == null || worldCamera == toolCamera) return;
        bool urp = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        bool scooping = false;
        if (urp)
        {
            if (worldUrp == null) worldUrp = worldCamera.GetUniversalAdditionalCameraData();
            if (toolUrp == null) toolUrp = toolCamera.GetUniversalAdditionalCameraData();
            worldUrp.renderType = CameraRenderType.Base;
            toolUrp.renderType = CameraRenderType.Overlay;
            // Stack cameras must use the same renderer. Preserve non-default renderer selections.
            toolUrp.SetRenderer(GetRendererIndex(worldUrp));
            var stack = worldUrp.cameraStack;
            if (stack != null && !stack.Contains(toolCamera)) stack.Add(toolCamera);
            toolUrp.renderPostProcessing = false;
            toolUrp.volumeLayerMask = worldUrp.volumeLayerMask;
            toolUrp.volumeTrigger = worldUrp.volumeTrigger;
            if (shovel == null) shovel = worldCamera.GetComponentInChildren<ShovelTool>(true);
            scooping = shovel != null && shovel.IsScooping;
            // During insertion the base camera renders the tool against real world
            // depth. Outside insertion the overlay clears depth for close-up carrying.
            toolCamera.nearClipPlane = .01f;
            toolCamera.enabled = !scooping;
        }
        else
        {
            if (worldUrp != null && worldUrp.renderType == CameraRenderType.Base)
                worldUrp.cameraStack?.Remove(toolCamera);
            toolCamera.enabled = false; // HDRP renders explicitly through HeldToolPass.
            toolCamera.nearClipPlane = .01f;
        }
        var volume = toolCamera.GetComponent<CustomPassVolume>();
        if (volume != null) volume.enabled = !urp;
        if (urp && scooping) worldCamera.cullingMask |= toolLayer.value;
        else worldCamera.cullingMask &= ~toolLayer.value;
        toolCamera.cullingMask = toolLayer;
        toolCamera.clearFlags = CameraClearFlags.Depth;
        toolCamera.useOcclusionCulling = false;
        toolCamera.targetTexture = null;
        toolCamera.targetDisplay = worldCamera.targetDisplay;
        if (toolCamera.transform.parent == worldCamera.transform)
            toolCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        else toolCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        toolCamera.fieldOfView = worldCamera.fieldOfView;
        toolCamera.aspect = worldCamera.aspect;
        toolCamera.orthographic = worldCamera.orthographic;
        toolCamera.orthographicSize = worldCamera.orthographicSize;
        toolCamera.farClipPlane = worldCamera.farClipPlane;
    }

    private static int GetRendererIndex(UniversalAdditionalCameraData data)
    {
        // Resolve through the pipeline's renderer instances, avoiding private-field reflection.
        var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var renderers = pipeline.rendererDataList;
        for (int i = 0; i < renderers.Length; i++)
            if (pipeline.GetRenderer(i) == data.scriptableRenderer) return i;
        return -1;
    }

    private void Prepare(ScriptableRenderContext context, Camera camera)
    {
        if (camera != worldCamera || toolCamera == null) return;
        ConfigureCameras();
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
