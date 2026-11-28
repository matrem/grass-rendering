namespace Scatter.Diagnostics
{
    /// <summary>
    /// Columns of the per-frame CSV (design 12.1): frame values, then scatter timings.
    /// Later phases append their columns here (blade counts, streaming, warnings).
    /// </summary>
    public static class FrameCsvColumns
    {
        public const int Frame = 0;
        public const int FrameTimeMs = 1;
        public const int CpuFrameMs = 2;
        public const int CpuMainThreadMs = 3;
        public const int CpuRenderThreadMs = 4;
        public const int GpuFrameMs = 5;
        public const int GcAllocatedInFrameBytes = 6;
        public const int ScatterCpuMainMs = 7;
        public const int GenerationGpuMs = 8;
        public const int CullGpuMs = 9;
        public const int DepthGpuMs = 10;
        public const int ColourGpuMs = 11;
        public const int ShadowsGpuMs = 12;
        public const int MotionVectorsGpuMs = 13;
        public const int CameraGpuMs = 14;
        public const int SsaoGpuMs = 15;

        /// <summary>Header names, in column order. Allocates: call at startup.</summary>
        public static string[] CreateNames()
        {
            return new[]
            {
                "frame",
                "frameTimeMs",
                "cpuFrameMs",
                "cpuMainThreadMs",
                "cpuRenderThreadMs",
                "gpuFrameMs",
                "gcAllocatedInFrameBytes",
                "scatterCpuMainMs",
                "generationGpuMs",
                "cullGpuMs",
                "depthGpuMs",
                "colourGpuMs",
                "shadowsGpuMs",
                "motionVectorsGpuMs",
                "cameraGpuMs",
                "ssaoGpuMs",
            };
        }

        /// <summary>Records one frame. Does not allocate.</summary>
        public static void Record(ScatterCsvLogger logger, int frame, in FrameStats stats, in ScatterStageTimings timings)
        {
            logger.BeginRow();
            logger.Set(Frame, frame);
            logger.Set(FrameTimeMs, stats.FrameTimeMs);
            logger.Set(CpuFrameMs, stats.CpuFrameMs);
            logger.Set(CpuMainThreadMs, stats.CpuMainThreadMs);
            logger.Set(CpuRenderThreadMs, stats.CpuRenderThreadMs);
            logger.Set(GpuFrameMs, stats.GpuFrameMs);
            logger.Set(GcAllocatedInFrameBytes, stats.GcAllocatedInFrameBytes);
            logger.Set(ScatterCpuMainMs, timings.CpuMainMs);
            logger.Set(GenerationGpuMs, timings.GenerationGpuMs);
            logger.Set(CullGpuMs, timings.CullGpuMs);
            logger.Set(DepthGpuMs, timings.DepthGpuMs);
            logger.Set(ColourGpuMs, timings.ColourGpuMs);
            logger.Set(ShadowsGpuMs, timings.ShadowsGpuMs);
            logger.Set(MotionVectorsGpuMs, timings.MotionVectorsGpuMs);
            logger.Set(CameraGpuMs, timings.CameraGpuMs);
            logger.Set(SsaoGpuMs, timings.SsaoGpuMs);
            logger.EndRow();
        }
    }
}
