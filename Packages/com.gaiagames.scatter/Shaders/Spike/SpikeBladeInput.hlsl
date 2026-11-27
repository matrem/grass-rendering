#ifndef SCATTER_SPIKE_BLADE_INPUT_INCLUDED
#define SCATTER_SPIKE_BLADE_INPUT_INCLUDED

// Spike blade: one tapered, pre-curved blade per instance data entry, built from a template vertex
// (side in [-1, 1], height fraction t in [0, 1]). Mesh draws read the template from the mesh;
// procedural draws (SPIKE_BLADE_PROCEDURAL) rebuild it from SV_VertexID. Same layout as BladeMeshFactory.cs.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SpikeCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TipColor;
    float4 _DebugColor;
    float _BladeWidth;          // meters, at the root
    float _BladeHeight;         // meters
    float _BladeBend;           // tip offset along the facing direction, fraction of the height
    float _SegmentCount;        // segments of this mesh LOD: 3 = 7 vertices, 2 = 5, 1 = 3
    float _BladesPerInstance;   // K
    float _Indexed;             // procedural only: 1 when SV_VertexID comes from the K-blade index buffer
    float _RegionStart;         // first visible list entry of the region (RenderMeshIndirect path)
CBUFFER_END

// Instance data: xyz = world position, w = yaw (radians).
StructuredBuffer<float4> _SpikeInstances;

#include "SpikeVisibleEntry.hlsl"

struct Attributes
{
#if !defined(SPIKE_BLADE_PROCEDURAL)
    float3 positionOS : POSITION; // template: x = side, y = height fraction
#endif
    uint vertexID : SV_VertexID;
    SPIKE_VERTEX_INPUT_INSTANCE_ID
};

struct SpikeSurface
{
    float3 positionWS;
    float3 normalWS;
    float heightFraction;
    bool valid;
};

// Template vertex v of a blade with the given segment count: pairs of vertices from root to tip, then the tip.
float2 SpikeBladeTemplateVertex(uint v, uint segmentCount)
{
    if (v >= 2u * segmentCount)
    {
        return float2(0.0, 1.0);
    }

    float side = (v & 1u) != 0u ? 1.0 : -1.0;
    return float2(side, (float)(v >> 1u) / (float)segmentCount);
}

// Non-indexed procedural draws: 3 vertices per triangle of the strip, odd triangles swapped to keep the winding.
uint SpikeTriangleListVertex(uint vertexID)
{
    uint triangleIndex = vertexID / 3u;
    uint corner = vertexID - triangleIndex * 3u;
    if ((triangleIndex & 1u) != 0u && corner < 2u)
    {
        corner = 1u - corner;
    }

    return triangleIndex + corner;
}

SpikeSurface SpikeComputeSurface(Attributes input)
{
    SpikeSurface surface = (SpikeSurface)0;
    SPIKE_SETUP_INSTANCE_ID(input);
    uint instanceID = SPIKE_INSTANCE_ID(input);
    uint segmentCount = (uint)_SegmentCount;

#if defined(SPIKE_BLADE_PROCEDURAL)
    uint bladeInInstance = 0u;
    uint templateIndex;
    if (_Indexed > 0.5)
    {
        uint verticesPerBlade = 2u * segmentCount + 1u;
        bladeInInstance = input.vertexID / verticesPerBlade;
        templateIndex = input.vertexID - bladeInInstance * verticesPerBlade;
    }
    else
    {
        templateIndex = SpikeTriangleListVertex(input.vertexID);
    }

    float2 templateVertex = SpikeBladeTemplateVertex(templateIndex, segmentCount);
#else
    uint bladeInInstance = 0u;
    float2 templateVertex = input.positionOS.xy;
#endif

    uint entry = SpikeLoadVisibleEntry(instanceID, bladeInInstance);
    if (entry == SPIKE_EMPTY_ENTRY)
    {
        return surface;
    }

    float4 instance = _SpikeInstances[entry];
    uint hash = SpikeHash(entry);
    float height = _BladeHeight * lerp(0.7, 1.3, SpikeHashToUnit(hash));
    float width = _BladeWidth * lerp(0.8, 1.2, SpikeHashToUnit(SpikeHash(hash)));
    float sinYaw;
    float cosYaw;
    sincos(instance.w, sinYaw, cosYaw);
    float3 forward = float3(sinYaw, 0.0, cosYaw);
    float3 right = float3(cosYaw, 0.0, -sinYaw);

    float t = templateVertex.y;
    float curve = _BladeBend * t * t;
    surface.positionWS = instance.xyz
        + right * (templateVertex.x * 0.5 * width * (1.0 - t))
        + float3(0.0, t * height, 0.0)
        + forward * (curve * height);
    float3 tangentUp = normalize(float3(0.0, 1.0, 0.0) + forward * (2.0 * _BladeBend * t));
    surface.normalWS = normalize(cross(right, tangentUp));
    surface.heightFraction = t;
    surface.valid = true;
    return surface;
}

half3 SpikeAlbedo(float heightFraction)
{
    return lerp(_BaseColor.rgb, _TipColor.rgb, heightFraction);
}

#endif
