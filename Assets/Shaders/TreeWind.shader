Shader "Beaver Flow/Sprite Tree Wind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 6)) = 1.5
        _WindFrequency ("Wind Frequency", Range(0, 4)) = 0.85
        _BaseMovement ("Base Movement", Range(0, 0.35)) = 0.08
        _TopStart ("Upper Bend Start", Range(0, 1)) = 0.18
        [PerRendererData] _GroundCompression ("Ground Compression", Range(0, 1)) = 0
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
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "TreeWind"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WindStrength;
                float _WindSpeed;
                float _WindFrequency;
                float _BaseMovement;
                float _TopStart;
                float _GroundCompression;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 objectOriginWS = TransformObjectToWorld(float3(0, 0, 0));
                float upperMask = smoothstep(_TopStart, 1.0, input.uv.y);
                float bendMask = lerp(_BaseMovement, 1.0, upperMask);
                float phase = _Time.y * _WindSpeed +
                              objectOriginWS.x * _WindFrequency +
                              objectOriginWS.y * 0.17;
                float sway = (sin(phase) +
                              sin(phase * 1.73 + input.uv.y * 4.0) * 0.24) *
                             (1.0 - saturate(_GroundCompression));

                float3 positionOS = input.positionOS;
                positionOS.x += sway * _WindStrength * bendMask;
                positionOS.y += abs(sway) * _WindStrength * 0.025 * upperMask;

                float compression = saturate(_GroundCompression);
                float foliageMask = smoothstep(0.35, 0.82, input.uv.y);
                positionOS.x *= lerp(1.0, 0.66, compression * foliageMask);
                positionOS.y -= compression * foliageMask * 0.035;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv) * input.color;
                return color;
            }
            ENDHLSL
        }
    }
}
