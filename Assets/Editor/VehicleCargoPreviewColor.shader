// Neutral editor-only material preview. Samples source albedo without exposure/specular blowout.
Shader "Hidden/FreeHangar/CargoPreviewColor"
{
    Properties
    {
        _BaseColor("Color",Color)=(1,1,1,1)
        _BaseColorMap("Albedo",2D)="white"{}
        _AlphaCutoff("Alpha Cutoff",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseColorMap); SAMPLER(sampler_BaseColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseColorMap_ST;
                float _AlphaCutoff;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv*_BaseColorMap_ST.xy+_BaseColorMap_ST.zw;return o;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float4 color=SAMPLE_TEXTURE2D(_BaseColorMap,sampler_BaseColorMap,input.uv)*_BaseColor;
                clip(color.a-_AlphaCutoff);
                float light=.55+.45*saturate(dot(normalize(input.normalWS),normalize(float3(.4,.8,-.5))));
                return float4(color.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
