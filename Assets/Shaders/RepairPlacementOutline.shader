Shader "RepairAll/PlacementOutline"
{
    Properties
    {
        [HDR] _Color ("Outline Color", Color) = (1, 0.72, 0.08, 1)
        _MeshCenter ("Mesh Center", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+80" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MeshCenter;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 viewWS:TEXCOORD1; half pulse:TEXCOORD2; };
            V Vert(A i)
            {
                V o;
                o.pulse = 0.5 + 0.5 * sin(_Time.y * 8);
                float3 p = _MeshCenter.xyz + (i.positionOS.xyz - _MeshCenter.xyz) * (1.015 + 0.025 * o.pulse);
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                half rim = pow(1 - abs(dot(normalize(i.normalWS), normalize(i.viewWS))), 2.5);
                return half4(_Color.rgb * (1 + i.pulse), _Color.a * (0.025 + rim * (0.5 + 0.45 * i.pulse)));
            }
            ENDHLSL
        }
    }
}
