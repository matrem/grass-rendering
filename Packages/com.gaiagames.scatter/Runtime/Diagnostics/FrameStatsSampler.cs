using System;
using Unity.Profiling;
using UnityEngine;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// Reads the whole-frame values of design 12.1 once per frame: frame time, CPU main/render and GPU time
    /// (<see cref="FrameTimingManager"/>) and GC allocated in frame (<see cref="ProfilerRecorder"/>).
    /// Builds need Player Settings "Frame Timing Stats" enabled for the CPU/GPU values.
    /// </summary>
    public sealed class FrameStatsSampler : IDisposable
    {
        private const string GcAllocatedInFrameCounter = "GC Allocated In Frame";

        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly FrameTimeHistory _history;
        private ProfilerRecorder _gcAllocatedInFrame;
        private FrameStats _latest;

        public FrameStatsSampler(int historyFrames)
        {
            _history = new FrameTimeHistory(historyFrames);
            _gcAllocatedInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, GcAllocatedInFrameCounter);
        }

        public FrameStats Latest => _latest;
        public FrameTimeHistory History => _history;

        /// <summary>Call once per frame, from the frame driver.</summary>
        public void Sample()
        {
            _latest.FrameTimeMs = Time.unscaledDeltaTime * 1000f;
            _history.Add(_latest.FrameTimeMs);

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                FrameTiming timing = _timings[0];
                _latest.CpuFrameMs = (float)timing.cpuFrameTime;
                _latest.CpuMainThreadMs = (float)timing.cpuMainThreadFrameTime;
                _latest.CpuRenderThreadMs = (float)timing.cpuRenderThreadFrameTime;
                _latest.GpuFrameMs = (float)timing.gpuFrameTime;
            }
            else
            {
                _latest.CpuFrameMs = float.NaN;
                _latest.CpuMainThreadMs = float.NaN;
                _latest.CpuRenderThreadMs = float.NaN;
                _latest.GpuFrameMs = float.NaN;
            }

            _latest.GcAllocatedInFrameBytes = _gcAllocatedInFrame.Valid ? _gcAllocatedInFrame.LastValue : -1;
        }

        public void Dispose()
        {
            _gcAllocatedInFrame.Dispose();
        }
    }
}
