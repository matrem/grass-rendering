namespace Scatter.Diagnostics
{
    /// <summary>
    /// Scatter timings shown in the HUD (design 12.1, "Scatter timings" group). Times in milliseconds;
    /// NaN means the stage does not exist yet or was not measured.
    /// </summary>
    public struct ScatterStageTimings
    {
        public float CpuMainMs;
        public float GenerationGpuMs;
        public float CullGpuMs;
        public float DepthGpuMs;
        public float ColourGpuMs;
        public float ShadowsGpuMs;
        public float MotionVectorsGpuMs;

        public static ScatterStageTimings NotMeasured => new ScatterStageTimings
        {
            CpuMainMs = float.NaN,
            GenerationGpuMs = float.NaN,
            CullGpuMs = float.NaN,
            DepthGpuMs = float.NaN,
            ColourGpuMs = float.NaN,
            ShadowsGpuMs = float.NaN,
            MotionVectorsGpuMs = float.NaN,
        };
    }
}
