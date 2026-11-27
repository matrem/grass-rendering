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

// Debug view (design 12.2), one global value: 0 = shading, 1 = colour per draw (_DebugColor: LOD, variant).
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
#elif defined(SPIKE_PASS_DEPTH_NORMALS)
    float3 normalWS : TEXCOORD1;
#elif defined(SPIKE_PASS_MOTION_VECTORS)
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
#endif
};

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
    if (_ScatterDebugView > 0.5)
    {
        return half4(_DebugColor.rgb * lerp(0.5, 1.0, input.heightFraction), 1.0);
    }

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    // Wrapped diffuse: thin blades also get light from behind (translucency comes in Phase 8).
    half diffuse = saturate(dot(normalWS, mainLight.direction) * 0.6 + 0.4);
    half3 lighting = mainLight.color * (diffuse * mainLight.shadowAttenuation) + SampleSH(normalWS);
    return half4(albedo * lighting, 1.0);
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
