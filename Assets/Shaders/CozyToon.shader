Shader "Tiny Up/Cozy Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Color Atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(Toon Lighting)]
        [HDR] _LitColor("Sunlit Tint", Color) = (1, 0.96, 0.88, 1)
        _ShadowColor("Shade Tint", Color) = (0.52, 0.55, 0.64, 1)
        _AmbientColor("Ambient Fill", Color) = (0.88, 0.76, 0.68, 1)
        _AmbientStrength("Ambient Fill Strength", Range(0, 0.5)) = 0.18
        _LightBands("Light Bands", Range(2, 5)) = 3
        _LightBias("Light Bias", Range(-0.5, 0.5)) = 0.08
        _BandSoftness("Band Softness", Range(0.001, 0.25)) = 0.045
        [Toggle] _ReceiveShadows("Receive Realtime Shadows", Float) = 1

        [Header(Painted Highlights)]
        [HDR] _SpecColor("Highlight Tint", Color) = (1, 0.88, 0.66, 1)
        _SpecularStrength("Highlight Strength", Range(0, 1)) = 0.12
        _SpecularSize("Highlight Size", Range(0.01, 0.75)) = 0.18

        [Header(Silhouette)]
        _EdgeColor("Edge Tint", Color) = (0.38, 0.28, 0.25, 1)
        _EdgeStrength("Edge Darkening", Range(0, 1)) = 0.2
        _EdgePower("Edge Width", Range(0.25, 8)) = 2

        [Header(Rendering)]
        [Toggle] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        // These aliases preserve values when switching from URP Lit/Simple Lit.
        [HideInInspector] _MainTex("Base Map", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _BumpScale("Normal Scale", Float) = 1
        [HideInInspector] [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 250

        Pass
        {
            Name "CozyToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _LitColor;
                half4 _ShadowColor;
                half4 _AmbientColor;
                half4 _SpecColor;
                half4 _EdgeColor;
                half _AmbientStrength;
                half _LightBands;
                half _LightBias;
                half _BandSoftness;
                half _ReceiveShadows;
                half _SpecularStrength;
                half _SpecularSize;
                half _EdgeStrength;
                half _EdgePower;
                half _AlphaClip;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half QuantizeLight(half value)
            {
                half bandCount = max(2.0h, round(_LightBands));
                half scaled = saturate(value + _LightBias) * (bandCount - 1.0h);
                half lowerBand = floor(scaled);
                half fractionInBand = frac(scaled);
                half derivativeWidth = fwidth(scaled) * 0.5h;
                half transitionWidth = max(derivativeWidth, _BandSoftness * (bandCount - 1.0h));
                half transition = smoothstep(
                    0.5h - transitionWidth,
                    0.5h + transitionWidth,
                    fractionInBand);
                return saturate((lowerBand + transition) / (bandCount - 1.0h));
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h)
                    clip(baseSample.a - _Cutoff);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half shadowAttenuation = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadows));
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * shadowAttenuation;
                half toonLight = QuantizeLight(diffuse);
                half lightPeak = max(0.001h, max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b)));
                // The bake scene's directional light is intentionally dim gray. Use its chroma,
                // but not its intensity, so the bright toon band cannot become darker than shade.
                half3 lightTint = mainLight.color / lightPeak;

                half3 ambientFill = baseSample.rgb * _AmbientColor.rgb * _AmbientStrength;
                half3 shadedColor = baseSample.rgb * _ShadowColor.rgb + ambientFill;
                half3 litColor = baseSample.rgb * _LitColor.rgb * lightTint;
                half3 color = lerp(shadedColor, litColor, toonLight);

                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirectionWS);
                half normalHalf = saturate(dot(normalWS, halfDirection));
                half specularEdge = 1.0h - saturate(_SpecularSize);
                half specular = smoothstep(
                    specularEdge - _BandSoftness,
                    specularEdge + _BandSoftness,
                    normalHalf);
                specular *= _SpecularStrength * shadowAttenuation * step(0.34h, toonLight);
                color += _SpecColor.rgb * lightTint * specular;

                half facing = saturate(dot(normalWS, viewDirectionWS));
                half edge = pow(saturate(1.0h - facing), max(0.25h, _EdgePower));
                color = lerp(color, color * _EdgeColor.rgb, edge * _EdgeStrength);

                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }

    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
