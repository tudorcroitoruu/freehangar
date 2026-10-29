// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Atmosphere" 
{
	Properties 
	{
		_Color ("Color", Color) = (0.1, 0.35, 1.0, 1.0)
		_Intensity("Intensity", float) = 200
		[HideInInspector] _EarthSunDirection("HDRP Sun Direction", Vector) = (0,1,0,0)
	}

	SubShader 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		//Tags { "RenderType"="Opaque" }
    	Pass 
    	{
			Blend One One
			ZWrite Off
			Cull Front
		
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag

			float4 _Color;
			float _Intensity;

			float4 _EarthSunDirection;

			struct vertexInput 
			{
				float4 pos				: POSITION;
				float3 normal			: NORMAL;
			};

			struct vertexOutput 
			{
				float4 pos				: POSITION;
				float3 normal			: TEXCOORD0;
				float3 viewDir			: TEXCOORD1;
				float3 diffuse			: TEXCOORD2;
			};
			
			vertexOutput vert(vertexInput input) 
			{
				vertexOutput output;
				output.pos = UnityObjectToClipPos(input.pos);
				output.normal = input.normal;
				output.viewDir = ObjSpaceViewDir(input.pos);

				float3 lightDir = normalize(mul((float3x3)unity_WorldToObject, _EarthSunDirection.w > 0.5 ? _EarthSunDirection.xyz : _WorldSpaceLightPos0.xyz));

				float d = saturate(dot(input.normal, lightDir) * 3);
				output.diffuse = d;//pow(d, 15);
				return output;
			}
		
			float4 frag(vertexOutput input) : Color
			{
				float3 viewDir = normalize(input.viewDir);
				float3 normalDir = normalize(input.normal);
				float alpha = pow(saturate(dot(viewDir, -normalDir)), 3) * _Intensity;
				float4 result;
				result.rgb = _Color.rgb * input.diffuse * alpha;
				result.a = 1;
				return result;
			}

			ENDCG

    	}
	}
}
