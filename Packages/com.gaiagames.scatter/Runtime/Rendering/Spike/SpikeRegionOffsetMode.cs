namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// How a BRG draw command points at its region of the visible list (Implementation Phase 1, S1).
    /// </summary>
    public enum SpikeRegionOffsetMode
    {
        /// <summary><c>visibleOffset</c> = first entry of the region; the whole buffer is bound (as the GPU Resident Drawer does).</summary>
        VisibleOffset = 0,

        /// <summary><c>visibleInstancesBufferWindowOffset</c> = first entry of the region, in bytes.</summary>
        WindowOffsetBytes = 1,

        /// <summary><c>visibleInstancesBufferWindowOffset</c> = first entry of the region, in entries.</summary>
        WindowOffsetEntries = 2,
    }
}
