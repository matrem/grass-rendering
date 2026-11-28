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

        private SpikeUrpSettings _urpSettings;

        [TearDown]
        public void TearDown()
        {
            // The runner may have changed the active URP asset (the project asset in the Editor).
            _urpSettings?.Restore();
            _urpSettings = null;
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
            Camera camera = CreateSystemAndCamera();
            SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.Create(1, new[] { 1 << 16 }, 16, new[] { 4, 16 }, 1 << 16);
            var views = new[] { new Pose(new Vector3(0f, 1.8f, -60f), Quaternion.Euler(10f, 0f, 0f)) };
            var runner = new SpikeBenchmarkRunner(_system, camera.transform, cases, views, WarmupFrames, MeasureFrames);

            yield return Run(runner, camera);

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

        [UnityTest]
        public IEnumerator Runner_Phase1bCases_ApplyUrpSettingsFalloffAndCasters_ThenRestore()
        {
            Camera camera = CreateSystemAndCamera();
            _urpSettings = SpikeUrpSettings.ForActivePipeline();
            float originalScale = _urpSettings.RenderScale;
            int originalShadow = _urpSettings.ShadowResolution;
            SpikeBenchmarkCase[] resolution = SpikeBenchmarkPlan.CreateResolution(1, new[] { 1 << 16 }, 16, new[] { 0.5f }, new[] { 512 });
            SpikeBenchmarkCase[] levers = SpikeBenchmarkPlan.CreateLevers(1, 1 << 16, 16);
            var cases = new SpikeBenchmarkCase[resolution.Length + levers.Length];
            resolution.CopyTo(cases, 0);
            levers.CopyTo(cases, resolution.Length);
            var views = new[] { new Pose(new Vector3(0f, 1.8f, -60f), Quaternion.Euler(10f, 0f, 0f)) };
            var runner = new SpikeBenchmarkRunner(_system, camera.transform, cases, views, WarmupFrames, MeasureFrames, _urpSettings);

            yield return Run(runner, camera);

            Assert.That(runner.IsDone, Is.True, "The benchmark did not finish.");
            if (_urpSettings.IsAvailable)
            {
                Assert.That(runner.Results[1].RenderScale, Is.EqualTo(0.5f));
                Assert.That(runner.Results[1].ShadowResolution, Is.EqualTo(512));
                Assert.That(runner.Results[3].RenderScale, Is.EqualTo(originalScale), "Levers cases keep the project values.");
                Assert.That(runner.Results[3].ShadowResolution, Is.EqualTo(originalShadow));
            }

            for (int i = 0; i < runner.Results.Length; i++)
            {
                SpikeBenchmarkResult result = runner.Results[i];
                Assert.That(result.VisibleLod0 + result.VisibleLod1, Is.EqualTo(result.VisibleBlades), result.Case.ToString());
            }

            // Levers: falloff off, then on, then on with widening, 5 caster presets each, after the baseline at index 2.
            const int firstLever = 3;
            SpikeBenchmarkResult allBlades = runner.Results[firstLever];
            SpikeBenchmarkResult thinned = runner.Results[firstLever + SpikeShadowCasterMask.PresetCount];
            Assert.That(thinned.Case.DensityFalloff, Is.EqualTo(SpikeDensityFalloff.On), "Test setup.");
            Assert.That(thinned.VisibleBlades, Is.GreaterThan(0u));
            Assert.That(thinned.VisibleBlades, Is.LessThan(allBlades.VisibleBlades), "The falloff keeps fewer blades.");
            Assert.That(_system.Config.DensityFalloff, Is.EqualTo(SpikeDensityFalloff.OnWiden), "The last case was applied.");
            Assert.That(_system.Config.ShadowCasters, Is.EqualTo(SpikeShadowCasters.None));

            _urpSettings.Restore();
            Assert.That(_urpSettings.RenderScale, Is.EqualTo(originalScale));
            Assert.That(_urpSettings.ShadowResolution, Is.EqualTo(originalShadow));
        }

        private Camera CreateSystemAndCamera()
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
            return camera;
        }

        private IEnumerator Run(SpikeBenchmarkRunner runner, Camera camera)
        {
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
        }
    }
}
