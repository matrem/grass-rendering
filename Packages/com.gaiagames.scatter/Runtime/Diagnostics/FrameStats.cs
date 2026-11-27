namespace Scatter.Diagnostics
{
    /// <summary>
    /// Whole-frame values of one frame (design 12.1, "Frame" group). Times in milliseconds;
    /// a value is NaN when the platform does not provide it.
    /// </summary>
    public struct FrameStats
    {
        public float FrameTimeMs;
        public float CpuFrameMs;
        public float CpuMainThreadMs;
        public float CpuRenderThreadMs;
        public float GpuFrameMs;

        /// <summary>Managed allocation of the previous frame, in bytes; -1 when the counter is unavailable.</summary>
        public long GcAllocatedInFrameBytes;
    }
}
