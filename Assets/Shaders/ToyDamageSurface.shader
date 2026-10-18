// Procedural rust, dirt, scratches, chipped paint, and cracked glass for repairable toys.
Shader "RepairAll/ToyDamageSurface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Header(Electronics)]
        _Electronics ("Electronic Surface", Range(0, 1)) = 0
        _EmissionMap ("Electronic Emission Mask", 2D) = "white" {}
        [HDR] _RepairedEmission ("Repaired Electronic Emission", Color) = (0, 0, 0, 1)
        _GlitchStrength ("Damaged Glitch Strength", Range(0, 1)) = 0
        [HideInInspector] _PreviewLighting ("Preview Lighting", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        [HideInInspector] _BaseNormalPower ("Base Normal Power Multiplier", Float) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.45
        _ReflectionStrength ("Reflection Probe Strength", Range(0, 3)) = 1
        [Header(Repair Damage)]
        _DamageAmount ("Overall Damage", Range(0, 1)) = 1
        _DirtAmount ("Dirt", Range(0, 1)) = 0
        _RustAmount ("Rust", Range(0, 1)) = 0
        _ScratchAmount ("Scratches", Range(0, 1)) = 0
        _ScratchPatternMap ("Scratch Pattern / Tiling", 2D) = "white" {}
        _ScratchTilingScale ("Scratch Tiling Scale", Range(0.1, 8)) = 1
        _PolishedAmount ("Polished Sheen", Range(0, 1)) = 0
        _ChippedPaintAmount ("Chipped Paint", Range(0, 1)) = 0
        _BrokenGlassAmount ("Broken Glass", Range(0, 1)) = 0
        _DamageSeed ("Damage Seed", Range(0, 1)) = 0.37
        _RustColor ("Rust Color", Color) = (0.58, 0.16, 0.035, 1)
        _RustEdgeColor ("Rust Edge", Color) = (0.95, 0.38, 0.055, 1)
        _RustTexture ("Rust Surface Texture", 2D) = "white" {}
        _RustTextureInfluence ("Rust Texture Influence", Range(0, 1)) = 0
        [Normal] _RustNormalMap ("Rust Normal", 2D) = "bump" {}
        _RustNormalStrength ("Rust Normal Power", Range(0, 5)) = 0.75
        [HideInInspector] _RustNormalPower ("Rust Normal Power Multiplier", Float) = 1
        _BareMetalColor ("Bare Metal", Color) = (0.28, 0.31, 0.32, 1)
        _DirtColor ("Dirt Color", Color) = (0.12, 0.10, 0.055, 1)
        _DirtTexture ("Dirt Surface Texture", 2D) = "white" {}
        _DirtTextureInfluence ("Dirt Texture Influence", Range(0, 1)) = 0
        [Normal] _DirtNormalMap ("Dirt Normal", 2D) = "bump" {}
        _DirtNormalStrength ("Dirt Normal Power", Range(0, 5)) = 0.6
        [HideInInspector] _DirtNormalPower ("Dirt Normal Power Multiplier", Float) = 1
        [Normal] _PaintNormalMap ("Chipped Paint Normal", 2D) = "bump" {}
        _PaintNormalStrength ("Paint Normal Power", Range(0, 5)) = 0.85
        [HideInInspector] _PaintNormalPower ("Paint Normal Power Multiplier", Float) = 1
        _GlassColor ("Damaged Glass", Color) = (0.30, 0.60, 0.67, 1)
        _GlassCrackMap ("Broken Glass Pattern", 2D) = "white" {}
        _GlassCrackStrength ("Glass Pattern Strength", Range(0, 1)) = 0
        _DamageScale ("Damage Scale", Range(1, 80)) = 18
        [HideInInspector] _DamageCenter ("Damage Projection Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _DamageCoordinateScale ("Damage Projection Scale", Vector) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Toy Damage Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_RustTexture); SAMPLER(sampler_RustTexture);
            TEXTURE2D(_DirtTexture); SAMPLER(sampler_DirtTexture);
            TEXTURE2D(_ScratchPatternMap); SAMPLER(sampler_ScratchPatternMap);
            TEXTURE2D(_RustNormalMap); SAMPLER(sampler_RustNormalMap);
            TEXTURE2D(_DirtNormalMap); SAMPLER(sampler_DirtNormalMap);
            TEXTURE2D(_PaintNormalMap); SAMPLER(sampler_PaintNormalMap);
            TEXTURE2D(_GlassCrackMap); SAMPLER(sampler_GlassCrackMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _OcclusionMap_ST;
                float4 _RustTexture_ST;
                float4 _DirtTexture_ST;
                float4 _ScratchPatternMap_ST;
                float4 _RustNormalMap_ST;
                float4 _DirtNormalMap_ST;
                float4 _PaintNormalMap_ST;
                float4 _GlassCrackMap_ST;
                float4 _DamageCenter, _DamageCoordinateScale;
                half4 _BaseColor, _RustColor, _RustEdgeColor, _BareMetalColor, _DirtColor, _GlassColor;
                half4 _EmissionColor;
                half4 _RepairedEmission;
                half _Electronics, _GlitchStrength;
                half _PreviewLighting;
                half _BumpScale, _BaseNormalPower, _OcclusionStrength, _Metallic, _Smoothness;
                half _ReflectionStrength;
                half _DamageAmount, _DirtAmount, _RustAmount, _ScratchAmount, _PolishedAmount;
                half _ScratchTilingScale;
                half _ChippedPaintAmount, _BrokenGlassAmount, _DamageSeed, _DamageScale;
                half _RustTextureInfluence, _DirtTextureInfluence, _GlassCrackStrength;
                half _RustNormalStrength, _DirtNormalStrength, _PaintNormalStrength;
                half _RustNormalPower, _DirtNormalPower, _PaintNormalPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float2 normalUv : TEXCOORD5;
                float2 occlusionUv : TEXCOORD6;
                float3 positionOS : TEXCOORD7;
                half3 normalOS : TEXCOORD8;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33 + _DamageSeed * 17.0);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3D(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), f.x);
                float b = lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), f.x);
                float c = lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), f.x);
                float d = lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + 1), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            float FractalNoise3D(float3 p)
            {
                return Noise3D(p) * 0.58 + Noise3D(p * 2.03 + 8.1) * 0.28 +
                    Noise3D(p * 4.07 + 19.7) * 0.14;
            }

            half4 SampleRustTriplanar(float3 p, float3 w)
            {
                return SAMPLE_TEXTURE2D(_RustTexture, sampler_RustTexture, p.zy * _RustTexture_ST.xy + _RustTexture_ST.zw) * w.x +
                    SAMPLE_TEXTURE2D(_RustTexture, sampler_RustTexture, p.xz * _RustTexture_ST.xy + _RustTexture_ST.zw) * w.y +
                    SAMPLE_TEXTURE2D(_RustTexture, sampler_RustTexture, p.xy * _RustTexture_ST.xy + _RustTexture_ST.zw) * w.z;
            }

            half4 SampleDirtTriplanar(float3 p, float3 w)
            {
                return SAMPLE_TEXTURE2D(_DirtTexture, sampler_DirtTexture, p.zy * _DirtTexture_ST.xy + _DirtTexture_ST.zw) * w.x +
                    SAMPLE_TEXTURE2D(_DirtTexture, sampler_DirtTexture, p.xz * _DirtTexture_ST.xy + _DirtTexture_ST.zw) * w.y +
                    SAMPLE_TEXTURE2D(_DirtTexture, sampler_DirtTexture, p.xy * _DirtTexture_ST.xy + _DirtTexture_ST.zw) * w.z;
            }

            float3 SampleRelief(float3 p, float3 w, int map)
            {
                half3 x, y, z;
                if (map == 0)
                {
                    x = UnpackNormal(SAMPLE_TEXTURE2D(_RustNormalMap, sampler_RustNormalMap, p.zy * _RustNormalMap_ST.xy + _RustNormalMap_ST.zw));
                    y = UnpackNormal(SAMPLE_TEXTURE2D(_RustNormalMap, sampler_RustNormalMap, p.xz * _RustNormalMap_ST.xy + _RustNormalMap_ST.zw));
                    z = UnpackNormal(SAMPLE_TEXTURE2D(_RustNormalMap, sampler_RustNormalMap, p.xy * _RustNormalMap_ST.xy + _RustNormalMap_ST.zw));
                }
                else if (map == 1)
                {
                    x = UnpackNormal(SAMPLE_TEXTURE2D(_DirtNormalMap, sampler_DirtNormalMap, p.zy * _DirtNormalMap_ST.xy + _DirtNormalMap_ST.zw));
                    y = UnpackNormal(SAMPLE_TEXTURE2D(_DirtNormalMap, sampler_DirtNormalMap, p.xz * _DirtNormalMap_ST.xy + _DirtNormalMap_ST.zw));
                    z = UnpackNormal(SAMPLE_TEXTURE2D(_DirtNormalMap, sampler_DirtNormalMap, p.xy * _DirtNormalMap_ST.xy + _DirtNormalMap_ST.zw));
                }
                else
                {
                    x = UnpackNormal(SAMPLE_TEXTURE2D(_PaintNormalMap, sampler_PaintNormalMap, p.zy * _PaintNormalMap_ST.xy + _PaintNormalMap_ST.zw));
                    y = UnpackNormal(SAMPLE_TEXTURE2D(_PaintNormalMap, sampler_PaintNormalMap, p.xz * _PaintNormalMap_ST.xy + _PaintNormalMap_ST.zw));
                    z = UnpackNormal(SAMPLE_TEXTURE2D(_PaintNormalMap, sampler_PaintNormalMap, p.xy * _PaintNormalMap_ST.xy + _PaintNormalMap_ST.zw));
                }
                return float3(0, x.y, x.x) * w.x + float3(y.x, 0, y.y) * w.y +
                    float3(z.x, z.y, 0) * w.z;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32 + _DamageSeed * 19.17);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), f.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1), f.x), f.y);
            }

            float FractalNoise(float2 p)
            {
                return Noise(p) * 0.58 + Noise(p * 2.03 + 8.1) * 0.28 +
                    Noise(p * 4.07 + 19.7) * 0.14;
            }

            float ScratchPattern(float2 uv)
            {
                float2 p = uv * float2(_DamageScale * 1.8, _DamageScale * 0.72) *
                    _ScratchTilingScale;
                float row = floor(p.y);
                float gate = step(0.60, Hash21(float2(row, _DamageSeed * 73.0)));
                float scratchLine = abs(frac(p.y + sin(p.x * 0.22 + row * 2.7) * 0.12) - 0.5);
                float broken = step(0.28, Hash21(floor(p * float2(0.18, 1.0))));
                return (1.0 - smoothstep(0.025, 0.10, scratchLine)) * gate * broken;
            }

            float CrackPattern(float2 uv)
            {
                float2 cell = floor(uv * 3.0);
                float2 p = frac(uv * 3.0) - 0.5;
                float radius = length(p);
                float spokes = abs(sin(atan2(p.y, p.x) *
                    (5.0 + floor(Hash21(cell) * 4.0)) + Hash21(cell + 4.2) * 6.283));
                float radial = 1.0 - smoothstep(0.018, 0.065, spokes * max(radius, 0.08));
                float rings = 1.0 - smoothstep(0.018, 0.055,
                    abs(frac(radius * 5.0 + Hash21(cell)) - 0.5));
                return saturate(max(radial, rings * 0.42) * smoothstep(0.48, 0.05, radius));
            }

            float SampleScratches(float2 uv)
            {
                uv = uv * _ScratchPatternMap_ST.xy + _ScratchPatternMap_ST.zw;
                return ScratchPattern(uv + _DamageSeed) *
                    SAMPLE_TEXTURE2D(_ScratchPatternMap, sampler_ScratchPatternMap, uv).r;
            }

            half4 SampleGlassTriplanar(float3 p, float3 w)
            {
                return SAMPLE_TEXTURE2D(_GlassCrackMap, sampler_GlassCrackMap, p.zy * _GlassCrackMap_ST.xy + _GlassCrackMap_ST.zw) * w.x +
                    SAMPLE_TEXTURE2D(_GlassCrackMap, sampler_GlassCrackMap, p.xz * _GlassCrackMap_ST.xy + _GlassCrackMap_ST.zw) * w.y +
                    SAMPLE_TEXTURE2D(_GlassCrackMap, sampler_GlassCrackMap, p.xy * _GlassCrackMap_ST.xy + _GlassCrackMap_ST.zw) * w.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalUv = TRANSFORM_TEX(input.uv, _BumpMap);
                output.occlusionUv = TRANSFORM_TEX(input.uv, _OcclusionMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 electronicUv = input.uv;
                half glitch = _Electronics * _GlitchStrength * saturate(_DamageAmount);
                float noise = 1.0;
                if (glitch > 0.001h)
                {
                    float tick = floor(_Time.y * 18.0);
                    float band = floor(input.uv.y * 32.0);
                    noise = frac(sin(band * 127.1 + tick * 311.7) * 43758.5453);
                }
                electronicUv.x += (noise - 0.5) * step(0.68, noise) * glitch * 0.12;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, electronicUv) * _BaseColor;
                if (glitch > 0.001h)
                {
                    baseSample.r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                        electronicUv + float2(glitch * 0.008, 0)).r * _BaseColor.r;
                    baseSample.b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                        electronicUv - float2(glitch * 0.008, 0)).b * _BaseColor.b;
                    baseSample.rgb = lerp(baseSample.rgb, half3(noise, 0.15, 1.0 - noise),
                        glitch * step(0.9, noise) * 0.7);
                }
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.normalUv),
                    _BumpScale * _BaseNormalPower);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                // Size-normalized, object-locked projection: no mesh UV or import-unit dependency.
                float3 coordinateScale = max(_DamageCoordinateScale.xyz, 0.000001);
                float3 surfacePos = (input.positionOS - _DamageCenter.xyz) * coordinateScale + 0.5;
                float3 damagePos = surfacePos * _DamageScale +
                    _DamageSeed * float3(11.3, 7.1, 5.9);
                float3 projectionNormal = normalize(input.normalOS / coordinateScale);
                float3 blend = pow(abs(projectionNormal), 4.0);
                blend /= max(0.0001, blend.x + blend.y + blend.z);
                float coarse = FractalNoise3D(damagePos * 0.28);
                float fine = FractalNoise3D(damagePos);
                half3 rustTexture = SampleRustTriplanar(damagePos, blend).rgb;
                float rustPhotoGrain = dot(rustTexture, float3(0.25, 0.60, 0.15));
                float rustPattern = lerp(coarse * 0.72 + fine * 0.42,
                    coarse * 0.52 + fine * 0.23 + (1.0 - rustPhotoGrain) * 0.40,
                    _RustTextureInfluence);
                half rust = smoothstep(0.43, 0.66, rustPattern) * _RustAmount * _DamageAmount;
                half rustEdge = smoothstep(0.38, 0.60, coarse) * (1.0 - rust) * _RustAmount * _DamageAmount;
                // Broad, ragged islands keep only a little of the original paint.
                // Rust photo grain breaks up the edge instead of producing uniform dots.
                float paintField = FractalNoise3D(damagePos * 0.16 + 3.7) * 0.54 +
                    FractalNoise3D(damagePos * 0.73 + 11.1) * 0.27 + fine * 0.19;
                float remainingPaint = smoothstep(0.46, 0.60, paintField);
                half chips = (1.0h - remainingPaint) * _ChippedPaintAmount * _DamageAmount;
                half paintEdge = saturate(1.0h - abs(remainingPaint * 2.0h - 1.0h)) *
                    _ChippedPaintAmount * _DamageAmount;
                // Rust grows on exposed metal; leave the small paint islands legible.
                half metalExposure = lerp(1.0h, saturate(chips * 0.92h + 0.08h),
                    _ChippedPaintAmount * _DamageAmount);
                rust *= metalExposure;
                rustEdge *= metalExposure;
                half occlusionMap = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.occlusionUv).g;
                float dirtGrain = Noise3D(damagePos * 7.3 + 31.7);
                float dirtPatches = smoothstep(0.28, 0.69, coarse * 0.75 + fine * 0.25);
                half3 dirtTexture = SampleDirtTriplanar(damagePos, blend).rgb;
                float photoGrain = dot(dirtTexture, float3(0.25, 0.62, 0.13));
                dirtPatches = lerp(dirtPatches,
                    smoothstep(0.24, 0.68, dirtPatches * 0.55 + (1.0 - photoGrain) * 0.55),
                    _DirtTextureInfluence);
                float dirtSpeckles = smoothstep(0.65, 0.83, dirtGrain) * (0.3 + dirtPatches);
                half dirt = saturate(dirtPatches * 0.85 + dirtSpeckles * 0.45 +
                    (1.0 - occlusionMap) * _OcclusionStrength * 0.4) * _DirtAmount * _DamageAmount;
                float3 relief = 0;
                if (rust + rustEdge > 0.01h)
                    relief += SampleRelief(damagePos, blend, 0) * (rust + rustEdge * 0.5h) *
                        _RustNormalStrength * _RustNormalPower * 8.0h;
                if (dirt > 0.01h)
                    relief += SampleRelief(damagePos, blend, 1) * dirt *
                        _DirtNormalStrength * _DirtNormalPower * 8.0h;
                if (chips > 0.01h)
                    relief += SampleRelief(damagePos, blend, 2) * chips *
                        _PaintNormalStrength * _PaintNormalPower * 8.0h;
                // Apply all normal detail before orienting the final normal for the visible face.
                // Flipping only the base normal lets strong damage relief turn backfaces toward
                // the front again, which makes an opened door's underside appear transparent.
                // Transform relief with rotation/reflection only: model scale must not amplify bumps.
                relief -= projectionNormal * dot(relief, projectionNormal);
                float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                float3 axisX = normalize(mul(objectToWorld, float3(1, 0, 0)));
                float3 axisY = normalize(mul(objectToWorld, float3(0, 1, 0)));
                float3 axisZ = normalize(mul(objectToWorld, float3(0, 0, 1)));
                normalWS = normalize(normalWS + axisX * relief.x + axisY * relief.y + axisZ * relief.z);
                normalWS *= IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half scratches = (SampleScratches(surfacePos.zy) * blend.x +
                    SampleScratches(surfacePos.xz) * blend.y + SampleScratches(surfacePos.xy) * blend.z) *
                    _ScratchAmount * _DamageAmount;
                float3 glassPos = surfacePos * (_DamageScale / 18.0);
                half4 glassTexture = SampleGlassTriplanar(glassPos, blend);
                half photoCracks = saturate((1.0h - min(glassTexture.r,
                    min(glassTexture.g, glassTexture.b))) * glassTexture.a * 1.6h);
                float3 crackPos = glassPos + _DamageSeed * 0.31;
                half proceduralCracks = CrackPattern(crackPos.zy) * blend.x +
                    CrackPattern(crackPos.xz) * blend.y + CrackPattern(crackPos.xy) * blend.z;
                half cracks = max(proceduralCracks *
                    (1.0h - _GlassCrackStrength * 0.75h), photoCracks * _GlassCrackStrength) *
                    _BrokenGlassAmount * _DamageAmount;
                half missingGlass = (1.0h - glassTexture.a) * _GlassCrackStrength *
                    _BrokenGlassAmount * _DamageAmount;

                half3 albedo = baseSample.rgb;
                half3 heatDarkenedMetal = _BareMetalColor.rgb *
                    lerp(0.38h, 0.90h, saturate(0.32h + fine * 0.40h));
                albedo = lerp(albedo, heatDarkenedMetal, chips * 0.96h);
                albedo = lerp(albedo, _BareMetalColor.rgb * 0.75h, paintEdge * 0.48h);
                albedo = lerp(albedo, _RustEdgeColor.rgb, rustEdge * 0.7);
                albedo = lerp(albedo, lerp(_RustColor.rgb, rustTexture,
                    _RustTextureInfluence), rust);
                albedo = lerp(albedo, _BareMetalColor.rgb * 1.65, scratches);
                albedo = lerp(albedo, _GlassColor.rgb, _BrokenGlassAmount * _DamageAmount * 0.48);
                albedo = lerp(albedo, _GlassColor.rgb * 0.18h, missingGlass * 0.75h);
                albedo = lerp(albedo, half3(0.82, 0.95, 1.0), cracks);
                // Grime coats the damaged surface; washing reveals the remaining repairs.
                half3 dirtColor = lerp(_DirtColor.rgb * lerp(0.65, 1.6, dirtGrain),
                    dirtTexture * 0.48h, _DirtTextureInfluence);
                albedo = lerp(albedo, dirtColor, dirt * 0.88);

                half occlusion = lerp(1.0h, occlusionMap, _OcclusionStrength);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * occlusion;
                half3 direct = mainLight.color * ndotl * mainLight.shadowAttenuation;
                half3 color = albedo * (ambient + direct);
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                color += albedo * _PreviewLighting * occlusion *
                    (0.35h + 0.65h * saturate(dot(normalWS, viewDir)));
                half3 halfDir = SafeNormalize(viewDir + mainLight.direction);
                half roughDamage = saturate(rust + chips + dirt * 0.65 + scratches * 0.4);
                half smoothness = saturate((_Smoothness + _PolishedAmount * 0.36h) *
                    (1.0 - roughDamage * 0.82));
                half specular = pow(saturate(dot(normalWS, halfDir)), exp2(4.0 + smoothness * 7.0));
                color += lerp(0.04h.xxx, albedo, _Metallic) * specular * mainLight.color;
                half3 reflectedDirection = reflect(-viewDir, normalWS);
                half3 probe = GlossyEnvironmentReflection(reflectedDirection,
                    input.positionWS, 1.0h - smoothness, occlusion,
                    GetNormalizedScreenSpaceUV(input.positionCS));
                half facingReflection = pow(1.0h - saturate(dot(normalWS, viewDir)), 5.0h);
                half3 reflectivity = lerp(0.04h.xxx, albedo, _Metallic);
                reflectivity += (1.0h - reflectivity) * facingReflection;
                color += probe * reflectivity * _ReflectionStrength;
                color += _PolishedAmount * pow(saturate(dot(normalWS, halfDir)), 24.0h) *
                    mainLight.color * 0.28h;
                color += cracks * _BrokenGlassAmount * half3(0.15, 0.34, 0.42);
                color += _EmissionColor.rgb;
                if (_Electronics > 0.001h)
                {
                    half3 emissionMask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, electronicUv).rgb;
                    half flicker = lerp(0.08h, 0.6h, step(0.35, noise));
                    // A keyboard backlight stays off until repaired; only a glitch-enabled display flickers.
                    half damagedPower = _GlitchStrength > 0.001h ? flicker : 0.0h;
                    half power = lerp(1.0h, damagedPower, saturate(_DamageAmount));
                    half screenCracks = saturate(cracks * 1.5h);
                    color = lerp(color, half3(0.008h, 0.012h, 0.02h), screenCracks);
                    color += emissionMask * _RepairedEmission.rgb * power * _Electronics * (1.0h - screenCracks);
                }
                return half4(MixFog(color, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Depth Only"
            Cull Off
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
