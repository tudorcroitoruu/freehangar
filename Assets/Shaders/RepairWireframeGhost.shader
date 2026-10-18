// Hand-drawn, x-ray assembly guide for URP.
Shader "RepairAll/WireframeGhost"
{
    Properties
    {
        _WireColor ("Ink Color", Color) = (0.12, 0.95, 0.82, 0.95)
        _FillColor ("Paper Fill", Color) = (0.06, 0.50, 0.62, 0.08)
        _WireThickness ("Ink Thickness", Range(0.5, 4.0)) = 1.35
        _PatternScale ("Sketch Scale", Range(2.0, 40.0)) = 13.0
        _Jitter ("Hand Jitter", Range(0.0, 1.0)) = 0.45
        _XRayAlpha ("Hidden Line Alpha", Range(0.0, 1.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionOS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 viewWS : TEXCOORD2;
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _WireColor;
            half4 _FillColor;
            half _WireThickness;
            half _PatternScale;
            half _Jitter;
            half _XRayAlpha;
        CBUFFER_END

        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positions.positionCS;
            output.positionOS = input.positionOS.xyz;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.viewWS = GetWorldSpaceViewDir(positions.positionWS);
            return output;
        }

        half SketchMask(Varyings input)
        {
            float3 p = input.positionOS * (_PatternScale * 0.12);
            float wobble = sin(dot(p, float3(1.73, 2.11, 2.67)) * 0.55) * _Jitter * 0.18;
            float3 cell = abs(frac(p + wobble) - 0.5);
            float3 aa = max(fwidth(p), 0.001) * _WireThickness;
            float3 lines = 1.0 - smoothstep(aa * 0.35, aa, 0.5 - cell);
            float3 normalWeight = abs(normalize(input.normalWS));
            half grid = max(lines.x * (1.0 - normalWeight.x),
                max(lines.y * (1.0 - normalWeight.y), lines.z * (1.0 - normalWeight.z)));
            half fresnel = pow(1.0 - abs(dot(normalize(input.normalWS), normalize(input.viewWS))), 3.5);
            return smoothstep(0.18, 0.85, grid * 0.45 + fresnel);
        }

        half4 DrawSketch(Varyings input, half alphaScale)
        {
            half4 color = lerp(_FillColor, _WireColor, SketchMask(input));
            color.a *= alphaScale;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Hidden Sketch"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHidden
            half4 FragHidden(Varyings input) : SV_Target { return DrawSketch(input, _XRayAlpha); }
            ENDHLSL
        }

        Pass
        {
            Name "Visible Sketch"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVisible
            half4 FragVisible(Varyings input) : SV_Target { return DrawSketch(input, 1.0h); }
            ENDHLSL
        }
    }
    FallBack Off
}
