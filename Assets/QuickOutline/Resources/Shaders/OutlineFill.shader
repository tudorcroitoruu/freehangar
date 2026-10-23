//
//  OutlineFill.shader
//  QuickOutline
//
//  Created by Chris Nolet on 2/21/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

Shader "Custom/Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0

    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
  }

  SubShader {
    Tags {
      "RenderPipeline" = "HDRenderPipeline"
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Tags { "LightMode" = "ForwardOnly" }
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref 64
        ReadMask 64
        WriteMask 0
        Comp NotEqual
      }

      HLSLPROGRAM
      #pragma target 4.5
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      #pragma vertex vert
      #pragma fragment frag

      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 position : SV_POSITION;
        float4 color : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform float4 _OutlineColor;
      uniform float _OutlineWidth;

      v2f vert(appdata input) {
        v2f output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normal;
        float3 viewPosition = TransformWorldToView(TransformObjectToWorld(input.vertex.xyz));
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_V, TransformObjectToWorldNormal(normal)));

        output.position = mul(UNITY_MATRIX_P, float4(viewPosition + viewNormal * -viewPosition.z * _OutlineWidth / 1000.0, 1));
        output.color = _OutlineColor;

        return output;
      }

      float4 frag(v2f input) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return input.color;
      }
      ENDHLSL
    }
  }
}
