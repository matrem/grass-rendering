namespace Scatter.Rendering.Spike
{
    /// <summary>Values measured every frame by the Phase 1 benchmark (S3), in milliseconds.</summary>
    public enum SpikeBenchmarkMetric
    {
        /// <summary>Whole frame on the GPU (FrameTimingManager).</summary>
        FrameGpu = 0,

        /// <summary>Whole frame on the CPU (FrameTimingManager).</summary>
        FrameCpu = 1,

        CpuMainThread = 2,
        CpuRenderThread = 3,

        /// <summary>Spike cull compute: counter reset, culls, draw arguments.</summary>
        CullGpu = 4,

        /// <summary>URP "MainLightShadow": the shadow cascades (all content). Measured in players.</summary>
        ShadowsGpu = 5,

        /// <summary>URP "DrawDepthNormalPrepass" (all content). Editor only: NaN in players.</summary>
        DepthGpu = 6,

        /// <summary>URP "DrawOpaqueObjects" (all content). Editor only: NaN in players.</summary>
        ColourGpu = 7,

        /// <summary>URP "DrawMotionVectors" (all content). Editor only, and only when the pass runs.</summary>
        MotionVectorsGpu = 8,

        /// <summary>Spike Tick on the main thread.</summary>
        ScatterCpu = 9,

        /// <summary>Draw submission on the CPU: BRG culling callbacks or Graphics.Render*Indirect calls.</summary>
        SubmitCpu = 10,

        /// <summary>URP "ExecuteRenderGraph": everything rendered for the camera. Measured in players.</summary>
        CameraGpu = 11,

        /// <summary>URP "SSAO": the SSAO computation, not its DepthNormals prepass. Measured in players.</summary>
        SsaoGpu = 12,
    }
}
