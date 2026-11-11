Shader "Toon/HDRP Particles"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [Toggle] _AlphaClip("Alpha clipping", Float) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 5
        [HideInInspector] _DstBlend("Destination blend", Float) = 10
        [HideInInspector] _ZWrite("Depth write", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.high-definition": "[17.0,18.0]" }
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull [_Cull]
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _AlphaClip;
        CBUFFER_END
        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            output.color = input.color * _BaseColor;
            return output;
        }
        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
            if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
            return color;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Stencil { WriteMask 3 Ref 0 Comp Always Pass Replace }
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
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
