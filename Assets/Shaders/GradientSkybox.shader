Shader "RepairAll/Gradient Skybox"
{
    Properties
    {
        [HDR] _TopColor ("Top Color", Color) = (0.055, 0.12, 0.24, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.35, 0.65, 0.85, 1)
        [HDR] _BottomColor ("Bottom Color", Color) = (0.10, 0.12, 0.16, 1)
        _HorizonHeight ("Horizon Height", Range(-0.8, 0.8)) = 0
        _HorizonWidth ("Horizon Band Width", Range(0, 1.5)) = 0.3
        _GradientPower ("Gradient Curve", Range(0.1, 8)) = 1
        _Exposure ("Exposure", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Gradient Skybox"
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _HorizonHeight;
                half _HorizonWidth;
                half _GradientPower;
                half _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half height = normalize(input.directionWS).y;
                half halfWidth = _HorizonWidth * 0.5h;
                half lowerEdge = clamp(_HorizonHeight - halfWidth, -0.999h, 0.999h);
                half upperEdge = clamp(_HorizonHeight + halfWidth, -0.999h, 0.999h);

                half lowerBlend = 1.0h - smoothstep(-1.0h, lowerEdge, height);
                half upperBlend = smoothstep(upperEdge, 1.0h, height);
                lowerBlend = pow(saturate(lowerBlend), _GradientPower);
                upperBlend = pow(saturate(upperBlend), _GradientPower);

                half3 color = lerp(_HorizonColor.rgb, _BottomColor.rgb, lowerBlend);
                color = lerp(color, _TopColor.rgb, upperBlend);
                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
