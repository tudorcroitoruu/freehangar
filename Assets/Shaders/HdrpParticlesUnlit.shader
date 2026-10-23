Shader "Freehangar/HDRP/Particles Unlit"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionEnabled("Emission Enabled", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _AlphaClip("Alpha Clipping", Float) = 0
        _FlipbookMode("Flipbook Blending", Float) = 0
        _ColorMode("Color Mode", Float) = 0
        _SoftParticlesEnabled("Soft Particles", Float) = 0
        _SoftParticlesNearFadeDistance("Soft Near", Float) = 0
        _SoftParticlesFarFadeDistance("Soft Far", Float) = 1
        _CameraFadingEnabled("Camera Fading", Float) = 0
        _CameraNearFadeDistance("Camera Near", Float) = 1
        _CameraFarFadeDistance("Camera Far", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Operation", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [Toggle] _ZWrite("Depth Write", Float) = 0
        [HideInInspector] _Premultiply("Premultiply", Float) = 0
        [HideInInspector] _Modulate("Modulate", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            BlendOp [_BlendOp]
            Cull [_Cull]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _EmissionMap_ST, _Color, _EmissionColor;
            float _Cutoff, _AlphaClip, _FlipbookMode, _ColorMode, _EmissionEnabled;
            float _SoftParticlesEnabled, _SoftParticlesNearFadeDistance, _SoftParticlesFarFadeDistance;
            float _CameraFadingEnabled, _CameraNearFadeDistance, _CameraFarFadeDistance;
            float _Premultiply, _Modulate;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; float blend:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; float2 blendDepth:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(i.positionOS);
                o.positionCS = TransformWorldToHClip(p);
                o.blendDepth = float2(i.blend, -TransformWorldToView(p).z);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_FlipbookMode > 0.5)
                    tex = lerp(tex, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw), i.blendDepth.x);
                float4 tint = i.color * _Color;
                float4 c = tex * tint;
                if (_ColorMode == 1) c.rgb = lerp(2 * tex.rgb * tint.rgb, 1 - 2 * (1 - tex.rgb) * (1 - tint.rgb), step(0.5, tex.rgb));
                if (_ColorMode == 4) c.rgb = tex.rgb + tint.rgb;
                if (_ColorMode == 5) c.rgb = tex.rgb - tint.rgb;
                if (_ColorMode == 6) c.rgb = abs(tex.rgb - tint.rgb);
                if (_EmissionEnabled > 0.5) c.rgb += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv.xy * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb * _EmissionColor.rgb;
                if (_SoftParticlesEnabled > 0.5)
                {
                    float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(i.positionCS.xy)), _ZBufferParams);
                    c.a *= saturate((sceneDepth - i.blendDepth.y - _SoftParticlesNearFadeDistance) / max(0.0001, _SoftParticlesFarFadeDistance - _SoftParticlesNearFadeDistance));
                }
                if (_CameraFadingEnabled > 0.5) c.a *= saturate((i.blendDepth.y - _CameraNearFadeDistance) / max(0.0001, _CameraFarFadeDistance - _CameraNearFadeDistance));
                if (_AlphaClip > 0.5) clip(c.a - _Cutoff);
                if (_Premultiply > 0.5) c.rgb *= c.a;
                if (_Modulate > 0.5) c.rgb = lerp(1.0, c.rgb, c.a);
                return c;
            }
            ENDHLSL
        }
    }
}
