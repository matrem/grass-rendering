using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Scatter.Rendering.Spike;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Rendering.Spike
{
    public sealed class SpikeBenchmarkTests
    {
        private static readonly int[] s_Counts = { 1000, 2000, 4000 };
        private static readonly int[] s_Sweep = { 2, 16, 64 };

        [Test]
        public void Plan_PerView_BaselineThenAllCombinationsThenKSweep()
        {
            SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.Create(2, s_Counts, 16, s_Sweep, 4000);

            // Per view: 1 baseline + 2 backends x 3 draw types x 3 counts + 2 backends x 2 sweep values (K16 at 4000 already measured).
            const int perView = 1 + 18 + 4;
            Assert.That(cases.Length, Is.EqualTo(2 * perView));
            Assert.That(cases[0].ScatterEnabled, Is.False);
            Assert.That(cases[perView].ScatterEnabled, Is.False);
            Assert.That(cases[perView].View, Is.EqualTo(1));
            for (int i = 0; i < cases.Length; i++)
            {
                if (cases[i].DrawType != SpikeDrawType.ProceduralIndexedIndirect)
                {
                    Assert.That(cases[i].BladesPerInstance, Is.EqualTo(1), cases[i].ToString());
                }

                for (int j = i + 1; j < cases.Length; j++)
                {
                    Assert.That(cases[j].ToString(), Is.Not.EqualTo(cases[i].ToString()), "Cases measured twice.");
                }
            }
        }

        [Test]
        public void ResolutionPlan_PerViewScaleAndShadowMap_BaselineThenEachBladeCount()
        {
            float[] scales = { 1f, 0.5f, 0.25f };
            int[] shadows = { 2048, 512 };
            SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.CreateResolution(2, new[] { 1000, 4000 }, 16, scales, shadows);

            Assert.That(cases.Length, Is.EqualTo(2 * 3 * 2 * 3));
            for (int i = 0; i < cases.Length; i++)
            {
                SpikeBenchmarkCase benchmarkCase = cases[i];
                Assert.That(benchmarkCase.ScatterEnabled, Is.EqualTo(i % 3 != 0), "Each setting starts with its baseline.");
                Assert.That(benchmarkCase.RenderScale, Is.EqualTo(scales[i / 6 % 3]));
                Assert.That(benchmarkCase.ShadowResolution, Is.EqualTo(shadows[i / 3 % 2]));
                Assert.That(benchmarkCase.View, Is.EqualTo(i / 18));
                if (benchmarkCase.ScatterEnabled)
                {
                    Assert.That(benchmarkCase.Backend, Is.EqualTo(SpikeBackend.Brg));
                    Assert.That(benchmarkCase.DrawType, Is.EqualTo(SpikeDrawType.ProceduralIndexedIndirect));
                    Assert.That(benchmarkCase.BladesPerInstance, Is.EqualTo(16));
                    Assert.That(benchmarkCase.DensityFalloff, Is.EqualTo(SpikeDensityFalloff.Off));
                }
            }

            AssertUnique(cases);
        }

        [Test]
        public void LeversPlan_PerView_BaselineThenEveryFalloffAndCasterPreset()
        {
            SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.CreateLevers(2, 4000, 16);

            const int perView = 1 + 3 * SpikeShadowCasterMask.PresetCount;
            Assert.That(cases.Length, Is.EqualTo(2 * perView));
            Assert.That(cases[0].ScatterEnabled, Is.False);
            Assert.That(cases[perView].ScatterEnabled, Is.False);
            Assert.That(cases[perView].View, Is.EqualTo(1));
            for (int i = 1; i < perView; i++)
            {
                Assert.That(cases[i].BladeCount, Is.EqualTo(4000));
                Assert.That(cases[i].Backend, Is.EqualTo(SpikeBackend.Brg));
                Assert.That(cases[i].DrawType, Is.EqualTo(SpikeDrawType.ProceduralIndexedIndirect));
                Assert.That(cases[i].RenderScale, Is.EqualTo(0f), "Project setting.");
            }

            Assert.That(cases[perView - 1].DensityFalloff, Is.EqualTo(SpikeDensityFalloff.OnWiden));
            Assert.That(cases[perView - 1].ShadowCasters, Is.EqualTo(SpikeShadowCasters.None));
            AssertUnique(cases);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Runner_AppliesTheObjectLayerSetting_ToEveryCase(bool objectsEnabled)
        {
            ScatterSpikeConfig config = ScatterSpikeConfig.Default;
            config.MaxBladeCount = 1 << 12;
            config.BladeCount = 1 << 12;
            config.ObjectCount = 1 << 8;
            config.ObjectsEnabled = !objectsEnabled;
            var system = new ScatterSpikeSystem(ScatterSpikeResources.Load(), config);
            var camera = new UnityEngine.GameObject("SpikeBenchmarkTests Camera");
            try
            {
                SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.CreateLevers(1, 1 << 12, 16);
                var views = new[] { new UnityEngine.Pose(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity) };
                var runner = new SpikeBenchmarkRunner(system, camera.transform, cases, views, 0, 1, null, objectsEnabled);

                for (int i = 0; i < 4; i++)
                {
                    runner.Step(default, Scatter.Diagnostics.ScatterStageTimings.NotMeasured);
                    Assert.That(system.Config.ObjectsEnabled, Is.EqualTo(objectsEnabled), $"Step {i}.");
                }
            }
            finally
            {
                system.Dispose();
                UnityEngine.Object.DestroyImmediate(camera);
            }
        }

        private static void AssertUnique(SpikeBenchmarkCase[] cases)
        {
            for (int i = 0; i < cases.Length; i++)
            {
                for (int j = i + 1; j < cases.Length; j++)
                {
                    Assert.That(cases[j].ToString(), Is.Not.EqualTo(cases[i].ToString()), "Cases measured twice.");
                }
            }
        }

        [Test]
        public void Recorder_MedianAndPercentile_SkipNaN_AndNaNWithoutSamples()
        {
            var recorder = new SpikeBenchmarkRecorder(100);
            for (int i = 1; i <= 100; i++)
            {
                recorder.Add(SpikeBenchmarkMetric.FrameGpu, i);
                recorder.Add(SpikeBenchmarkMetric.ColourGpu, float.NaN);
            }

            var result = new SpikeBenchmarkResult();
            recorder.Summarize(result);

            Assert.That(result.MedianMs[(int)SpikeBenchmarkMetric.FrameGpu], Is.EqualTo(50f));
            Assert.That(result.Percentile99Ms[(int)SpikeBenchmarkMetric.FrameGpu], Is.EqualTo(99f));
            Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.FrameGpu], Is.EqualTo(100));
            Assert.That(result.SampleCount[(int)SpikeBenchmarkMetric.ColourGpu], Is.EqualTo(0));
            Assert.That(float.IsNaN(result.MedianMs[(int)SpikeBenchmarkMetric.ColourGpu]), Is.True);
        }

        [Test]
        public void Recorder_AddAndSummarize_DoNotAllocate()
        {
            var recorder = new SpikeBenchmarkRecorder(300);
            var result = new SpikeBenchmarkResult();
            recorder.Add(SpikeBenchmarkMetric.FrameGpu, 1f);
            recorder.Summarize(result);

            Assert.That(() =>
            {
                recorder.Add(SpikeBenchmarkMetric.FrameGpu, 2f);
                recorder.Summarize(result);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Csv_OneRowPerCase_DotDecimals_EmptyNaN()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
            try
            {
                SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.CreateLevers(1, 1000, 16);
                var results = new SpikeBenchmarkResult[2];
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = new SpikeBenchmarkResult { Case = cases[i] };
                    for (int m = 0; m < SpikeBenchmarkResult.MetricCount; m++)
                    {
                        results[i].MedianMs[m] = 1.5f;
                        results[i].Percentile99Ms[m] = float.NaN;
                    }
                }

                // Second row: a levers case as the runner fills it (URP values applied, visible blades per LOD).
                results[1].Case.DensityFalloff = SpikeDensityFalloff.OnWiden;
                results[1].Case.ShadowCasters = SpikeShadowCasters.Lod0FirstCascade;
                results[1].RenderScale = 0.5f;
                results[1].ShadowResolution = 512;
                results[1].VisibleLod0 = 3;
                results[1].VisibleLod1 = 4;
                results[1].VisibleBlades = 7;

                var writer = new StringWriter(CultureInfo.InvariantCulture);
                SpikeBenchmarkCsv.Write(writer, results, results.Length, "Test GPU", "Direct3D12", 2560, 1440);
                string[] lines = writer.ToString().TrimEnd().Split('\n');

                Assert.That(lines.Length, Is.EqualTo(1 + results.Length));
                Assert.That(lines[0], Does.StartWith("view,scatter,backend,drawType,bladeCount,K,renderScale,shadowResolution,densityFalloff,shadowCasters,"
                    + "visibleBlades,visibleLod0,visibleLod1,visibleObjects,overflow,"));
                Assert.That(lines[0], Does.Contain("frameGpuMedianMs,frameGpuP99Ms"));
                Assert.That(lines[1], Does.StartWith("0,off,,,0,1,,,,,"), "Baseline: no URP values, no falloff or casters.");
                Assert.That(lines[2], Does.Contain(",1.5,,"));
                Assert.That(lines[2], Does.Contain(",16,0.5,512,OnWiden,Lod0FirstCascade,"));
                Assert.That(lines[2].TrimEnd(), Does.EndWith(",\"Test GPU\",Direct3D12,2560,1440"));
                int columns = lines[0].TrimEnd().Split(',').Length;
                Assert.That(lines[2].TrimEnd().Split(',').Length, Is.EqualTo(columns));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
