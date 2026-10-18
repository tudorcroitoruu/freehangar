Shader "Beaver Flow/Sprite Flowing Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FlowDirection ("Flow Direction (XY)", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed", Range(-3, 3)) = 0.35
        _WaveScale ("Wave Scale", Range(1, 20)) = 6
        _Distortion ("Texture Distortion", Range(0, 0.05)) = 0.008
        _WaveBrightness ("Wave Brightness", Range(0, 1)) = 0.2
        _HighlightColor ("Wave Highlight", Color) = (0.75, 0.95, 1, 1)
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
            Name "FlowingWater"

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
                float4 _FlowDirection;
                float _FlowSpeed;
                float _WaveScale;
                float _Distortion;
                float _WaveBrightness;
                half4 _HighlightColor;
            CBUFFER_END

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
                const float Tau = 6.28318530718;
                float directionLength = length(_FlowDirection.xy);
                float2 flowDirection = directionLength > 0.0001
                    ? _FlowDirection.xy / directionLength
                    : float2(1, 0);
                float2 waveNormal = float2(-flowDirection.y, flowDirection.x);
                float travel = dot(input.uv, flowDirection) * _WaveScale -
                               _Time.y * _FlowSpeed;
                float crossWave = dot(input.uv, waveNormal) *
                                  (_WaveScale * 0.68) +
                                  _Time.y * _FlowSpeed * 0.61;
                float primaryWave = sin(travel * Tau);
                float secondaryWave = sin(
                    (crossWave + primaryWave * 0.12) * Tau);
                float distortion =
                    (primaryWave + secondaryWave * 0.45) * _Distortion;
                float2 sampleUv = input.uv + waveNormal * distortion;

                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    sampleUv) * input.color;
                float crest = saturate(primaryWave * 0.5 + 0.5);
                crest *= saturate(secondaryWave * 0.5 + 0.5);
                crest = smoothstep(0.58, 0.95, crest) * _WaveBrightness;
                color.rgb = lerp(color.rgb, _HighlightColor.rgb, crest);
                return color;
            }
            ENDHLSL
        }
    }
}
