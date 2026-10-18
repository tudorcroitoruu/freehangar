Shader "Custom/PipesBoard"
{
    Properties
    {
        _GridData ("Grid Data", 2D) = "black" {}
        _GridSize ("Grid Size", Vector) = (8,8,0,0)
        _EmptyPipeColor ("Empty Pipe", Color) = (0.92,0.90,0.84,1)
        _PipeOutlineColor ("Pipe Outline", Color) = (0.10,0.10,0.10,1)
        _WaterColor ("Water", Color) = (0.24,0.48,0.86,1)
        _SourceColor ("Source", Color) = (0.18,0.76,1.0,1)
        _BackgroundColor ("Background", Color) = (0.96,0.95,0.90,0)
        _PipeRadius ("Pipe Radius", Range(0.04,0.28)) = 0.145
        _OutlineWidth ("Outline Width", Range(0.0,0.08)) = 0.025
        _CellInset ("Cell Inset", Range(0.0,0.25)) = 0.08
        _ConnectionOverlap ("Connection Overlap", Range(0.0,0.08)) = 0.055
        _JointRadius ("Joint Radius", Range(0.04,0.34)) = 0.155
        _TerminalRadius ("Terminal Radius", Range(0.04,0.36)) = 0.19
        _SourceRadius ("Source Radius", Range(0.06,0.42)) = 0.22
        _WaterLeadSoftness ("Water Lead Softness", Range(0.01,0.25)) = 0.08
        _CompletePhase ("Complete Phase", Range(0.0,1.0)) = 0
        _SplashIntensity ("Splash Intensity", Range(0.0,1.0)) = 0.28
        _VisualStyle ("Visual Style", Range(0.0,3.0)) = 0
        _Pressure ("Pressure", Range(0.0,1.0)) = 0
        _LockRopeTex ("Lock Rope Texture", 2D) = "white" {}
        _LockRopeTex2 ("Lock Rope Texture 2", 2D) = "white" {}
        _LockRopeTex3 ("Lock Rope Texture 3", 2D) = "white" {}
        _LockRopeTex4 ("Lock Rope Texture 4", 2D) = "white" {}
        _LockRopeTex5 ("Lock Rope Texture 5", 2D) = "white" {}
        _HammerTex ("Hammer Texture", 2D) = "white" {}
        _HammerTex2 ("Hammer Texture 2", 2D) = "white" {}
        _HammerTex3 ("Hammer Texture 3", 2D) = "white" {}
        _HammerTex4 ("Hammer Texture 4", 2D) = "white" {}
        _HammerTex5 ("Hammer Texture 5", 2D) = "white" {}
        _UseLockRopeTex ("Use Lock Rope Texture", Range(0.0,1.0)) = 0
        _UseLockRopeTex2 ("Use Lock Rope Texture 2", Range(0.0,1.0)) = 0
        _UseLockRopeTex3 ("Use Lock Rope Texture 3", Range(0.0,1.0)) = 0
        _UseLockRopeTex4 ("Use Lock Rope Texture 4", Range(0.0,1.0)) = 0
        _UseLockRopeTex5 ("Use Lock Rope Texture 5", Range(0.0,1.0)) = 0
        _UseHammerTex ("Use Hammer Texture", Range(0.0,1.0)) = 0
        _UseHammerTex2 ("Use Hammer Texture 2", Range(0.0,1.0)) = 0
        _UseHammerTex3 ("Use Hammer Texture 3", Range(0.0,1.0)) = 0
        _UseHammerTex4 ("Use Hammer Texture 4", Range(0.0,1.0)) = 0
        _UseHammerTex5 ("Use Hammer Texture 5", Range(0.0,1.0)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_GridData); SAMPLER(sampler_GridData);
            TEXTURE2D(_LockRopeTex); SAMPLER(sampler_LockRopeTex);
            TEXTURE2D(_LockRopeTex2); SAMPLER(sampler_LockRopeTex2);
            TEXTURE2D(_LockRopeTex3); SAMPLER(sampler_LockRopeTex3);
            TEXTURE2D(_LockRopeTex4); SAMPLER(sampler_LockRopeTex4);
            TEXTURE2D(_LockRopeTex5); SAMPLER(sampler_LockRopeTex5);
            TEXTURE2D(_HammerTex); SAMPLER(sampler_HammerTex);
            TEXTURE2D(_HammerTex2); SAMPLER(sampler_HammerTex2);
            TEXTURE2D(_HammerTex3); SAMPLER(sampler_HammerTex3);
            TEXTURE2D(_HammerTex4); SAMPLER(sampler_HammerTex4);
            TEXTURE2D(_HammerTex5); SAMPLER(sampler_HammerTex5);
            float4 _GridSize;
            float4 _EmptyPipeColor;
            float4 _PipeOutlineColor;
            float4 _WaterColor;
            float4 _SourceColor;
            float4 _BackgroundColor;
            float _PipeRadius;
            float _OutlineWidth;
            float _CellInset;
            float _ConnectionOverlap;
            float _JointRadius;
            float _TerminalRadius;
            float _SourceRadius;
            float _WaterLeadSoftness;
            float _CompletePhase;
            float _SplashIntensity;
            float _VisualStyle;
            float _Pressure;
            float _UseLockRopeTex;
            float _UseLockRopeTex2;
            float _UseLockRopeTex3;
            float _UseLockRopeTex4;
            float _UseLockRopeTex5;
            float _UseHammerTex;
            float _UseHammerTex2;
            float _UseHammerTex3;
            float _UseHammerTex4;
            float _UseHammerTex5;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(0.00001, dot(ba, ba)));
                return length(pa - ba * h);
            }

            float2 rotate2(float2 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float bitOn(float mask, float bit)
            {
                return step(0.5, fmod(floor(mask / bit), 2.0));
            }

            float bitCount(float mask)
            {
                return bitOn(mask, 1.0) + bitOn(mask, 2.0) + bitOn(mask, 4.0) + bitOn(mask, 8.0);
            }

            float sdBox(float2 p, float2 b)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            float ropeBand(float2 p, float offset)
            {
                float wave = sin((p.x + offset) * 28.0 + _Time.y * 4.0) * 0.025;
                float strand = smoothstep(0.045, 0.0, abs(p.y + wave));
                float braid = smoothstep(0.028, 0.0, abs(frac((p.x + offset) * 6.5 + p.y * 2.0) - 0.5));
                return strand * (0.72 + 0.28 * braid);
            }

            float drawHammer(float2 p, float swing)
            {
                p = rotate2(p, -0.55 + swing);
                float handle = smoothstep(0.035, 0.0, sdSegment(p, float2(-0.18, -0.28), float2(0.18, 0.18)));
                float head = smoothstep(0.035, 0.0, sdBox(p - float2(0.2, 0.22), float2(0.18, 0.07)));
                float claw = smoothstep(0.025, 0.0, sdSegment(p, float2(0.34, 0.22), float2(0.45, 0.3)));
                return saturate(handle + head + claw);
            }

            float4 sampleLockRopeTexture(float index, float2 uv, out float useTexture)
            {
                if (index < 0.5)
                {
                    useTexture = _UseLockRopeTex;
                    return SAMPLE_TEXTURE2D(_LockRopeTex, sampler_LockRopeTex, uv);
                }
                if (index < 1.5)
                {
                    useTexture = _UseLockRopeTex2;
                    return SAMPLE_TEXTURE2D(_LockRopeTex2, sampler_LockRopeTex2, uv);
                }
                if (index < 2.5)
                {
                    useTexture = _UseLockRopeTex3;
                    return SAMPLE_TEXTURE2D(_LockRopeTex3, sampler_LockRopeTex3, uv);
                }
                if (index < 3.5)
                {
                    useTexture = _UseLockRopeTex4;
                    return SAMPLE_TEXTURE2D(_LockRopeTex4, sampler_LockRopeTex4, uv);
                }

                useTexture = _UseLockRopeTex5;
                return SAMPLE_TEXTURE2D(_LockRopeTex5, sampler_LockRopeTex5, uv);
            }

            float4 sampleHammerTexture(float index, float2 uv, out float useTexture)
            {
                if (index < 0.5)
                {
                    useTexture = _UseHammerTex;
                    return SAMPLE_TEXTURE2D(_HammerTex, sampler_HammerTex, uv);
                }
                if (index < 1.5)
                {
                    useTexture = _UseHammerTex2;
                    return SAMPLE_TEXTURE2D(_HammerTex2, sampler_HammerTex2, uv);
                }
                if (index < 2.5)
                {
                    useTexture = _UseHammerTex3;
                    return SAMPLE_TEXTURE2D(_HammerTex3, sampler_HammerTex3, uv);
                }
                if (index < 3.5)
                {
                    useTexture = _UseHammerTex4;
                    return SAMPLE_TEXTURE2D(_HammerTex4, sampler_HammerTex4, uv);
                }

                useTexture = _UseHammerTex5;
                return SAMPLE_TEXTURE2D(_HammerTex5, sampler_HammerTex5, uv);
            }

            float isCornerMask(float mask)
            {
                float up = bitOn(mask, 1.0);
                float down = bitOn(mask, 2.0);
                float left = bitOn(mask, 4.0);
                float right = bitOn(mask, 8.0);
                float count = up + down + left + right;
                float straight = max(up * down, left * right);
                return step(1.5, count) * step(count, 2.5) * (1.0 - straight);
            }

            float pipeDistance(float2 p, float mask)
            {
                float d = 99.0;
                float end = 0.5 + _ConnectionOverlap;
                float up = bitOn(mask, 1.0);
                float down = bitOn(mask, 2.0);
                float left = bitOn(mask, 4.0);
                float right = bitOn(mask, 8.0);
                float count = up + down + left + right;

                if (up > 0.5 && down > 0.5 && left < 0.5 && right < 0.5)
                    d = min(d, sdSegment(p, float2(0, -end), float2(0, end)));
                else if (left > 0.5 && right > 0.5 && up < 0.5 && down < 0.5)
                    d = min(d, sdSegment(p, float2(-end, 0), float2(end, 0)));
                else
                {
                    if (up > 0.5) d = min(d, sdSegment(p, float2(0, 0), float2(0, end)));
                    if (down > 0.5) d = min(d, sdSegment(p, float2(0, 0), float2(0, -end)));
                    if (left > 0.5) d = min(d, sdSegment(p, float2(0, 0), float2(-end, 0)));
                    if (right > 0.5) d = min(d, sdSegment(p, float2(0, 0), float2(end, 0)));
                    d = min(d, length(p) - max(_JointRadius - _PipeRadius, 0.0));
                }

                if (count <= 1.5)
                    d = min(d, length(p) - max(_TerminalRadius - _PipeRadius, 0.0));

                return d;
            }

            float2 dirForBit(float bit)
            {
                if (abs(bit - 1.0) < 0.5) return float2(0, 1);
                if (abs(bit - 2.0) < 0.5) return float2(0, -1);
                if (abs(bit - 4.0) < 0.5) return float2(-1, 0);
                if (abs(bit - 8.0) < 0.5) return float2(1, 0);
                return float2(0, 0);
            }

            float waterReveal(float2 p, float progress, float fillBit, float isSource)
            {
                progress = saturate(progress);
                if (isSource > 0.5)
                    return progress;
                if (fillBit < 0.5)
                    return progress;
                if (progress <= 0.001)
                    return 0.0;

                float2 inlet = dirForBit(fillBit);
                float travel = dot(p, -inlet) + 0.5;
                float across = dot(p, float2(-inlet.y, inlet.x));
                float waveA = sin(across * 18.0 + _Time.y * 5.5 + progress * 7.0) * 0.025;
                float waveB = sin(across * 37.0 - _Time.y * 3.0 + progress * 11.0) * 0.012;
                float head = progress * 1.12 + waveA + waveB;
                float revealSoftness = min(_WaterLeadSoftness, 0.055);
                float front = smoothstep(head + revealSoftness * 0.35, head - revealSoftness, travel);
                float ripple = sin((travel - head) * 42.0 - _Time.y * 7.0) * 0.5 + 0.5;
                front += smoothstep(revealSoftness * 1.4, 0.0, abs(travel - head)) * ripple * 0.16;
                return saturate(front);
            }

            float waterLead(float2 p, float progress, float fillBit, float isSource)
            {
                progress = saturate(progress);
                if (isSource > 0.5 || fillBit < 0.5 || progress <= 0.001 || progress >= 0.999)
                    return 0.0;

                float2 inlet = dirForBit(fillBit);
                float travel = dot(p, -inlet) + 0.5;
                float head = progress * 1.12;
                return smoothstep(_WaterLeadSoftness * 1.8, 0.0, abs(travel - head));
            }

            float3 laserPalette(float t)
            {
                return 0.58 + 0.42 * cos(6.28318 * (float3(0.0, 0.33, 0.67) + t));
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 grid = i.uv * _GridSize.xy;
                float2 cell = floor(grid);
                float2 local = frac(grid) - 0.5;
                float2 sampleUv = (cell + 0.5) / max(float2(1.0, 1.0), _GridSize.xy);
                float4 data = SAMPLE_TEXTURE2D(_GridData, sampler_GridData, sampleUv);

                if (data.a < 0.0001)
                    return half4(_BackgroundColor.rgb, 0.0);

                float rawMask = saturate(data.r) * 15.0;
                float baseMask = round(rawMask);
                float splashPulse = saturate(abs(rawMask - baseMask) * 2.25);
                float visualRotation = data.g * 4.0;
                float water = saturate(data.b);
                float packedCode = max(0.0, round(data.a * 32768.0 - 128.0));
                float fillBit = fmod(packedCode, 16.0);
                float isSource = bitOn(packedCode, 16.0);
                float isLocked = bitOn(packedCode, 32.0);
                float hasHammer = bitOn(packedCode, 64.0);
                float isBreaking = bitOn(packedCode, 128.0);
                float openEndMask = fmod(floor(packedCode / 256.0), 16.0);
                float lockHammerTextureIndex = clamp(floor(packedCode / 4096.0), 0.0, 4.0);
                float angle = visualRotation * 1.57079632679;
                float2 scaledLocal = local;
                float2 pipeLocal = rotate2(scaledLocal, angle);
                float d = pipeDistance(pipeLocal, baseMask);
                float sourceDist = length(scaledLocal) - _SourceRadius;
                d = min(d, lerp(99.0, sourceDist, isSource));

                float outlineRadius = _PipeRadius + _OutlineWidth;
                float outline = smoothstep(outlineRadius + 0.015, outlineRadius - 0.005, d);
                float body = smoothstep(_PipeRadius + 0.012, _PipeRadius - 0.006, d);

                float style = round(_VisualStyle);
                float styleTime = _Time.y * 0.7;
                float splashAge = 1.0 - splashPulse;
                float wobble = sin(splashAge * 24.0) * splashPulse * 0.075;
                float2 liquidLocal = pipeLocal + float2(wobble, -wobble * 0.35);
                float waterFront = waterReveal(liquidLocal, water, fillBit, isSource);
                float laserT = frac(grid.x * 0.11 + grid.y * 0.17 + styleTime);
                half3 waterColor = lerp(_WaterColor.rgb, _SourceColor.rgb, isSource * 0.35);
                if (style > 1.5 && style < 2.5)
                {
                    float heat = 0.72 + 0.28 * sin((dot(pipeLocal, float2(3.0, 5.0)) + styleTime * 3.0) * 8.0);
                    waterColor = lerp(float3(1.0, 0.18, 0.03), float3(1.0, 0.88, 0.12), heat * waterFront);
                }
                else if (style > 2.5)
                {
                    waterColor = laserPalette(laserT);
                }

                half3 pipeColor = lerp(_EmptyPipeColor.rgb, waterColor, waterFront);

                float alpha = max(max(outline * _PipeOutlineColor.a, body), _BackgroundColor.a);
                half3 color = _BackgroundColor.rgb;
                color = lerp(color, _PipeOutlineColor.rgb, outline);
                color = lerp(color, pipeColor, body);

                float hintPulse = splashPulse * (0.55 + 0.45 * sin(_Time.y * 18.0 + cell.x * 1.17 + cell.y * 2.13));
                half3 hintColor = lerp(waterColor, _SourceColor.rgb + float3(0.22, 0.22, 0.18), 0.35);
                color = lerp(color, hintColor, saturate((body * 0.42 + outline * 0.32) * hintPulse));
                color += hintColor * saturate(outline * splashPulse * 0.28);

                float glowStrength = lerp(0.18, 0.42, step(1.5, style));
                float centerGlow = smoothstep(0.28, 0.0, length(local)) * waterFront * glowStrength;
                color += waterColor * centerGlow;

                float pressure = splashPulse * waterFront;
                float flowBand = sin((dot(liquidLocal, float2(7.0, 3.5)) - _Time.y * 5.5) * 6.28318) * 0.5 + 0.5;
                flowBand = smoothstep(0.74, 1.0, flowBand) * body * pressure;
                float cornerImpact = isCornerMask(baseMask) * smoothstep(0.28, 0.02, length(liquidLocal)) * pressure;
                float cornerSpark = cornerImpact * (0.68 + 0.32 * sin(_Time.y * 18.0 + cell.x + cell.y));
                color += waterColor * (flowBand * 0.84 + cornerSpark * 1.56);

                float waterBody = body * waterFront;
                float glossSweep = sin((liquidLocal.x * 4.0 + liquidLocal.y * 5.5) - _Time.y * 2.2) * 0.5 + 0.5;
                glossSweep = smoothstep(0.52, 1.0, glossSweep);
                float softBandA = smoothstep(0.24, 0.0, abs(liquidLocal.y - liquidLocal.x * 0.28 - 0.09));
                float softBandB = smoothstep(0.18, 0.0, abs(liquidLocal.y + liquidLocal.x * 0.18 + 0.04));
                float rimGloss = smoothstep(_PipeRadius * 1.35, _PipeRadius * 0.35, abs(pipeDistance(pipeLocal, baseMask))) * 0.18;
                float sourceGloss = isSource * smoothstep(_SourceRadius * 1.05, _SourceRadius * 0.25, length(scaledLocal)) * 0.18;
                float waterGloss = waterBody * (softBandA * 0.11 + softBandB * 0.07 + glossSweep * 0.055 + rimGloss + sourceGloss);
                color = lerp(color, waterColor + float3(0.28, 0.34, 0.36), saturate(waterGloss));

                float splashSeed = frac(sin(dot(cell + 1.0, float2(12.9898, 78.233))) * 43758.5453);
                float2 dropDirA = normalize(float2(splashSeed - 0.5, frac(splashSeed * 2.31) - 0.5) + 0.001);
                float2 dropDirB = normalize(float2(frac(splashSeed * 5.17) - 0.5, frac(splashSeed * 7.91) - 0.5) + 0.001);
                float sideOffsetA = lerp(-0.18, 0.18, frac(splashSeed * 3.17));
                float sideOffsetB = lerp(-0.18, 0.18, frac(splashSeed * 6.83 + 0.37));
                float sideOffsetC = lerp(-0.12, 0.12, frac(splashSeed * 9.41 + 0.62));
                float drops = 0.0;
                float endFill = smoothstep(0.99, 1.0, water) * (1.0 - isSource);
                float endPulse = 0.45 + 0.55 * sin(_Time.y * 15.0 + splashSeed * 6.28318);
                float leakCycleA = frac(_Time.y * 1.9 + splashSeed);
                float leakCycleB = frac(_Time.y * 2.3 + splashSeed * 1.73 + 0.45);
                float leakSizeA = max(0.05, sin(leakCycleA * 3.14159));
                float leakSizeB = max(0.05, sin(leakCycleB * 3.14159));
                float endDrops = 0.0;
                float activeOpen = bitOn(openEndMask, 1.0);
                float2 exitDir = dirForBit(1.0);
                float2 exitCenter = rotate2(exitDir * 0.43, -angle);
                float2 sprayDir = rotate2(exitDir, -angle);
                float2 sideDir = float2(-sprayDir.y, sprayDir.x);
                float mouth = smoothstep(0.18, 0.0, length(local - exitCenter));
                float endDropA = smoothstep(0.096 * leakSizeA, 0.0, length(local - exitCenter - sprayDir * lerp(0.03, 0.22, leakCycleA) - sideDir * sideOffsetA - dropDirA * 0.035));
                float endDropB = smoothstep(0.072 * leakSizeB, 0.0, length(local - exitCenter - sprayDir * lerp(0.04, 0.25, leakCycleB) - sideDir * sideOffsetB - dropDirB * 0.04));
                float endDropC = smoothstep(0.066, 0.0, length(local - exitCenter - sprayDir * lerp(0.02, 0.16, frac(leakCycleA + 0.33)) - sideDir * sideOffsetC));
                endDrops += activeOpen * (mouth * 0.35 + endDropA + endDropB * 0.8 + endDropC * 0.7);

                activeOpen = bitOn(openEndMask, 2.0);
                exitDir = dirForBit(2.0);
                exitCenter = rotate2(exitDir * 0.43, -angle);
                sprayDir = rotate2(exitDir, -angle);
                sideDir = float2(-sprayDir.y, sprayDir.x);
                mouth = smoothstep(0.18, 0.0, length(local - exitCenter));
                endDropA = smoothstep(0.096 * leakSizeA, 0.0, length(local - exitCenter - sprayDir * lerp(0.03, 0.22, leakCycleA) - sideDir * sideOffsetA - dropDirA * 0.035));
                endDropB = smoothstep(0.072 * leakSizeB, 0.0, length(local - exitCenter - sprayDir * lerp(0.04, 0.25, leakCycleB) - sideDir * sideOffsetB - dropDirB * 0.04));
                endDropC = smoothstep(0.066, 0.0, length(local - exitCenter - sprayDir * lerp(0.02, 0.16, frac(leakCycleA + 0.33)) - sideDir * sideOffsetC));
                endDrops += activeOpen * (mouth * 0.35 + endDropA + endDropB * 0.8 + endDropC * 0.7);

                activeOpen = bitOn(openEndMask, 4.0);
                exitDir = dirForBit(4.0);
                exitCenter = rotate2(exitDir * 0.43, -angle);
                sprayDir = rotate2(exitDir, -angle);
                sideDir = float2(-sprayDir.y, sprayDir.x);
                mouth = smoothstep(0.18, 0.0, length(local - exitCenter));
                endDropA = smoothstep(0.096 * leakSizeA, 0.0, length(local - exitCenter - sprayDir * lerp(0.03, 0.22, leakCycleA) - sideDir * sideOffsetA - dropDirA * 0.035));
                endDropB = smoothstep(0.072 * leakSizeB, 0.0, length(local - exitCenter - sprayDir * lerp(0.04, 0.25, leakCycleB) - sideDir * sideOffsetB - dropDirB * 0.04));
                endDropC = smoothstep(0.066, 0.0, length(local - exitCenter - sprayDir * lerp(0.02, 0.16, frac(leakCycleA + 0.33)) - sideDir * sideOffsetC));
                endDrops += activeOpen * (mouth * 0.35 + endDropA + endDropB * 0.8 + endDropC * 0.7);

                activeOpen = bitOn(openEndMask, 8.0);
                exitDir = dirForBit(8.0);
                exitCenter = rotate2(exitDir * 0.43, -angle);
                sprayDir = rotate2(exitDir, -angle);
                sideDir = float2(-sprayDir.y, sprayDir.x);
                mouth = smoothstep(0.18, 0.0, length(local - exitCenter));
                endDropA = smoothstep(0.096 * leakSizeA, 0.0, length(local - exitCenter - sprayDir * lerp(0.03, 0.22, leakCycleA) - sideDir * sideOffsetA - dropDirA * 0.035));
                endDropB = smoothstep(0.072 * leakSizeB, 0.0, length(local - exitCenter - sprayDir * lerp(0.04, 0.25, leakCycleB) - sideDir * sideOffsetB - dropDirB * 0.04));
                endDropC = smoothstep(0.066, 0.0, length(local - exitCenter - sprayDir * lerp(0.02, 0.16, frac(leakCycleA + 0.33)) - sideDir * sideOffsetC));
                endDrops += activeOpen * (mouth * 0.35 + endDropA + endDropB * 0.8 + endDropC * 0.7);
                endDrops *= endFill * endPulse * _SplashIntensity;
                drops += endDrops;
                color += waterColor * drops;

                float2 cellUv = (cell + 0.5) / max(float2(1.0, 1.0), _GridSize.xy);
                float completeDistance = length((cellUv - 0.5) * float2(_GridSize.x / max(_GridSize.y, 1.0), 1.0));
                float completeHead = _CompletePhase * 0.95;
                float completeWave = smoothstep(0.12, 0.0, abs(completeDistance - completeHead)) * body * waterFront;
                float completeGlow = saturate(_CompletePhase) * body * waterFront * 0.08;
                color += waterColor * (completeWave * 0.38 + completeGlow);

                float lockBreak = isLocked * isBreaking * splashPulse;
                float ropeAlive = isLocked * (1.0 - smoothstep(0.78, 1.0, lockBreak));
                float scaredShake = isLocked * isBreaking * (1.0 - smoothstep(0.82, 1.0, lockBreak));
                float scaredSeed = cell.x * 1.91 + cell.y * 2.37;
                float2 scaredOffset = scaredShake * float2(
                    sin(_Time.y * 42.0 + scaredSeed),
                    cos(_Time.y * 37.0 + scaredSeed * 1.31)) * 0.045;
                float scaredTwist = scaredShake * sin(_Time.y * 55.0 + scaredSeed * 0.73) * 0.16;
                float2 ropeLocal = rotate2(local + scaredOffset, scaredTwist);
                float ropeA = ropeBand(ropeLocal, 0.0);
                float ropeB = ropeBand(rotate2(ropeLocal, 1.5708), 0.08);
                float knot = smoothstep(0.12, 0.0, length(ropeLocal));
                float fraySeed = frac(sin(dot(cell + 2.0, float2(41.13, 17.71))) * 9317.34);
                float fray = smoothstep(0.035, 0.0, sdSegment(ropeLocal, float2(-0.36, 0.18), float2(-0.18, 0.02)));
                fray += smoothstep(0.035, 0.0, sdSegment(ropeLocal, float2(0.36, -0.18), float2(0.18, -0.02)));
                float ropeMask = saturate((ropeA + ropeB + knot * 0.85 + fray * 0.45) * ropeAlive);
                float crack = isLocked * lockBreak * smoothstep(0.055, 0.0, abs(ropeLocal.x + sin(ropeLocal.y * 18.0) * 0.035));
                ropeMask *= 1.0 - crack;
                half3 ropeColor = lerp(float3(0.34, 0.2, 0.11), float3(0.78, 0.58, 0.32), frac((ropeLocal.x - ropeLocal.y) * 9.0 + fraySeed));
                float2 ropeUv = saturate(ropeLocal * 0.6 + 0.5);
                float useRopeTex;
                float4 ropeTex = sampleLockRopeTexture(lockHammerTextureIndex, ropeUv, useRopeTex);
                ropeColor = lerp(ropeColor, ropeTex.rgb, useRopeTex * ropeTex.a);
                ropeMask = lerp(ropeMask, ropeTex.a * ropeAlive, useRopeTex);
                color = lerp(color, ropeColor, ropeMask);

                float hammerIcon = hasHammer * drawHammer(local - float2(0.08, 0.02), 0.15 * sin(_Time.y * 5.0));
                float strikeHammer = isLocked * isBreaking * drawHammer(local - float2(0.18, 0.08), sin(_Time.y * 28.0) * 0.55 * (1.0 - lockBreak));
                float hammerMask = saturate(hammerIcon + strikeHammer);
                half3 hammerColor = lerp(float3(0.42, 0.25, 0.12), float3(0.84, 0.82, 0.75), step(0.36, local.y + local.x));
                float2 hammerUv = saturate(local + 0.5);
                float useHammerTex;
                float4 hammerTex = sampleHammerTexture(lockHammerTextureIndex, hammerUv, useHammerTex);
                hammerColor = lerp(hammerColor, hammerTex.rgb, useHammerTex * hammerTex.a);
                hammerMask = lerp(hammerMask, hammerTex.a * saturate(hasHammer + isLocked * isBreaking), useHammerTex);
                color = lerp(color, hammerColor, hammerMask);
                float impact = isLocked * isBreaking * smoothstep(0.24, 0.0, length(local)) * (1.0 - lockBreak) * (0.55 + 0.45 * sin(_Time.y * 36.0));
                color += float3(1.0, 0.78, 0.24) * impact;

                alpha = max(alpha, saturate(drops));
                alpha = max(alpha, saturate(ropeMask + hammerMask + impact));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
