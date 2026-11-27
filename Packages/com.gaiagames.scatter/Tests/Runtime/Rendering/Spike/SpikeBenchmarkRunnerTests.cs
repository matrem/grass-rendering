using System.Collections;
using NUnit.Framework;
using Scatter.Diagnostics;
using Scatter.Rendering.Spike;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scatter.Tests.PlayMode.Rendering.Spike
{
    /// <summary>A short benchmark (one view, small counts, few frames) on a real spike system.</summary>
    public sealed class SpikeBenchmarkRunnerTests
    {
        private const int WarmupFrames = 8;
        private const int MeasureFrames = 4;
        private const int MaxFrames = 500;

        private ScatterSpikeSystem _system;
        private GameObject _cameraObject;
        private FrameStatsSampler _sampler;
        private UrpPassTimings _passTimings;
        private RenderTexture _target;

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
            _system = null;
            _sampler?.Dispose();
            _passTimings?.Dispose();
            Object.Destroy(_cameraObject);
            if (_target != null)
            {
                _target.Release();
                Object.Destroy(_target);
            }
        }

        [UnityTest]
        public IEnumerator Runner_MeasuresEveryCase_WithVisibleBladesAndPassTimings()
        {
            ScatterSpikeConfig config = ScatterSpikeConfig.Default;
            config.MaxBladeCount = 1 << 16;
            config.BladeCount = 1 << 16;
            config.ObjectCount = 1 << 10;
            _system = new ScatterSpikeSystem(ScatterSpikeResources.Load(), config);
            _cameraObject = new GameObject("SpikeBenchmarkRunnerTests Camera");
            Camera camera = _cameraObject.AddComponent<Camera>();
            // Batch mode has no window: a camera renders only into a target texture.
            _target = new RenderTexture(256, 256, 24) { name = "SpikeBenchmarkRunnerTests Target" };
            camera.targetTexture = _target;
            _sampler = new FrameStatsSampler(MeasureFrames);
            _passTimings = new UrpPassTimings();

            SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.Create(1, new[] { 1 << 16 }, 16, new[] { 4, 16 }, 1 << 16);
            var views = new[] { new Pose(new Vector3(0f, 1.8f, -60f), Quaternion.Euler(10f, 0f, 0f)) };
            var runner = new SpikeBenchmarkRunner(_system, camera.transform, cases, views, WarmupFrames, MeasureFrames);

            for (int frame = 0; frame < MaxFrames && !runner.IsDone; frame++)
            {
                _sampler.Sample();
                ScatterStageTimings timings = ScatterStageTimings.NotMeasured;
                timings.CullGpuMs = _system.Config.ScatterEnabled ? _system.CullGpuMs : float.NaN;
                _passTimings.Fill(ref timings);
                runner.Step(_sampler.Latest, timings);
                _system.Tick(camera, true);
                yield return null;
            }

            Assert.That(runner.IsDone, Is.True, "The benchmark did not finish.");
            Assert.That(cases.Length, Is.EqualTo(1 + 6 + 2), "Test setup.");
            Assert.That(camera.transform.position, Is.EqualTo(views[0].position));
            for (int i = 0; i < runner.Results.Length; i++)
            {
                SpikeBenchmarkResult result = runner.Results[i];
                string label = result.Case.ToString();
                Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.ScatterCpu], Is.EqualTo(MeasureFrames), label);
                if (result.Case.ScatterEnabled)
                {
                    // With scatter off this test scene has nothing to draw: URP skips the opaque pass.
                    Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.ColourGpu], Is.GreaterThan(0), label + ": URP colour pass not timed.");
                    Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.CullGpu], Is.GreaterThan(0), label + ": cull not timed.");
                    Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.CameraGpu], Is.GreaterThan(0), label + ": URP camera not timed.");
                    Assert.That(result.VisibleBlades, Is.GreaterThan(0u), label);
                    Assert.That(result.Overflow, Is.EqualTo(0u), label);
                }
            }

            Assert.That(_system.Config.BladesPerInstance, Is.EqualTo(4), "The last case (K sweep, K = 4) was applied.");
        }
    }
}
