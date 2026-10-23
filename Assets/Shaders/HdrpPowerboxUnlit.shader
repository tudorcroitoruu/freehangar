Shader "Freehangar/HDRP/Powerbox Unlit"
{
    Properties
    {
        _Icon("Icon", 2D) = "white" {}
        _Background("Background", 2D) = "white" {}
        _IconTint("Icon Tint", Color) = (0,0,0,0)
        _IconContrast("Icon Contrast", Range(0.01,5)) = 1
        _IconBrightness("Icon Brightness", Range(0,3)) = 1
        _BackgroundTint("Background Tint", Color) = (1,0,0.055,0)
        _BackgroundContrast("Background Contrast", Range(0,5)) = 1
        _BackgroundBrightness("Background Brightness", Range(0,3)) = 1
        _IconScale("Icon Scale", Float) = 2
        _IconOffset("Icon Offset", Vector) = (0,-1,0,0)
    }
    HLSLINCLUDE
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    TEXTURE2D(_Icon); SAMPLER(sampler_Icon);
    TEXTURE2D(_Background); SAMPLER(sampler_Background);
    CBUFFER_START(UnityPerMaterial)
    float4 _Background_ST, _IconTint, _BackgroundTint, _IconOffset;
    float _IconContrast, _IconBrightness, _BackgroundContrast, _BackgroundBrightness, _IconScale;
    CBUFFER_END
    struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
    Varyings Vert(Attributes i)
    {
        Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.positionCS = TransformObjectToHClip(i.positionOS); o.uv = i.uv; o.color = i.color; return o;
    }
    float4 Frag(Varyings i):SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float4 icon = SAMPLE_TEXTURE2D(_Icon, sampler_Icon, i.uv * _IconScale + _IconOffset.xy);
        float4 bg = SAMPLE_TEXTURE2D(_Background, sampler_Background, i.uv * _Background_ST.xy + _Background_ST.zw);
        float3 color = lerp(_BackgroundBrightness * pow(max(bg.rgb, 0), _BackgroundContrast) * _BackgroundTint.rgb * i.color.rgb,
            pow(max(icon.rgb + _IconTint.rgb, 0), _IconContrast) * _IconBrightness, icon.a);
        return float4(color * GetCurrentExposureMultiplier(), 1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull Back ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
