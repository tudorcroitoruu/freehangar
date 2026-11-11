Shader "Hidden/Tests/Legacy Lit Properties"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _BaseColorMap("Texture", 2D) = "white" {}
        _Metallic("Metallic", Float) = 0
        _Smoothness("Smoothness", Float) = .5
        _SurfaceType("Surface", Float) = 0
        _DoubleSidedEnable("Double Sided", Float) = 0
        _MaskMap("Mask", 2D) = "white" {}
        _MetallicRemapMin("Metallic Min", Float) = 0
        _MetallicRemapMax("Metallic Max", Float) = 1
        _SmoothnessRemapMin("Smoothness Min", Float) = 0
        _SmoothnessRemapMax("Smoothness Max", Float) = 1
        _MaterialID("Workflow", Float) = 1
    }
    Fallback "Universal Render Pipeline/Unlit"
}
