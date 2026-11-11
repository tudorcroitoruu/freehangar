Shader "Freehangar/URP/Tree Cutout Toon"
{
    Properties
    {
        _TextureSample("Tree atlas", 2D) = "white" {}
        _TextureRamp("Lighting ramp", 2D) = "white" {}
        [Toggle] _AlphaClipping("Alpha clipping", Float) = 1
        _AlphaCutoff("Alpha cutoff", Range(0,1)) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull [_Cull] ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _TextureSample_ST;
        float _AlphaClipping, _AlphaCutoff, _Cull;
        CBUFFER_END
        TEXTURE2D(_TextureSample); SAMPLER(sampler_TextureSample);
        TEXTURE2D(_TextureRamp); SAMPLER(sampler_TextureRamp);
        struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes i)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(i.positionOS); o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.uv = TRANSFORM_TEX(i.uv, _TextureSample);
            return o;
        }
        half4 Atlas(Varyings i)
        {
            UNITY_SETUP_INSTANCE_ID(i);
            #if defined(LOD_FADE_CROSSFADE)
            LODFadeCrossFade(i.positionCS);
            #endif
            half4 color = SAMPLE_TEXTURE2D(_TextureSample, sampler_TextureSample, i.uv);
            if (_AlphaClipping > .5) clip(color.a - _AlphaCutoff);
            return color;
        }
        half3 RampLight(Light light, half3 n)
        {
            half ramp = dot(n, light.direction) * .495 + .5;
            return SAMPLE_TEXTURE2D(_TextureRamp, sampler_TextureRamp, ramp.xx).rgb * ramp * light.color * light.distanceAttenuation * light.shadowAttenuation;
        }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 albedo = Atlas(i);
                half3 n = normalize(i.normalWS);
                half3 light = max(SampleSH(n), 0) + RampLight(GetMainLight(TransformWorldToShadowCoord(i.positionWS)), n);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    light += RampLight(GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1)), n);
                LIGHT_LOOP_END
                return half4(MixFog(albedo.rgb * light, ComputeFogFactor(TransformWorldToHClip(i.positionWS).z)), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Depth(Varyings i):SV_Target { Atlas(i); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Normals(Varyings i):SV_Target { Atlas(i); return half4(normalize(i.normalWS),0); }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes i)
            {
                Varyings o = Vert(i);
                float3 lightDirection = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                lightDirection = normalize(_LightPosition - o.positionWS);
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, normalize(o.normalWS), lightDirection));
                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 Depth(Varyings i):SV_Target { Atlas(i); return 0; }
            ENDHLSL
        }
    }
}
