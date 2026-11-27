namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Draw command types compared by the Phase 1 spike (Implementation Phase 1). Values mirrored by
    /// SPIKE_DRAW_* in SpikeCommon.hlsl.
    /// </summary>
    public enum SpikeDrawType : uint
    {
        /// <summary>One blade mesh instance per blade (BRG <c>BatchDrawCommandIndirect</c>).</summary>
        MeshIndirect = 0,

        /// <summary>One blade per instance, built from SV_VertexID, no mesh (BRG <c>BatchDrawCommandProceduralIndirect</c>).</summary>
        ProceduralIndirect = 1,

        /// <summary>K blades per instance through an index buffer (BRG <c>BatchDrawCommandProceduralIndirect</c> with an index buffer).</summary>
        ProceduralIndexedIndirect = 2,
    }
}
