//
//  OutlineMask.shader
//  QuickOutline
//
//  Created by Chris Nolet on 2/21/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

Shader "Custom/Outline Mask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
  }

  SubShader {
    Tags {
      "RenderPipeline" = "HDRenderPipeline"
      // Render the mask before the fill, within HDRP's transparent range.
      "Queue" = "Transparent+98"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "Mask"
      Tags { "LightMode" = "ForwardOnly" }
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref 64
        ReadMask 64
        WriteMask 64
        Pass Replace
      }
      HLSLPROGRAM
      #pragma target 4.5
      #pragma vertex Vert
      #pragma fragment Frag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
      struct Attributes { float3 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
      Varyings Vert(Attributes i)
      {
          Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
          o.positionCS = TransformObjectToHClip(i.positionOS); return o;
      }
      float4 Frag(Varyings i):SV_Target { return 0; }
      ENDHLSL
    }
  }
}
