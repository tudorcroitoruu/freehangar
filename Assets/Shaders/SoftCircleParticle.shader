Shader "Beaver Flow/Soft Circle Particle"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _UseTexture ("Use Particle Texture", Float) = 0
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.75)) = 0.24
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SoftCircleParticle"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _EdgeSoftness;
                float _UseTexture;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                half circleAlpha = 1.0h - smoothstep(
                    saturate(1.0 - _EdgeSoftness),
                    1.0,
                    radius);
                if (_UseTexture > 0.5)
                {
                    half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    input.color *= texel;
                    circleAlpha = 1.0h;
                }
                clip(circleAlpha - 0.001h);
                input.color.a *= circleAlpha;
                return input.color;
            }
            ENDHLSL
        }
    }
}
