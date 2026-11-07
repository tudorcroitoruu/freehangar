Shader "Freehangar/HDRP/Grain Kernels"
{
    Properties
    {
        _GrainWeightsA("Corn / Wheat / Barley / Oats", Vector) = (1,0,0,0)
        _GrainWeightsB("Rye / Rice / Peas / Soybeans", Vector) = (0,0,0,0)
        _GrainWeightsC("Sunflower / Buckwheat", Vector) = (0,0,0,0)
        [Toggle] _KernelMode("3D kernel (off for pile substrate)", Float) = 1
        _Brightness("Brightness", Range(0.1,2)) = 0.8
        _DetailScale("Substrate grain scale", Float) = 48
        _SurfaceNormalStrength("Grain normal strength", Range(0, 4)) = 1.4
        [HideInInspector] _GrainSurfaceDetail("Grain surface normals and height", 2DArray) = "" {}
        [HideInInspector] _UseSurfaceDetail("Textured grain surface", Float) = 0
        [HideInInspector] _GrainType("Particle crop override", Float) = -1
        [HideInInspector] _UseSpatialGrain("Spatial grain", Float) = 0
        [HideInInspector] _GrainMapA("Spatial grain A", 2D) = "black" {}
        [HideInInspector] _GrainMapB("Spatial grain B", 2D) = "black" {}
        [HideInInspector] _GrainMapC("Spatial grain C", 2D) = "black" {}
        [HideInInspector] _PileSurfaceHeights("Animated pile heights", 2D) = "black" {}
        [HideInInspector] _UsePileSurface("Follow animated pile", Float) = 0
        [HideInInspector] _KernelDistanceFade("Kernel distance fade", Vector) = (0,0,0,0)
    }
    HLSLINCLUDE
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    CBUFFER_START(UnityPerMaterial)
    float4 _GrainWeightsA, _GrainWeightsB, _GrainWeightsC;
    float _KernelMode, _Brightness, _DetailScale;
    float _UseSurfaceDetail;
    float _SurfaceNormalStrength;
    float _UseSpatialGrain;
    float4 _GrainGrid;
    float4x4 _GrainWorldToLocal;
    float _UsePileSurface;
    float4 _KernelDistanceFade;
    float4 _PileSurfaceGrid; // size, resolution
    float4x4 _PileSurfaceWorldToLocal, _PileSurfaceLocalToWorld;
    CBUFFER_END
    TEXTURE2D(_GrainMapA); SAMPLER(sampler_GrainMapA);
    TEXTURE2D(_GrainMapB); SAMPLER(sampler_GrainMapB);
    TEXTURE2D(_GrainMapC); SAMPLER(sampler_GrainMapC);
    TEXTURE2D(_PileSurfaceHeights);
    TEXTURE2D_ARRAY(_GrainSurfaceDetail); SAMPLER(sampler_GrainSurfaceDetail);

    float3 FollowPile(float3 origin, inout float3 offset, inout float3 normal)
    {
        float3 local = mul(_PileSurfaceWorldToLocal, float4(origin, 1)).xyz;
        float2 grid = saturate(local.xz / _PileSurfaceGrid.x + .5) * (_PileSurfaceGrid.y - 1);
        int2 cell = min((int2)floor(grid), (int)_PileSurfaceGrid.y - 2);
        float2 f = grid - cell;
        float2 a = LOAD_TEXTURE2D(_PileSurfaceHeights, cell).rg;
        float2 b = LOAD_TEXTURE2D(_PileSurfaceHeights, cell + int2(1, 0)).rg;
        float2 c = LOAD_TEXTURE2D(_PileSurfaceHeights, cell + int2(0, 1)).rg;
        float2 d = LOAD_TEXTURE2D(_PileSurfaceHeights, cell + int2(1, 1)).rg;
        float2 height = f.x + f.y <= 1 ? a * (1-f.x-f.y) + b*f.x + c*f.y :
            d*(f.x+f.y-1) + c*(1-f.x) + b*(1-f.y);
        float ah = a.x + a.y, bh = b.x + b.y, ch = c.x + c.y, dh = d.x + d.y;
        float2 slope = f.x + f.y <= 1 ? float2(bh-ah, ch-ah) : float2(dh-ch, dh-bh);
        float spacing = _PileSurfaceGrid.x / (_PileSurfaceGrid.y - 1);
        float3 localNormal = normalize(float3(-slope.x, spacing, -slope.y));
        float3 up = normalize(mul(localNormal, (float3x3)_PileSurfaceWorldToLocal));
        // Shortest rotation from world up to the current slope. Instance matrices
        // retain only the grain's stable random rotation, never a queued old slope.
        float3 axis = cross(float3(0,1,0), up);
        offset += cross(axis, offset) + cross(axis, cross(axis, offset)) / max(.001, 1+up.y);
        normal += cross(axis, normal) + cross(axis, cross(axis, normal)) / max(.001, 1+up.y);
        float worldHeight = length(_PileSurfaceLocalToWorld._m01_m11_m21) * height.x;
        offset *= smoothstep(0, .012, worldHeight);
        local.y = height.x + height.y;
        return mul(_PileSurfaceLocalToWorld, float4(local, 1)).xyz + float3(0,.003,0);
    }
    UNITY_INSTANCING_BUFFER_START(GrainInstances)
        UNITY_DEFINE_INSTANCED_PROP(float, _GrainType)
    UNITY_INSTANCING_BUFFER_END(GrainInstances)
    float Hash(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
    int Species(float seed, float3 world)
    {
        float4 a = _GrainWeightsA, b = _GrainWeightsB, c = _GrainWeightsC;
        if (_UseSpatialGrain > .5)
        {
            float3 local = mul(_GrainWorldToLocal, float4(world,1)).xyz;
            float2 uv = (local.xz / _GrainGrid.x + .5) * ((_GrainGrid.y-1)/_GrainGrid.y) + .5/_GrainGrid.y;
            a = SAMPLE_TEXTURE2D_LOD(_GrainMapA, sampler_GrainMapA, uv, 0);
            b = SAMPLE_TEXTURE2D_LOD(_GrainMapB, sampler_GrainMapB, uv, 0);
            c = SAMPLE_TEXTURE2D_LOD(_GrainMapC, sampler_GrainMapC, uv, 0);
        }
        float sum = dot(a, 1) + dot(b, 1) + c.x + c.y;
        if (sum < 0.00001) return 0;
        float target = seed * sum, accumulated = 0;
        for (int t = 0; t < 10; t++)
        {
            if (t < 4) accumulated += a[min(t,3)];
            else if (t < 8) accumulated += b[clamp(t-4,0,3)];
            else accumulated += c[clamp(t-8,0,3)];
            if (target < accumulated) return t;
        }
        return 9;
    }
    float3 Dimensions(int t)
    {
        if (t == 1) return float3(.64,.72,1.02);
        if (t == 2) return float3(.48,.66,1.2);
        if (t == 3) return float3(.35,.5,1.32);
        if (t == 4) return float3(.42,.57,1.17);
        if (t == 5) return float3(.38,.54,1.13);
        if (t == 6) return float3(.8,1.4,.70);
        if (t == 7) return float3(.85,1.35,.73);
        if (t == 8) return float3(.60,.45,1.14);
        if (t == 9) return float3(.75,.95,.78);
        return 1;
    }
    float3 GrainColor(int t)
    {
        if (t == 1) return float3(.58,.36,.13);
        if (t == 2) return float3(.72,.51,.24);
        if (t == 3) return float3(.77,.65,.42);
        if (t == 4) return float3(.35,.29,.17);
        if (t == 5) return float3(.92,.88,.75);
        if (t == 6) return float3(.22,.42,.08);
        if (t == 7) return float3(.72,.59,.29);
        if (t == 8) return float3(.065,.055,.047);
        if (t == 9) return float3(.27,.12,.055);
        return float3(.88,.49,.055);
    }
    struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv1:TEXCOORD1; float2 uv2:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct Varyings
    {
        float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 local:TEXCOORD1;
        float3 world:TEXCOORD2; nointerpolation float species:TEXCOORD3;
        nointerpolation float variation:TEXCOORD4; float4 lightmapUV:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
    };
    Varyings Vert(Attributes input)
    {
        Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float3 origin = GetAbsolutePositionWS(TransformObjectToWorld(float3(0,0,0)));
        float3 stableSeed = origin;
        #if defined(UNITY_INSTANCING_ENABLED)
        stableSeed = float3(unity_InstanceID, 13, 7);
        #endif
        if (_UsePileSurface > .5) stableSeed = float3(origin.x, 0, origin.z);
        else if (_KernelDistanceFade.y > 0) stableSeed = origin;
        float seed = Hash(stableSeed * 31.7);
        int t = Species(seed, origin);
        float overrideType = UNITY_ACCESS_INSTANCED_PROP(GrainInstances, _GrainType);
        if (overrideType >= 0) t = (int)overrideType;
        float3 p = input.positionOS, n = input.normalOS;
        o.local = normalize(p + .00001);
        if (_KernelMode > .5 && t != 0)
        {
            float3 radial = normalize(p + .00001);
            float3 shape = Dimensions(t);
            // Barley/oats/rye and sunflower taper toward the husk tip.
            if (t == 2 || t == 3 || t == 4 || t == 8) shape.x *= 1 - .35 * radial.z;
            if (t == 9)
            {
                float angle = atan2(radial.z, radial.x);
                float triangular = .55 / cos(fmod(angle + 6.2831853, 2.0943951) - 1.04719755);
                shape.xz *= triangular;
            }
            p = radial * shape;
            n = normalize(radial / shape);
        }
        o.world = GetAbsolutePositionWS(TransformObjectToWorld(p));
        o.normalWS = TransformObjectToWorldNormal(n);
        float3 kernelOrigin = origin;
        if (_UsePileSurface > .5 && _KernelMode > .5)
        {
            float3 offset = o.world - origin;
            float3 surfaceOrigin = FollowPile(origin, offset, o.normalWS);
            kernelOrigin = surfaceOrigin;
            o.world = surfaceOrigin + offset;
        }
        if (_KernelMode > .5 && _KernelDistanceFade.y > 0)
        {
            float viewDistance = length(GetWorldSpaceViewDir(GetCameraRelativePositionWS(kernelOrigin)));
            float detail = 1 - smoothstep(_KernelDistanceFade.x, _KernelDistanceFade.y, viewDistance);
            o.world = kernelOrigin + (o.world - kernelOrigin) * detail;
        }
        o.positionCS = TransformWorldToHClip(GetCameraRelativePositionWS(o.world));
        o.lightmapUV = float4(input.uv1, input.uv2); o.species = t; o.variation = .86 + Hash(stableSeed * 73.3) * .28;
        return o;
    }
    float3 GrainAlbedo(Varyings i)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        int t = (int)i.species;
        float3 p = i.local;
        float variation = i.variation;
        if (_KernelMode < .5)
        {
            float2 cell = floor(i.world.xz * _DetailScale);
            t = Species(Hash(float3(cell, 17)), i.world);
            p = float3(frac(i.world.x * _DetailScale) * 2 - 1, 1, frac(i.world.z * _DetailScale) * 2 - 1);
            variation = .82 + Hash(float3(cell, 3)) * .28;
        }
        float3 color = GrainColor(t);
        if (t >= 1 && t <= 4) color *= 1 - .32 * (1 - smoothstep(.025,.12,abs(p.x))) * smoothstep(.1,.6,p.y);
        if (t == 7) color *= 1 - .75 * (1 - smoothstep(.10,.25,length(p.xz * float2(2,1)))) * step(.5,p.y);
        if (t == 8) color = lerp(color, float3(.63,.57,.43), pow(abs(sin(p.x * 15)), 20) * .75);
        if (t == 0) color *= .91 + .09 * smoothstep(-.8,.5,p.z);
        return color * variation * _Brightness;
    }
    void TexturedGrainSurface(Varyings i, out float3 color, out float3 normal)
    {
        float3 n = normalize(i.normalWS);
        float3 blend = pow(abs(n), 4);
        blend /= max(dot(blend, 1), .0001);
        float2 cell = floor(i.world.xz * _DetailScale);
        int type = Species(Hash(float3(cell, 17)), i.world);
        float3 uv = i.world * (_DetailScale / 8.0);
        // World-space triplanar projection keeps normal detail on excavated walls.
        float4 x = SAMPLE_TEXTURE2D_ARRAY(_GrainSurfaceDetail, sampler_GrainSurfaceDetail, uv.yz, type);
        float4 y = SAMPLE_TEXTURE2D_ARRAY(_GrainSurfaceDetail, sampler_GrainSurfaceDetail, uv.xz, type);
        float4 z = SAMPLE_TEXTURE2D_ARRAY(_GrainSurfaceDetail, sampler_GrainSurfaceDetail, uv.xy, type);
        float2 nx = x.rg * 2 - 1, ny = y.rg * 2 - 1, nz = z.rg * 2 - 1;
        float3 bump = float3(0, nx.x, nx.y) * blend.x + float3(ny.x, 0, ny.y) * blend.y +
            float3(nz.x, nz.y, 0) * blend.z;
        normal = normalize(n + (bump - n * dot(n, bump)) * _SurfaceNormalStrength);
        float height = dot(float3(x.b, y.b, z.b), blend);
        color = GrainColor(type) * (.74 + .26 * height) * _Brightness;
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardOnly" Tags { "LightMode"="ForwardOnly" }
            Cull Back ZWrite Off ZTest Equal
            Stencil { WriteMask 3 Ref 2 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLit
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile_fragment SCREEN_SPACE_SHADOWS_OFF SCREEN_SPACE_SHADOWS_ON
            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH
            #pragma multi_compile_fragment USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST
            #ifndef SHADER_STAGE_FRAGMENT
                #define SHADOW_LOW
                #define USE_FPTL_LIGHTLIST
            #endif
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
            #define SHADERPASS SHADERPASS_FORWARD
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
            #define HAS_LIGHTLOOP
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
            float4 FragLit(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 positionRWS = GetCameraRelativePositionWS(i.world);
                PositionInputs pos = GetPositionInput(i.positionCS.xy, _ScreenSize.zw,
                    i.positionCS.z, i.positionCS.w, positionRWS, uint2(i.positionCS.xy) / GetTileSize());
                float3 V = GetWorldSpaceNormalizeViewDir(positionRWS);
                SurfaceData surface;
                ZERO_INITIALIZE(SurfaceData, surface);
                surface.materialFeatures = MATERIALFEATUREFLAGS_LIT_STANDARD;
                surface.baseColor = GrainAlbedo(i);
                surface.normalWS = normalize(i.normalWS);
                if (_KernelMode < .5 && _UseSurfaceDetail > .5)
                    TexturedGrainSurface(i, surface.baseColor, surface.normalWS);
                surface.geomNormalWS = normalize(i.normalWS);
                surface.perceptualSmoothness = 0.15;
                surface.ambientOcclusion = 1;
                surface.specularOcclusion = 1;
                BuiltinData builtin;
                InitBuiltinData(pos, 1, surface.normalWS, -surface.normalWS, float4(i.lightmapUV.xy,0,0), float4(i.lightmapUV.zw,0,0), builtin);
                PostInitBuiltinData(V, pos, surface, builtin);
                BSDFData bsdf = ConvertSurfaceDataToBSDFData(i.positionCS.xy, surface);
                PreLightData preLight = GetPreLightData(V, pos, bsdf);
                LightLoopOutput lighting;
                LightLoop(V, pos, preLight, bsdf, builtin, LIGHT_FEATURE_MASK_FLAGS_OPAQUE, lighting);
                return float4((lighting.diffuseLighting + lighting.specularLighting) * GetCurrentExposureMultiplier(), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthForwardOnly" Tags { "LightMode"="DepthForwardOnly" }
            Cull Back ZWrite On
            Stencil { WriteMask 8 Ref 0 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"
            float4 FragDepth(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                NormalData normal;
                normal.normalWS = normalize(i.normalWS);
                if (_KernelMode < .5 && _UseSurfaceDetail > .5)
                {
                    float3 unusedColor;
                    TexturedGrainSurface(i, unusedColor, normal.normalWS);
                }
                normal.perceptualRoughness = 0.85;
                float4 encoded;
                EncodeIntoNormalBuffer(normal, encoded);
                return encoded;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            Cull Back ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragShadow
            #pragma multi_compile_instancing
            float4 FragShadow(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
