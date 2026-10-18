Shader "CardStack/JokerRainbowSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Speed ("Rainbow Speed", Range(0, 4)) = 1.2
        _Density ("Rainbow Density", Range(0.5, 8)) = 3.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float _Speed;
            float _Density;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float phase = input.uv.x * _Density + input.uv.y * (_Density * 0.35) - _Time.y * _Speed;
                float3 rainbow = 0.55 + 0.45 * cos(6.2831853 * (phase + float3(0.0, 0.3333333, 0.6666667)));
                float detail = dot(sprite.rgb, float3(0.299, 0.587, 0.114));
                float3 finalColor = rainbow * lerp(0.58, 1.18, detail) * input.color.rgb;
                return half4(finalColor, sprite.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
