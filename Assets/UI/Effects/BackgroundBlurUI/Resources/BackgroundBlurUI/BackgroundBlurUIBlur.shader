Shader "Hidden/BackgroundBlurUI/GaussianBlur"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Optimized Gaussian Blur"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Direction;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 stepUv = _MainTex_TexelSize.xy * _Direction.xy;

                // Five bilinear samples approximate a high quality 9-tap Gaussian.
                half4 color = tex2D(_MainTex, input.uv) * 0.2270270270h;
                color += tex2D(
                    _MainTex,
                    input.uv + stepUv * 1.3846153846) * 0.3162162162h;
                color += tex2D(
                    _MainTex,
                    input.uv - stepUv * 1.3846153846) * 0.3162162162h;
                color += tex2D(
                    _MainTex,
                    input.uv + stepUv * 3.2307692308) * 0.0702702703h;
                color += tex2D(
                    _MainTex,
                    input.uv - stepUv * 3.2307692308) * 0.0702702703h;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
