// Made with Amplify Shader Editor v1.9.9.12
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FreeHangar/Tree Cutout Toon"
{
	Properties
	{
		_TextureSample( "Texture Sample", 2D ) = "white" {}
		_TextureRamp( "Texture Ramp", 2D ) = "white" {}


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		//_InstancedTerrainNormals("Instanced Terrain Normals", Float) = 1.0

		[ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1.0
		//[ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0
		[HideInInspector][ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
		//[HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1

		[Toggle] _AlphaClipping("Alpha Clipping", Float) = 1
_AlphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 0
	}

	// Hand-written HDRP support. Amplify's embedded graph below still targets URP;
	// preserve this subshader when regenerating that graph.
	SubShader
	{
		PackageRequirements { "com.unity.render-pipelines.high-definition": "[17.0,18.0]" }
		Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
		Cull [_Cull]
		ZWrite On
		ZTest LEqual

		HLSLINCLUDE
		#pragma target 4.5
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
		#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

		TEXTURE2D(_TextureSample);
		SAMPLER(sampler_TextureSample);
		TEXTURE2D(_TextureRamp);
		SAMPLER(sampler_TextureRamp);
		CBUFFER_START(UnityPerMaterial)
			float4 _TextureSample_ST;
			float4 _TextureRamp_ST;
			float _ReceiveShadows;
			float _SpecularHighlights;
float _AlphaClipping;
float _AlphaCutoff;
		CBUFFER_END

		struct ToonAttributes
		{
			float3 positionOS : POSITION;
			float3 normalOS : NORMAL;
			float2 uv : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};
		struct ToonVaryings
		{
			float4 positionCS : SV_POSITION;
			float3 positionRWS : TEXCOORD0;
			float3 normalWS : TEXCOORD1;
			float2 uv : TEXCOORD2;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};
		ToonVaryings ToonVert(ToonAttributes input)
		{
			ToonVaryings output;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
			output.positionRWS = TransformObjectToWorld(input.positionOS);
			output.positionCS = TransformWorldToHClip(output.positionRWS);
			output.normalWS = TransformObjectToWorldNormal(input.normalOS);
			output.uv = input.uv * _TextureSample_ST.xy + _TextureSample_ST.zw;
			return output;
		}
		float4 ToonDepth(ToonVaryings input) : SV_Target
		{
			if (_AlphaClipping > 0.5) clip(SAMPLE_TEXTURE2D(_TextureSample, sampler_TextureSample, input.uv).a - _AlphaCutoff); return 0;
		}
		ENDHLSL

		Pass
		{
			Name "ForwardOnly"
			Tags { "LightMode"="ForwardOnly" }
			// Exclude this custom-lit surface from HDRP's deferred lighting.
			Stencil { WriteMask 3 Ref 0 Comp Always Pass Replace }
			HLSLPROGRAM
			#pragma vertex ToonVert
			#pragma fragment ToonForward
			#pragma multi_compile_instancing
			#pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
			#pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
			#define SHADOW_DATA_NOT_GUARANTEED_SCALAR
			#define AREA_SHADOW_MEDIUM
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"

			float3 ToonLight(float3 normalWS, float3 L, float3 color)
			{
				// The source graph samples both ramp axes with half-Lambert.
				float rampUV = dot(normalWS, L) * 0.495 + 0.5;
				float3 ramp = SAMPLE_TEXTURE2D_LOD(_TextureRamp, sampler_TextureRamp, rampUV.xx, 0).rgb;
				return color * ramp * rampUV;
			}

			float4 ToonForward(ToonVaryings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float3 normalWS = normalize(input.normalWS);
				float4 texel = SAMPLE_TEXTURE2D(_TextureSample, sampler_TextureSample, input.uv); if (_AlphaClipping > 0.5) clip(texel.a - _AlphaCutoff); float3 albedo = texel.rgb;
				float3 lighting = max(EvaluateAmbientProbe(normalWS), 0);
				uint layers = GetMeshRenderingLayerMask();
				HDShadowContext shadowContext = InitShadowContext();
				// HDRP has one shadow-casting directional light. Sample its cascades
				// outside the light loop, as HDRP's own light loop does.
				float directionalShadow = 1;
				#ifndef _RECEIVE_SHADOWS_OFF
				if (_ReceiveShadows > 0.5 && _DirectionalShadowIndex >= 0)
				{
					DirectionalLightData sun = _DirectionalLightDatas[_DirectionalShadowIndex];
					directionalShadow = lerp(1, GetDirectionalShadowAttenuation(shadowContext, input.positionCS.xy,
						input.positionRWS, normalWS, sun.shadowIndex, -sun.forward), sun.shadowDimmer);
				}
				#endif
				[loop] for (uint i = 0; i < _DirectionalLightCount; ++i)
				{
					DirectionalLightData light = _DirectionalLightDatas[i];
					if ((light.lightLayers & layers) == 0) continue;
					float3 L = -light.forward;
					float shadow = (int)i == _DirectionalShadowIndex ? directionalShadow : 1;
					lighting += ToonLight(normalWS, L, light.color * light.diffuseDimmer * shadow);
				}
				[loop] for (uint j = 0; j < _PunctualLightCount; ++j)
				{
					LightData light = _LightDatas[j];
					if ((light.lightLayers & layers) == 0) continue;
					float3 toLight = light.positionRWS - input.positionRWS;
					float distanceSq = max(dot(toLight, toLight), 0.0001);
					float inverseDistance = rsqrt(distanceSq);
					float distanceToLight = distanceSq * inverseDistance;
					float3 L = toLight * inverseDistance;
					float4 distances = float4(distanceToLight, distanceSq, inverseDistance, dot(-toLight, light.forward));
					float attenuation = PunctualLightAttenuation(distances, light.rangeAttenuationScale,
						light.rangeAttenuationBias, light.angleScale, light.angleOffset);
					float shadow = 1;
					#ifndef _RECEIVE_SHADOWS_OFF
					if (_ReceiveShadows > 0.5 && light.shadowIndex >= 0 && attenuation > 0)
						shadow = lerp(1, GetPunctualShadowAttenuation(shadowContext, input.positionCS.xy,
							input.positionRWS, normalWS, light.shadowIndex, L, distanceToLight,
							light.lightType == GPULIGHTTYPE_POINT, light.lightType != GPULIGHTTYPE_PROJECTOR_BOX), light.shadowDimmer);
					#endif
					lighting += ToonLight(normalWS, L, light.color * light.diffuseDimmer * attenuation * shadow);
				}
				// HDRP light buffers use physical units; pre-expose the result once.
				return float4(albedo * lighting * GetCurrentExposureMultiplier(), 1);
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthForwardOnly"
			Tags { "LightMode"="DepthForwardOnly" }
			HLSLPROGRAM
			#pragma vertex ToonVert
			#pragma fragment ToonNormals
			#pragma multi_compile_instancing
			#pragma multi_compile_fragment _ WRITE_MSAA_DEPTH
			void ToonNormals(ToonVaryings input,
			#ifdef WRITE_MSAA_DEPTH
				out float4 depth : SV_Target0, out float4 normal : SV_Target1
			#else
				out float4 normal : SV_Target0
			#endif
			)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				NormalData data;
				data.normalWS = normalize(input.normalWS);
				data.perceptualRoughness = 0.5;
				EncodeIntoNormalBuffer(data, normal);
				#ifdef WRITE_MSAA_DEPTH
				depth = float4(input.positionCS.zzz, 1);
				#endif
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			ColorMask 0
			ZClip [_ZClip]
			HLSLPROGRAM
			#pragma vertex ToonVert
			#pragma fragment ToonDepth
			#pragma multi_compile_instancing
			ENDHLSL
		}
	}
}

