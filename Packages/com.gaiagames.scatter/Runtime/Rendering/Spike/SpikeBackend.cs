namespace Scatter.Rendering.Spike
{
    /// <summary>Draw backends compared by the Phase 1 spike (design 6.3), on the same cull output.</summary>
    public enum SpikeBackend
    {
        /// <summary>BatchRendererGroup indirect draw commands (main path).</summary>
        Brg = 0,

        /// <summary>Graphics.RenderMeshIndirect, RenderPrimitivesIndirect and RenderPrimitivesIndexedIndirect (baseline).</summary>
        Indirect = 1,
    }
}
