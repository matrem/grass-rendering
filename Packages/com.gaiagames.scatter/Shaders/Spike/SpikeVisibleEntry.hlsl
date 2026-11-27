#ifndef SCATTER_SPIKE_VISIBLE_ENTRY_INCLUDED
#define SCATTER_SPIKE_VISIBLE_ENTRY_INCLUDED

// Reads the instance index written by the cull compute in the visible list (design 6.3).
// Include after Core.hlsl and after the UnityPerMaterial cbuffer, which must declare
// _BladesPerInstance (K) and _RegionStart (first entry of the region, used without BRG only).

// Instance ID field of the vertex input. With BRG (DOTS instancing) the Unity macro declares it; the
// RenderMeshIndirect path declares SV_InstanceID itself, without the instancing keywords.
#if UNITY_ANY_INSTANCING_ENABLED
    #define SPIKE_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_INPUT_INSTANCE_ID
    #define SPIKE_SETUP_INSTANCE_ID(input) UNITY_SETUP_INSTANCE_ID(input)
    #define SPIKE_INSTANCE_ID(input) unity_InstanceID
#else
    #define SPIKE_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
    #define SPIKE_SETUP_INSTANCE_ID(input)
    #define SPIKE_INSTANCE_ID(input) input.instanceID
#endif

#if !defined(UNITY_DOTS_INSTANCING_ENABLED)
// Bound through the material by the RenderMeshIndirect backend. With BRG, BRG binds the visible list itself.
ByteAddressBuffer _SpikeVisibleList;
#endif

// instanceID: instance of the draw. bladeInInstance: blade inside the instance (0 unless K > 1).
uint SpikeLoadVisibleEntry(uint instanceID, uint bladeInInstance)
{
    uint k = max((uint)_BladesPerInstance, 1u);
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    if (k == 1u)
    {
        // BRG reads visibleOffset + instance ID in the bound window of the visible list.
        return GetDOTSInstanceIndex();
    }

    // K blades per instance: BRG gives the first entry of the draw (its visibleOffset); entries are read
    // directly from the same bound buffer.
    uint firstEntry = GetDOTSIndirectVisibleIndex() - instanceID;
    return LoadDOTSIndirectInstanceIndex(firstEntry + instanceID * k + bladeInInstance);
#else
    return _SpikeVisibleList.Load(((uint)_RegionStart + instanceID * k + bladeInInstance) << 2);
#endif
}

#endif
