namespace Scatter.Diagnostics
{
    /// <summary>
    /// Scatter timings shown in the HUD (design 12.1, "Scatter timings" group). Times in milliseconds;
    /// NaN means the stage does not exist yet or was not measured. URP pass times are whole passes (scatter and other
    /// content); depth, colour and motion vectors are measured in the Editor only (<see cref="UrpPassTimings"/>).
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

        /// <summary>Everything URP renders for the camera (Render Graph execution).</summary>
        public float CameraGpuMs;

        /// <summary>SSAO computation (not its DepthNormals prepass).</summary>
        public float SsaoGpuMs;

        public static ScatterStageTimings NotMeasured => new ScatterStageTimings
        {
            CpuMainMs = float.NaN,
            GenerationGpuMs = float.NaN,
            CullGpuMs = float.NaN,
            DepthGpuMs = float.NaN,
            ColourGpuMs = float.NaN,
            ShadowsGpuMs = float.NaN,
            MotionVectorsGpuMs = float.NaN,
            CameraGpuMs = float.NaN,
            SsaoGpuMs = float.NaN,
        };
    }
}
