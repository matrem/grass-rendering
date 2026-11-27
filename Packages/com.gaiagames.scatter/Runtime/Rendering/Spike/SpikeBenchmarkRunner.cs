using Scatter.Diagnostics;
using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Runs the Phase 1 benchmark (S3) on a <see cref="ScatterSpikeSystem"/>: for each case, applies its settings and
    /// camera view, waits <c>warmupFrames</c> frames (GPU timings arrive a few frames late), then records
    /// <c>measureFrames</c> frames and summarises them. Call <see cref="Step"/> once per frame, before the system Tick.
    /// Results are allocated in the constructor; stepping does not allocate.
    /// </summary>
    public sealed class SpikeBenchmarkRunner
    {
        private readonly ScatterSpikeSystem _system;
        private readonly Transform _camera;
        private readonly SpikeBenchmarkCase[] _cases;
        private readonly Pose[] _views;
        private readonly int _warmupFrames;
        private readonly int _measureFrames;
        private readonly SpikeBenchmarkRecorder _recorder;
        private int _caseIndex = -1;
        private int _frame;

        public SpikeBenchmarkRunner(ScatterSpikeSystem system, Transform camera, SpikeBenchmarkCase[] cases, Pose[] views, int warmupFrames, int measureFrames)
        {
            _system = system;
            _camera = camera;
            _cases = cases;
            _views = views;
            _warmupFrames = Mathf.Max(warmupFrames, 0);
            _measureFrames = Mathf.Max(measureFrames, 1);
            _recorder = new SpikeBenchmarkRecorder(_measureFrames);
            Results = new SpikeBenchmarkResult[cases.Length];
            for (int i = 0; i < cases.Length; i++)
            {
                Results[i] = new SpikeBenchmarkResult { Case = cases[i] };
            }
        }

        public SpikeBenchmarkResult[] Results { get; }

        public int CaseCount => _cases.Length;

        /// <summary>Index of the case being measured; -1 before the first <see cref="Step"/>.</summary>
        public int CaseIndex => _caseIndex;

        public bool IsDone => _caseIndex >= _cases.Length;

        /// <summary>
        /// One frame: records the values of the previous frame for the current case, and moves to the next case
        /// when this one has enough frames. <paramref name="timings"/> holds the cull and URP pass GPU times.
        /// </summary>
        public void Step(in FrameStats frame, in ScatterStageTimings timings)
        {
            if (IsDone)
            {
                return;
            }

            if (_caseIndex < 0)
            {
                StartCase(0);
                return;
            }

            ApplyView(_cases[_caseIndex].View);
            _frame++;
            if (_frame <= _warmupFrames)
            {
                return;
            }

            Record(frame, timings);
            if (_frame >= _warmupFrames + _measureFrames)
            {
                FinishCase();
                StartCase(_caseIndex + 1);
            }
        }

        private void Record(in FrameStats frame, in ScatterStageTimings timings)
        {
            _recorder.Add(SpikeBenchmarkMetric.FrameGpu, frame.GpuFrameMs);
            _recorder.Add(SpikeBenchmarkMetric.FrameCpu, frame.CpuFrameMs);
            _recorder.Add(SpikeBenchmarkMetric.CpuMainThread, frame.CpuMainThreadMs);
            _recorder.Add(SpikeBenchmarkMetric.CpuRenderThread, frame.CpuRenderThreadMs);
            _recorder.Add(SpikeBenchmarkMetric.CullGpu, timings.CullGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.ShadowsGpu, timings.ShadowsGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.DepthGpu, timings.DepthGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.ColourGpu, timings.ColourGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.MotionVectorsGpu, timings.MotionVectorsGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.ScatterCpu, _system.TickCpuMs);
            _recorder.Add(SpikeBenchmarkMetric.SubmitCpu, _system.SubmitCpuMs);
            _recorder.Add(SpikeBenchmarkMetric.CameraGpu, timings.CameraGpuMs);
            _recorder.Add(SpikeBenchmarkMetric.SsaoGpu, timings.SsaoGpuMs);
        }

        private void FinishCase()
        {
            SpikeBenchmarkResult result = Results[_caseIndex];
            _recorder.Summarize(result);
            if (result.Case.ScatterEnabled)
            {
                result.VisibleBlades = _system.GetCounter(ScatterSpikeSystem.BladeLod0Region) + _system.GetCounter(ScatterSpikeSystem.BladeLod1Region);
                result.VisibleObjects = _system.GetCounter(ScatterSpikeSystem.ObjectRegion);
                result.Overflow = _system.GetCounter(_system.Culler.OverflowCounterIndex);
            }
        }

        private void StartCase(int index)
        {
            _caseIndex = index;
            _frame = 0;
            _recorder.Clear();
            if (IsDone)
            {
                return;
            }

            SpikeBenchmarkCase benchmarkCase = _cases[index];
            _system.SetScatterEnabled(benchmarkCase.ScatterEnabled);
            if (benchmarkCase.ScatterEnabled)
            {
                _system.SetBackend(benchmarkCase.Backend);
                _system.SetBladeDrawType(benchmarkCase.DrawType);
                _system.SetBladeCount(benchmarkCase.BladeCount);
                if (benchmarkCase.DrawType == SpikeDrawType.ProceduralIndexedIndirect && _system.Config.BladesPerInstance != benchmarkCase.BladesPerInstance)
                {
                    _system.SetBladesPerInstance(benchmarkCase.BladesPerInstance);
                }
            }

            ApplyView(benchmarkCase.View);
        }

        private void ApplyView(int view)
        {
            Pose pose = _views[Mathf.Clamp(view, 0, _views.Length - 1)];
            _camera.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }
}
