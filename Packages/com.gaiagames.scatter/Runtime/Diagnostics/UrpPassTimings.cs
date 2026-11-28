using System;
using UnityEngine.Rendering;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// GPU time of the URP work that draws the scatter (design 12.1), read from URP's own profiler markers: a
    /// <see cref="ProfilingSampler"/> created with the same name shares the marker. Times are whole-pass totals
    /// (scatter and other content). Measured with <see cref="GpuMarkerProbe"/> (Phase 1, S3): in players only the scopes
    /// URP records itself have a GPU time (camera, main light shadows, SSAO); the Render Graph pass markers (depth,
    /// colour, motion vectors) have one in the Editor only, whatever the graphics threading mode. Editor and development
    /// players only (profiler markers); NaN when not measured.
    /// </summary>
    public sealed class UrpPassTimings : IDisposable
    {
        // Measured in players and in the Editor (URP 17.6).
        public const string CameraMarker = "ExecuteRenderGraph";
        public const string ShadowsMarker = "MainLightShadow";
        public const string SsaoMarker = "SSAO";

        // Render Graph pass markers: measured in the Editor only.
        public const string DepthMarker = "DrawDepthNormalPrepass";
        public const string ColourMarker = "DrawOpaqueObjects";
        public const string MotionVectorsMarker = "DrawMotionVectors";

        private readonly ProfilingSampler _camera = new ProfilingSampler(CameraMarker);
        private readonly ProfilingSampler _shadows = new ProfilingSampler(ShadowsMarker);
        private readonly ProfilingSampler _ssao = new ProfilingSampler(SsaoMarker);
        private readonly ProfilingSampler _depth = new ProfilingSampler(DepthMarker);
        private readonly ProfilingSampler _colour = new ProfilingSampler(ColourMarker);
        private readonly ProfilingSampler _motionVectors = new ProfilingSampler(MotionVectorsMarker);

        public UrpPassTimings()
        {
            SetRecording(true);
        }

        /// <summary>Writes the URP rows of <paramref name="timings"/>. Does not allocate.</summary>
        public void Fill(ref ScatterStageTimings timings)
        {
            timings.CameraGpuMs = Read(_camera);
            timings.ShadowsGpuMs = Read(_shadows);
            timings.SsaoGpuMs = Read(_ssao);
            timings.DepthGpuMs = Read(_depth);
            timings.ColourGpuMs = Read(_colour);
            timings.MotionVectorsGpuMs = Read(_motionVectors);
        }

        public void Dispose()
        {
            SetRecording(false);
            _camera.Dispose();
            _shadows.Dispose();
            _ssao.Dispose();
            _depth.Dispose();
            _colour.Dispose();
            _motionVectors.Dispose();
        }

        private void SetRecording(bool enabled)
        {
            _camera.enableRecording = enabled;
            _shadows.enableRecording = enabled;
            _ssao.enableRecording = enabled;
            _depth.enableRecording = enabled;
            _colour.enableRecording = enabled;
            _motionVectors.enableRecording = enabled;
        }

        private static float Read(ProfilingSampler sampler)
        {
            // Not ProfilingSampler.gpuSampleCount: it throws while the recorder has no sample (Core RP 17.6).
            // A pass that ran always takes some GPU time: 0 means not measured.
            float milliseconds = sampler.gpuElapsedTime;
            return milliseconds > 0f ? milliseconds : float.NaN;
        }
    }
}
