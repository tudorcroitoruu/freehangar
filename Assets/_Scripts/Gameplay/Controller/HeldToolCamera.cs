using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>URP camera stack with independent held-tool depth.</summary>
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
            toolCamera.enabled = false;
            toolCamera.nearClipPlane = .01f;
        }
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
