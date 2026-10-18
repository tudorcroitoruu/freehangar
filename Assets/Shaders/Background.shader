Shader "Custom/PipesBackground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorA ("Color A", Color) = (0.96,0.95,0.92,1)
        _ColorB ("Color B", Color) = (0.86,0.89,0.94,1)
        _EffectStrength ("Effect Strength", Range(0,1)) = 0.25
        _VisualStyle ("Visual Style", Range(0,3)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _ColorA;
            float4 _ColorB;
            float _EffectStrength;
            float _VisualStyle;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = saturate(uv.y * 0.82 + uv.x * 0.18);
                float3 color = lerp(_ColorA.rgb, _ColorB.rgb, t);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return half4(color, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
