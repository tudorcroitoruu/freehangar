Shader "Hidden/HDRP/Sky/PolyverseStyleSky"
{
    Properties
    {
        _BackgroundCubemap("Background Cubemap", Cube) = "black" {}
        _PatternCubemap("Pattern Cubemap", Cube) = "black" {}
        _StarsCubemap("Stars Cubemap", Cube) = "black" {}
        _CloudsCubemap("Clouds Cubemap", Cube) = "black" {}
        _SunTexture("Sun Texture", 2D) = "black" {}
        _MoonTexture("Moon Texture", 2D) = "black" {}
    }

    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"

    TEXTURECUBE(_BackgroundCubemap);
    SAMPLER(sampler_BackgroundCubemap);

    TEXTURECUBE(_PatternCubemap);
    SAMPLER(sampler_PatternCubemap);

    TEXTURECUBE(_StarsCubemap);
    SAMPLER(sampler_StarsCubemap);

    TEXTURECUBE(_CloudsCubemap);
    SAMPLER(sampler_CloudsCubemap);

    TEXTURE2D(_SunTexture);
    SAMPLER(sampler_SunTexture);

    TEXTURE2D(_MoonTexture);
    SAMPLER(sampler_MoonTexture);

    float _SkyIntensity;
    float _SkyTime;
    float _DebugSolidColor;

    int _BackgroundMode;
    float _HasBackgroundCubemap;
    float4 _SkyColor;
    float4 _EquatorColor;
    float4 _GroundColor;
    float _EquatorHeight;
    float _EquatorSmoothness;
    float _BackgroundExposure;

    int _EnablePatternOverlay;
    float _HasPatternCubemap;
    float _PatternContrast;

    int _EnableStars;
    float _HasStarsCubemap;
    float _StarsIntensity;
    float _StarsVisibility;
    float _StarsIgnoreExposure;
    int _StarsLayers;
    float4 _StarsColor;
    float _StarsSize;
    float _StarsSunMask;
    float _StarsHeightMask;
    int _StarsBottomMask;
    int _EnableStarsTwinkling;
    float _TwinklingContrast;
    float _TwinklingSpeed;
    int _EnableStarsRotation;
    float _StarsRotation;
    float3 _StarsRotationAxis;
    float _StarsRotationSpeed;

    int _EnableSun;
    float _HasSunTexture;
    float4 _SunColor;
    float _SunSize;
    float _SunIntensity;
    float3 _SunDirection;

    int _EnableMoon;
    float _HasMoonTexture;
    float4 _MoonColor;
    float _MoonSize;
    float _MoonIntensity;
    float _MoonPhase;
    float _MoonDarkSideVisibility;
    float _MoonPhaseRoundness;
    float _MoonPhaseFade;
    float3 _MoonDirection;

    int _EnableClouds;
    float _HasCloudsCubemap;
    float _CloudsOpacity;
    float _CloudsFade;
    float _CloudsHeight;
    float2 _CloudsOffset;
    float4 _CloudsShape; // coverage, scale, edge softness, detail
    float4 _CloudsAnimation; // wind x/z, evolution speed, deformation strength
    float _CloudsHorizonFade;
    float4 _CloudsLayer; // altitude, fade start/end in metres, distant size
    float _CloudsMaxHeight; // maximum elevation above the world horizon, in degrees
    float _CloudsBreakup;
    float4 _CloudsLightColor;
    float4 _CloudsShadowColor;
    float3 _CloudsSunDirection;
    float3 _CloudsSunTint;
    float4 _CloudsLighting; // direct strength, ambient fill, transition softness, silver lining
    int _EnableCloudsRotation;
    float _CloudsRotation;
    float3 _CloudsRotationAxis;
    float _CloudsRotationSpeed;

    int _EnableHorizonFog;
    float4 _HorizonFogColor;
    float _FogHeight;
    float _FogSmoothness;
    float _FogFill;

    float _SkyboxOffset;
    float _SkyboxRotation;
    float3 _SkyboxRotationAxis;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(
            input.vertexID,
            UNITY_RAW_FAR_CLIP_VALUE);
        return output;
    }

    float3 SafeNormalize(float3 value, float3 fallback)
    {
        float lengthSquared = dot(value, value);
        return lengthSquared > 1e-8 ? value * rsqrt(lengthSquared) : fallback;
    }

    float3 RotateAroundAxis(float3 value, float3 axis, float angleRadians)
    {
        axis = SafeNormalize(axis, float3(0.0, 1.0, 0.0));
        float sine;
        float cosine;
        sincos(angleRadians, sine, cosine);

        return value * cosine
             + cross(axis, value) * sine
             + axis * dot(axis, value) * (1.0 - cosine);
    }

    float3 SoftLight(float3 baseColor, float3 blendColor)
    {
        float3 low = 2.0 * baseColor * blendColor;
        float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
        return saturate(lerp(low, high, step(0.5, baseColor)));
    }

    float Hash21(float2 p)
    {
        return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
    }

    float Hash31(float3 p)
    {
        return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
    }

    float CloudVolumeNoise(float3 position)
    {
        float3 cell = floor(position);
        float3 fraction = frac(position);
        float3 blend = fraction * fraction * (3.0 - 2.0 * fraction);
        float lower = lerp(lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), blend.x),
            lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), blend.x), blend.y);
        float upper = lerp(lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), blend.x),
            lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + float3(1, 1, 1)), blend.x), blend.y);
        return lerp(lower, upper, blend.z);
    }

    float4 CloudEllipsoid(float3 position, float3 center, float3 radii)
    {
        float3 scaled = (position - center) / radii;
        float distance = max(length(scaled), 0.0001);
        return float4(-scaled / (radii * distance), 1.0 - distance);
    }

    float4 CloudMergeSurface(float4 a, float4 b)
    {
        const float smoothing = 0.24;
        float blend = saturate(0.5 + 0.5 * (a.w - b.w) / smoothing);
        return float4(lerp(b.xyz, a.xyz, blend),
            lerp(b.w, a.w, blend) + smoothing * blend * (1.0 - blend));
    }

    float CloudVolumeDensity(float3 position, float random, float life, float evolution, out float3 normal)
    {
        float deformation = sin(evolution * 0.7 + random * TWO_PI) * _CloudsAnimation.w * 0.035;
        float variation = (random - 0.5) * _CloudsShape.w * 0.08;
        float4 surface = CloudEllipsoid(position, float3(0, -0.035, 0), float3(0.29, 0.085, 0.19));
        surface = CloudMergeSurface(surface, CloudEllipsoid(position, float3(-0.17, 0.015, 0), float3(0.15, 0.13, 0.15)));
        surface = CloudMergeSurface(surface, CloudEllipsoid(position, float3(-0.035 + deformation, 0.07, 0.015), float3(0.18, 0.19 + variation, 0.17)));
        surface = CloudMergeSurface(surface, CloudEllipsoid(position, float3(0.15, 0.025 + deformation, 0.01), float3(0.16, 0.15 - variation, 0.15)));
        surface = CloudMergeSurface(surface, CloudEllipsoid(position, float3(0.28, -0.02, -0.01), float3(0.11, 0.095, 0.12)));
        float body = surface.w;
        normal = SafeNormalize(-surface.xyz, float3(0, 1, 0));
        float softness = max(0.025, _CloudsShape.z);
        // Empty samples and fully formed clouds do not need the erosion noise evaluations.
        if (body <= -softness) return 0.0;
        float erosion = 0.0;
        if (life < 1.0)
        {
            float3 noisePosition = position * 15.0 + random * 37.1 + float3(0, evolution * 0.15, 0);
            float breakupNoise = CloudVolumeNoise(noisePosition) * 0.7
                + CloudVolumeNoise(noisePosition * 2.07 + 13.7) * 0.3;
            // Life erodes density spatially. It does not uniformly lower the cloud's alpha.
            erosion = (1.0 - life) * (1.8 + (breakupNoise - 0.5) * _CloudsBreakup * 1.2);
        }
        return smoothstep(-softness, softness, body - erosion);
    }

    // Stratified ranks guarantee a similar cloud count in every 4x4 neighbourhood.
    // Scramble each block so the spacing does not read as a repeating checkerboard.
    float CloudDistributionRank(float2 cell)
    {
        float2 block = floor(cell / 4.0);
        float2 offset = floor(float2(Hash21(block + 18.2), Hash21(block + 64.8)) * 4.0);
        float2 local = frac((cell + offset) / 4.0) * 4.0;
        float2 low = local - floor(local / 2.0) * 2.0;
        float2 high = floor(local / 2.0);
        float fineRank = 2.0 * low.x + 3.0 * low.y - 4.0 * low.x * low.y;
        float coarseRank = 2.0 * high.x + 3.0 * high.y - 4.0 * high.x * high.y;
        return (fineRank * 4.0 + coarseRank + 0.5) / 16.0;
    }

    float2 CloudVolume(float3 direction, float3 sunDirection, float2 cell, float cellSize, float evolution)
    {
        float2 result = float2(0, 0.7);
        float random = Hash21(cell + 7.31);
        float rank = CloudDistributionRank(cell);
        float presence = smoothstep(rank - 0.03, rank + 0.03, _CloudsShape.x);
        float2 jitter = float2(Hash21(cell + 31.7), Hash21(cell + 83.1)) - 0.5;
        float2 travel = (_CloudsOffset + _SkyTime * _CloudsAnimation.xy) * _CloudsLayer.x;
        float3 center = float3((cell.x + 0.5 + jitter.x * 0.32) * cellSize + travel.x,
            _CloudsLayer.x + (random - 0.5) * cellSize * 0.1,
            (cell.y + 0.5 + jitter.y * 0.32) * cellSize + travel.y);
        float distance = length(center.xz);
        float distanceAmount = smoothstep(_CloudsLayer.y, _CloudsLayer.z, distance);
        float distanceFade = 1.0 - distanceAmount;
        float size = cellSize * lerp(0.88, 1.02, Hash21(cell + 41.5))
            * lerp(1.0, _CloudsLayer.w, distanceAmount);
        // Spread lifetimes across the occupied sites: neighbouring clouds do not all die together.
        float phase = frac(evolution * 0.28 + rank / max(_CloudsShape.x, 0.0625)
            + Hash21(floor(cell / 4.0) + 51.9));
        float life = smoothstep(0.0, 0.18, phase) * (1.0 - smoothstep(0.78, 1.0, phase));
        // Coverage selects/form-erodes clouds instead of leaving whole translucent silhouettes.
        // Only the shared opacity/fade controls and intentional distance fade scale their alpha.
        life *= presence;
        if (presence > 0.001 && distanceFade > 0.0001 && life > 0.001)
        {
            // A seeded world-Y orientation varies the 3D shape, not the direction of travel.
            float angle = Hash21(cell + 9.17) * TWO_PI;
            float3 origin = RotateAroundAxis(-center / size, float3(0, 1, 0), angle);
            float3 ray = RotateAroundAxis(direction, float3(0, 1, 0), angle);
            float3 lightDirection = RotateAroundAxis(sunDirection, float3(0, 1, 0), angle);
            float3 bounds = float3(0.46, 0.31, 0.31);
            float3 scaledOrigin = origin / bounds;
            float3 scaledRay = ray / bounds;
            float a = dot(scaledRay, scaledRay);
            float b = dot(scaledOrigin, scaledRay);
            float c = dot(scaledOrigin, scaledOrigin) - 1.0;
            float discriminant = b * b - a * c;
            if (discriminant > 0.0)
            {
                float root = sqrt(discriminant);
                float nearDistance = max(0.0, (-b - root) / a);
                float farDistance = (-b + root) / a;
                if (farDistance > nearDistance)
                {
                    const int steps = 16;
                    float stepLength = (farDistance - nearDistance) / steps;
                    float transmittance = 1.0;
                    float litDensity = 0.0;
                    float opacity = 0.0;
                    [loop]
                    for (int sampleIndex = 0; sampleIndex < steps; sampleIndex++)
                    {
                        float t = nearDistance + (sampleIndex + 0.5) * stepLength;
                        float3 samplePosition = origin + ray * t;
                        float3 normal;
                        float density = CloudVolumeDensity(samplePosition, random, life, evolution, normal);
                        float sampleOpacity = 1.0 - exp(-density * stepLength * 32.0);
                        float weight = transmittance * sampleOpacity;
                        // The gradient of the merged volume lights each lobe without internal seams.
                        float wrappedDiffuse = dot(normal, lightDirection) * 0.5 + 0.5;
                        float halfSoftness = max(0.01, _CloudsLighting.z * 0.5);
                        float diffuse = smoothstep(0.5 - halfSoftness, 0.5 + halfSoftness, wrappedDiffuse);
                        float rim = pow(1.0 - saturate(dot(normal, -ray)), 3.0)
                            * pow(saturate(dot(ray, lightDirection)), 4.0) * _CloudsLighting.w;
                        float lighting = saturate(_CloudsLighting.y
                            + (1.0 - _CloudsLighting.y) * diffuse * _CloudsLighting.x + rim);
                        litDensity += lighting * weight;
                        opacity += weight;
                        transmittance *= 1.0 - sampleOpacity;
                    }
                    result = float2(opacity * distanceFade,
                        litDensity / max(0.0001, opacity));
                }
            }
        }
        return result;
    }
    float3 GetOrientedDirection(float3 direction)
    {
        direction = SafeNormalize(
            direction + float3(0.0, -_SkyboxOffset, 0.0),
            float3(0.0, 1.0, 0.0));

        return SafeNormalize(
            RotateAroundAxis(
                direction,
                _SkyboxRotationAxis,
                radians(_SkyboxRotation)),
            direction);
    }

    float3 GetGradientSky(float3 direction)
    {
        float3 hemisphereColor = lerp(
            _GroundColor.rgb,
            _SkyColor.rgb,
            step(0.0, direction.y));

        float equatorAmount = saturate(
            abs(direction.y) / max(_EquatorHeight, 0.0001));

        equatorAmount = pow(
            equatorAmount,
            max(0.0001, 1.0 - _EquatorSmoothness));

        return lerp(_EquatorColor.rgb, hemisphereColor, equatorAmount);
    }

    float GetTwinkle(float3 direction)
    {
        float firstWave = sin(
            (direction.x + direction.y) * 20.0
            + _SkyTime * _TwinklingSpeed * 10.0);

        float secondWave = sin(
            (direction.x + direction.z) * 30.0
            + _SkyTime * _TwinklingSpeed * 5.0);

        float twinkle = abs(firstWave * secondWave + 0.5);
        return saturate(pow(twinkle, max(_TwinklingContrast, 0.0001)));
    }

    float GetStarsMask(float3 skyDirection)
    {
        float3 starsDirection = skyDirection;

        if (_EnableStarsRotation != 0)
        {
            float rotation = radians(
                _StarsRotation + _SkyTime * _StarsRotationSpeed);

            starsDirection = RotateAroundAxis(
                starsDirection,
                _StarsRotationAxis,
                -rotation);
        }

        float starMask = 0.0;

        if (_HasStarsCubemap > 0.5)
        {
            float4 starsSample = SAMPLE_TEXTURECUBE_LOD(
                _StarsCubemap,
                sampler_StarsCubemap,
                starsDirection,
                0.0);

            float starSignal = starsSample.r;

            if (_StarsLayers >= 2)
                starSignal = max(starSignal, starsSample.g);

            if (_StarsLayers >= 3)
                starSignal = max(starSignal, starsSample.b);

            float threshold = lerp(0.995, 0.05, saturate(_StarsSize));
            starMask = smoothstep(
                threshold,
                min(1.0, threshold + 0.01),
                starSignal);
        }
        else
        {
            float3 p = starsDirection * 170.0;
            float3 cell = floor(p);
            float3 local = frac(p) - 0.5;
            float random = Hash31(cell);
            float3 starPosition = float3(
                Hash31(cell + 13.1),
                Hash31(cell + 27.7),
                Hash31(cell + 41.3)) - 0.5;
            float distanceToStar = length(local - starPosition);
            float starRadius = lerp(0.025, 0.11, saturate(_StarsSize));
            float starCore = smoothstep(starRadius, 0.0, distanceToStar);
            float starGlow = smoothstep(starRadius * 2.2, 0.0, distanceToStar) * 0.25;

            starMask = step(0.982, random)
                * saturate(starCore + starGlow)
                * lerp(0.35, 1.0, Hash31(cell + 91.13));
        }

        if (_EnableStarsTwinkling != 0)
            starMask *= GetTwinkle(starsDirection);

        if (_StarsSunMask > 0.0001)
        {
            float3 sunDirection = SafeNormalize(
                _SunDirection,
                float3(0.0, 1.0, 0.0));

            float sunProximity = saturate(dot(skyDirection, sunDirection));
            float sunVisibility = 1.0 - smoothstep(
                1.0 - _StarsSunMask,
                1.0,
                sunProximity);

            starMask *= sunVisibility;
        }

        if (_StarsHeightMask > 0.0001)
        {
            starMask *= smoothstep(
                0.0,
                _StarsHeightMask,
                abs(skyDirection.y));
        }

        if (_StarsBottomMask != 0)
            starMask *= step(0.0, skyDirection.y);

        return saturate(starMask * _StarsVisibility);
    }

    float GetMoonPhaseMask(float2 moonLocal)
    {
        float phaseAngle = frac(0.5 - _MoonPhase) * TWO_PI;
        float litAmount = 0.5 - 0.5 * cos(phaseAngle);
        float terminatorOffset = 1.0 - 2.0 * litAmount;
        float visibleSide = sin(phaseAngle) < 0.0 ? -1.0 : 1.0;

        float x = moonLocal.x * visibleSide;
        float yArc = sqrt(saturate(1.0 - moonLocal.y * moonLocal.y));
        float arcStrength = saturate((_MoonPhaseRoundness - 0.5) / 1.1);
        float extraRoundness = max(_MoonPhaseRoundness - 1.6, 0.0);
        float roundedArc = pow(max(yArc, 0.0001), 1.0 + extraRoundness);
        float terminatorShape = lerp(1.0, roundedArc, arcStrength);
        float terminator = terminatorOffset * terminatorShape;

        float fade = saturate(_MoonPhaseFade);

        if (fade <= 0.0001)
            return step(terminator, x);

        float practicalFade = smoothstep(0.0, 0.5, fade);
        float broadFade = smoothstep(0.5, 1.0, fade);
        float fadeWidth = lerp(0.02, 1.0, practicalFade);
        fadeWidth = lerp(fadeWidth, 1.6, broadFade);

        return smoothstep(terminator - fadeWidth, terminator + fadeWidth, x);
    }

    void GetCelestialCoordinates(
        float3 skyDirection,
        float3 bodyDirection,
        float bodySize,
        out float2 uv,
        out float facing,
        out float inside)
    {
        bodyDirection = SafeNormalize(bodyDirection, float3(0.0, 1.0, 0.0));

        float3 referenceUp = abs(bodyDirection.y) > 0.99
            ? float3(1.0, 0.0, 0.0)
            : float3(0.0, 1.0, 0.0);

        float3 right = SafeNormalize(
            cross(referenceUp, bodyDirection),
            float3(1.0, 0.0, 0.0));

        float3 up = SafeNormalize(
            cross(bodyDirection, right),
            float3(0.0, 1.0, 0.0));

        float scale = lerp(20.0, 2.0, saturate(bodySize));

        uv = float2(
            dot(skyDirection, right),
            dot(skyDirection, up));

        uv = uv * scale * 0.5 + 0.5;
        facing = saturate(dot(skyDirection, bodyDirection));

        inside = step(0.0, uv.x)
               * step(0.0, uv.y)
               * step(uv.x, 1.0)
               * step(uv.y, 1.0);
    }

    float3 CompositeSun(float3 skyColor, float3 skyDirection)
    {
        float2 uv;
        float facing;
        float inside;

        GetCelestialCoordinates(
            skyDirection,
            _SunDirection,
            _SunSize,
            uv,
            facing,
            inside);

        float4 sunSample = SAMPLE_TEXTURE2D_LOD(
            _SunTexture,
            sampler_SunTexture,
            uv,
            0.0);
        float2 local = uv * 2.0 - 1.0;
        float disk = smoothstep(1.0, 0.86, length(local));
        float textureBrightness = lerp(1.0, sunSample.r, _HasSunTexture);
        float textureAlpha = lerp(1.0, sunSample.a, _HasSunTexture);

        float3 sunColor = textureBrightness
            * _SunColor.rgb
            * max(_SunIntensity, 1.0);

        float sunMask = textureAlpha
            * disk
            * saturate(_SunIntensity)
            * facing
            * inside;

        return lerp(skyColor, sunColor, saturate(sunMask));
    }

    float3 CompositeMoon(float3 skyColor, float3 skyDirection)
    {
        float2 uv;
        float facing;
        float inside;

        GetCelestialCoordinates(
            skyDirection,
            _MoonDirection,
            _MoonSize,
            uv,
            facing,
            inside);

        float4 moonSample = SAMPLE_TEXTURE2D_LOD(
            _MoonTexture,
            sampler_MoonTexture,
            uv,
            0.0);
        float2 local = uv * 2.0 - 1.0;
        float disk = smoothstep(1.0, 0.9, length(local));
        float phaseMask = GetMoonPhaseMask(local);
        float darkSideVisibility = saturate(_MoonDarkSideVisibility);
        float phaseVisibility = lerp(darkSideVisibility, 1.0, phaseMask);
        float textureBrightness = lerp(1.0, moonSample.r, _HasMoonTexture);
        float textureAlpha = lerp(1.0, moonSample.a, _HasMoonTexture);

        float3 moonColor = textureBrightness
            * _MoonColor.rgb
            * max(_MoonIntensity, 1.0)
            * phaseVisibility;

        float moonMask = textureAlpha
            * disk
            * phaseVisibility
            * saturate(_MoonIntensity)
            * facing
            * inside;

        return lerp(skyColor, moonColor, saturate(moonMask));
    }

    float3 CompositeClouds(float3 skyColor, float3 skyDirection)
    {
        // World-space elevation keeps the limit stable while the cloud layer rotates.
        float elevation = degrees(asin(saturate(skyDirection.y)));
        float heightFade = _CloudsMaxHeight >= 90.0 ? 1.0
            : 1.0 - smoothstep(max(0.0, _CloudsMaxHeight - 5.0), max(0.001, _CloudsMaxHeight), elevation);
        if (_CloudsMaxHeight <= 0.0 || heightFade <= 0.0) return skyColor;
        float3 cloudDirection = skyDirection;
        float3 cloudSunDirection = SafeNormalize(_CloudsSunDirection, float3(0, 1, 0));

        if (_EnableCloudsRotation != 0)
        {
            float rotation = radians(
                _CloudsRotation + _SkyTime * _CloudsRotationSpeed);

            cloudDirection = RotateAroundAxis(
                cloudDirection,
                _CloudsRotationAxis,
                -rotation);
            cloudSunDirection = RotateAroundAxis(cloudSunDirection, _CloudsRotationAxis, -rotation);
        }

        cloudDirection.y -= _CloudsHeight;

        float cloudShape = 0.0;
        float3 cloudColor = _CloudsLightColor.rgb;
        // Keep one initialized output for both branches, including HDRP's cubemap baking pass.
        if (_HasCloudsCubemap > 0.5)
        {
            float3 movedCloudDirection = SafeNormalize(
                cloudDirection + float3(_CloudsOffset.x, 0, _CloudsOffset.y), cloudDirection);
            float4 cloudsSample = SAMPLE_TEXTURECUBE_LOD(
                _CloudsCubemap,
                sampler_CloudsCubemap,
                movedCloudDirection,
                0.0);
            cloudColor = lerp(_CloudsShadowColor.rgb, _CloudsLightColor.rgb, cloudsSample.rgb);
            cloudShape = cloudsSample.a;
        }
        else
        {
            float3 direction = SafeNormalize(cloudDirection, float3(0, 1, 0));
            float evolution = _SkyTime * _CloudsAnimation.z;
            float strongest = 0.0;
            float lighting = 0.7;
            if (direction.y > 0.04 && _CloudsShape.x > 0.0)
            {
                // Ray/layer intersection is finite and isotropic at the zenith: there is no polar grid.
                float2 planePosition = direction.xz * (_CloudsLayer.x / direction.y);
                float cellSize = _CloudsLayer.x * 1.5 / _CloudsShape.y;
                float2 travel = (_CloudsOffset + _SkyTime * _CloudsAnimation.xy) * _CloudsLayer.x;
                float2 cell = floor((planePosition - travel) / cellSize);
                // Include the full volume support at grazing angles, not only the plane hit cell.
                int reach = min(4, max(1, (int)ceil(0.45 + 0.4 * length(direction.xz) / direction.y)));
                [loop]
                for (int z = -reach; z <= reach; z++)
                {
                    [loop]
                    for (int x = -reach; x <= reach; x++)
                    {
                        float2 cluster = CloudVolume(direction, cloudSunDirection, cell + float2(x, z), cellSize, evolution);
                        lighting = lerp(lighting, cluster.y, step(strongest + 0.000001, cluster.x));
                        strongest = max(strongest, cluster.x);
                    }
                }
            }
            float horizon = smoothstep(0.0, _CloudsHorizonFade, skyDirection.y);
            cloudShape = lerp(strongest, 1.0, smoothstep(0.85, 1.0, _CloudsShape.x)) * horizon;
            cloudShape *= step(0.0001, _CloudsShape.x);
            cloudColor = lerp(_CloudsShadowColor.rgb, _CloudsLightColor.rgb * _CloudsSunTint, lighting);
        }
        float cloudsMask = saturate(cloudShape * _CloudsOpacity * _CloudsFade * heightFade);
        return lerp(skyColor, cloudColor, cloudsMask);
    }

    float3 CompositeHorizonFog(float3 skyColor, float3 skyDirection)
    {
        float height = max(_FogHeight, 0.0001);
        float vertical = abs(skyDirection.y);

        float keepSky = saturate(vertical / height);
        keepSky = pow(keepSky, max(0.0001, 1.0 - _FogSmoothness));
        keepSky = lerp(keepSky, 0.0, saturate(_FogFill));

        return lerp(_HorizonFogColor.rgb, skyColor, keepSky);
    }

    float3 EvaluateSky(float3 rawDirection, float exposure)
    {
        float3 direction = GetOrientedDirection(rawDirection);
        float3 colorGradient = GetGradientSky(direction);

        float3 cubemapColor = colorGradient;

        if (_HasBackgroundCubemap > 0.5)
        {
            cubemapColor = SAMPLE_TEXTURECUBE_LOD(
                _BackgroundCubemap,
                sampler_BackgroundCubemap,
                direction,
                0.0).rgb * _BackgroundExposure;
        }

        float3 skyColor = colorGradient;

        if (_BackgroundMode == 1)
            skyColor = cubemapColor;
        else if (_BackgroundMode == 2 && _HasBackgroundCubemap > 0.5)
            skyColor = colorGradient * cubemapColor;

        if (_EnablePatternOverlay != 0 && _HasPatternCubemap > 0.5)
        {
            float pattern = SAMPLE_TEXTURECUBE_LOD(
                _PatternCubemap,
                sampler_PatternCubemap,
                direction,
                0.0).r;

            pattern = lerp(0.5, pattern, _PatternContrast);
            skyColor = SoftLight(skyColor, pattern.xxx);
        }

        if (_EnableStars != 0)
        {
            float starsMask = GetStarsMask(direction);
            float exposureCompensation = lerp(
                1.0,
                rcp(max(exposure, 0.0001)),
                saturate(_StarsIgnoreExposure));
            float3 starsColor = _StarsColor.rgb * _StarsIntensity * exposureCompensation;
            skyColor = lerp(skyColor, starsColor, starsMask);
        }

        if (_EnableSun != 0)
            skyColor = CompositeSun(skyColor, direction);

        if (_EnableMoon != 0)
            skyColor = CompositeMoon(skyColor, direction);

        if (_EnableClouds != 0)
            skyColor = CompositeClouds(skyColor, direction);

        if (_EnableHorizonFog != 0)
            skyColor = CompositeHorizonFog(skyColor, direction);

        return skyColor;
    }

    float4 RenderSky(Varyings input, float exposure)
    {
        float3 viewDirectionWS = GetSkyViewDirWS(input.positionCS.xy);
        // HDRP's sky view direction uses the opposite vertical sign from the
        // original skybox convention this shader ports.
        float3 directionIntoScene = float3(
            viewDirectionWS.x,
            -viewDirectionWS.y,
            viewDirectionWS.z);

        if (_DebugSolidColor > 0.5)
        {
            float3 debugColor = _SkyColor.rgb * _SkyIntensity * exposure;
            return float4(ClampToFloat16Max(debugColor), 1.0);
        }

        float3 skyColor = EvaluateSky(directionIntoScene, exposure);
        skyColor *= _SkyIntensity * exposure;
        skyColor = ClampToFloat16Max(skyColor);

        return float4(skyColor, 1.0);
    }

    float4 FragBaking(Varyings input) : SV_Target
    {
        return RenderSky(input, 1.0);
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return RenderSky(input, GetCurrentExposureMultiplier());
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "FragBaking"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragBaking
            ENDHLSL
        }

        Pass
        {
            Name "FragRender"
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FragRender
            ENDHLSL
        }
    }

    Fallback Off
}
