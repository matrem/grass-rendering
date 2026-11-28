#ifndef SCATTER_SPIKE_OBJECT_INPUT_INCLUDED
#define SCATTER_SPIKE_OBJECT_INPUT_INCLUDED

// Spike Object layer (small rocks): a real mesh per instance, flattened, scaled and turned per instance.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SpikeCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TipColor;           // colour at the top of the object
    float4 _DebugColor;
    float4 _ObjectShape;        // xyz = mesh scale per axis (meters), w = random scale range (fraction)
    float _BladesPerInstance;   // always 1 for objects; read by SpikeVisibleEntry.hlsl
    float _RegionStart;         // first visible list entry of the region (RenderMeshIndirect path)
CBUFFER_END

// Instance data: xyz = world position, w = yaw (radians).
StructuredBuffer<float4> _SpikeInstances;

#include "SpikeVisibleEntry.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    SPIKE_VERTEX_INPUT_INSTANCE_ID
};

struct SpikeSurface
{
    float3 positionWS;
    float3 normalWS;
    float heightFraction;
    bool valid;
};

SpikeSurface SpikeComputeSurface(Attributes input)
{
    SpikeSurface surface = (SpikeSurface)0;
    SPIKE_SETUP_INSTANCE_ID(input);
    uint entry = SpikeLoadVisibleEntry(SPIKE_INSTANCE_ID(input), 0u);
    if (entry == SPIKE_EMPTY_ENTRY)
    {
        return surface;
    }

    float4 instance = _SpikeInstances[entry];
    float scale = 1.0 + _ObjectShape.w * (SpikeHashToUnit(SpikeHash(entry)) * 2.0 - 1.0);
    float3 axisScale = _ObjectShape.xyz * scale;
    float sinYaw;
    float cosYaw;
    sincos(instance.w, sinYaw, cosYaw);

    float3 local = input.positionOS * axisScale;
    float3 rotated = float3(local.x * cosYaw + local.z * sinYaw, local.y, -local.x * sinYaw + local.z * cosYaw);
    float3 normalLocal = input.normalOS / axisScale;
    float3 normalRotated = float3(normalLocal.x * cosYaw + normalLocal.z * sinYaw, normalLocal.y, -normalLocal.x * sinYaw + normalLocal.z * cosYaw);

    // Half buried: the mesh centre sits a quarter of its height above the ground.
    surface.positionWS = instance.xyz + rotated + float3(0.0, 0.25 * axisScale.y, 0.0);
    surface.normalWS = normalize(normalRotated);
    surface.heightFraction = saturate(input.positionOS.y + 0.5);
    surface.valid = true;
    return surface;
}

half3 SpikeAlbedo(float heightFraction)
{
    return lerp(_BaseColor.rgb, _TipColor.rgb, heightFraction);
}

#endif
