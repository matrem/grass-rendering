#ifndef SCATTER_SPIKE_PASSES_INCLUDED
#define SCATTER_SPIKE_PASSES_INCLUDED

// The five passes of every scatter shader (design 8.1), shared by the spike shaders.
// Include after an input file (SpikeBladeInput.hlsl or SpikeObjectInput.hlsl) that defines Attributes,
// SpikeSurface, SpikeComputeSurface and SpikeAlbedo. The pass is chosen with one define per pass:
// SPIKE_PASS_FORWARD, SPIKE_PASS_SHADOW_CASTER, SPIKE_PASS_DEPTH_ONLY, SPIKE_PASS_DEPTH_NORMALS, SPIKE_PASS_MOTION_VECTORS.

#if defined(SPIKE_PASS_FORWARD)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#elif defined(SPIKE_PASS_SHADOW_CASTER)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#elif defined(SPIKE_PASS_MOTION_VECTORS)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#endif

// Debug view (design 12.2), one global value: 0 = shading, 1 = colour per draw (_DebugColor: LOD, variant),
// 2 = main light only (colour x diffuse x shadow), 3 = ambient only (SampleSH), 4 = main light shadow only,
// 5 = diffuse term only (wrapped N.L, with the side seen). Values mirrored by SpikeDebugView.
float _ScatterDebugView;

#if defined(SPIKE_PASS_SHADOW_CASTER)
// Set by URP for the shadow-casting light (ShadowUtils.SetupShadowCasterConstantBuffer).
float3 _LightDirection;
float3 _LightPosition;
#endif

struct Varyings
{
    float4 positionCS : SV_POSITION;
#if defined(SPIKE_PASS_FORWARD)
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float heightFraction : TEXCOORD2;
    half3 ambientFront : TEXCOORD3;
    half3 ambientBack : TEXCOORD4;
#elif defined(SPIKE_PASS_DEPTH_NORMALS)
    float3 normalWS : TEXCOORD1;
#elif defined(SPIKE_PASS_MOTION_VECTORS)
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
#endif
};

#if defined(SPIKE_PASS_FORWARD)
// Ambient of both sides of a blade from one SH evaluation: SampleSH(n) and SampleSH(-n) of AmbientProbe.hlsl
// (AMBIENT_PROBE_BUFFER 0 in URP) up to float rounding. The L0 and L2 bands are even in n and L1 is odd, so
// the back side is L0 - L1 + L2: the L0 and L2 terms are evaluated once.
void SpikeSampleSHBothSides(float3 normalWS, out half3 front, out half3 back)
{
    real3 l0 = real3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
    real3 l1 = real3(dot(unity_SHAr.xyz, normalWS), dot(unity_SHAg.xyz, normalWS), dot(unity_SHAb.xyz, normalWS));
    real3 l2 = SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
    real3 even = l0 + l2;
    real3 frontSH = even + l1;
    real3 backSH = even - l1;
#ifdef UNITY_COLORSPACE_GAMMA
    frontSH = LinearToSRGB(frontSH);
    backSH = LinearToSRGB(backSH);
#endif
    front = frontSH;
    back = backSH;
}
#endif

// Empty visible list entries (padding of K-blade instances) collapse to one point: no triangle is rasterised.
static const float4 SpikeCollapsedPositionCS = float4(0.0, 0.0, 0.0, 1.0);

Varyings SpikeVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    SpikeSurface surface = SpikeComputeSurface(input);
    if (!surface.valid)
    {
        output.positionCS = SpikeCollapsedPositionCS;
        return output;
    }

#if defined(SPIKE_PASS_SHADOW_CASTER)
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirectionWS = normalize(_LightPosition - surface.positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(surface.positionWS, surface.normalWS, lightDirectionWS)));
#else
    output.positionCS = TransformWorldToHClip(surface.positionWS);
#endif

#if defined(SPIKE_PASS_FORWARD)
    output.positionWS = surface.positionWS;
    output.normalWS = surface.normalWS;
    output.heightFraction = surface.heightFraction;
    // Ambient per vertex, in the stage where the instance setup ran: with BRG, SampleSH reads the SH values
    // that UNITY_SETUP_INSTANCE_ID samples (unity_DOTS_Sampled_SH*), which are 0 in a stage without the setup.
    // Blades are double-sided: both sides, chosen per pixel.
    SpikeSampleSHBothSides(surface.normalWS, output.ambientFront, output.ambientBack);
#elif defined(SPIKE_PASS_DEPTH_NORMALS)
    output.normalWS = surface.normalWS;
#elif defined(SPIKE_PASS_MOTION_VECTORS)
    // Static content in the spike: the previous position is the current one (wind comes in Phase 6).
    float4 positionWS = float4(surface.positionWS, 1.0);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, positionWS);
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, positionWS);
#endif
    return output;
}

#if defined(SPIKE_PASS_FORWARD)
half4 SpikeFragment(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
    half3 albedo = SpikeAlbedo(input.heightFraction);
    if (_ScatterDebugView > 0.5 && _ScatterDebugView < 1.5)
    {
        return half4(_DebugColor.rgb * lerp(0.5, 1.0, input.heightFraction), 1.0);
    }

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    // Wrapped diffuse: thin blades also get light from behind (translucency comes in Phase 8).
    half diffuse = saturate(dot(normalWS, mainLight.direction) * 0.6 + 0.4);
    half3 direct = mainLight.color * (diffuse * mainLight.distanceAttenuation * mainLight.shadowAttenuation);
    half3 ambient = isFrontFace ? input.ambientFront : input.ambientBack;
    if (_ScatterDebugView > 1.5)
    {
        half3 view = _ScatterDebugView < 2.5 ? direct
            : _ScatterDebugView < 3.5 ? ambient
            : _ScatterDebugView < 4.5 ? mainLight.shadowAttenuation.xxx
            : diffuse.xxx;
        return half4(view, 1.0);
    }

    return half4(albedo * (direct + ambient), 1.0);
}
#elif defined(SPIKE_PASS_DEPTH_ONLY)
half SpikeFragment(Varyings input) : SV_Target
{
    return input.positionCS.z;
}
#elif defined(SPIKE_PASS_DEPTH_NORMALS)
half4 SpikeFragment(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
}
#elif defined(SPIKE_PASS_MOTION_VECTORS)
float4 SpikeFragment(Varyings input) : SV_Target
{
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}
#else
half4 SpikeFragment(Varyings input) : SV_Target
{
    return 0;
}
#endif

#endif
