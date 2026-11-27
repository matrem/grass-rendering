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
                SpikeBenchmarkCase[] cases = SpikeBenchmarkPlan.Create(1, new[] { 1000 }, 16, new[] { 16 }, 1000);
                var results = new SpikeBenchmarkResult[cases.Length];
                for (int i = 0; i < cases.Length; i++)
                {
                    results[i] = new SpikeBenchmarkResult { Case = cases[i] };
                    for (int m = 0; m < SpikeBenchmarkResult.MetricCount; m++)
                    {
                        results[i].MedianMs[m] = 1.5f;
                        results[i].Percentile99Ms[m] = float.NaN;
                    }
                }

                var writer = new StringWriter(CultureInfo.InvariantCulture);
                SpikeBenchmarkCsv.Write(writer, results, results.Length, "Test GPU", "Direct3D12", 2560, 1440);
                string[] lines = writer.ToString().TrimEnd().Split('\n');

                Assert.That(lines.Length, Is.EqualTo(1 + cases.Length));
                Assert.That(lines[0], Does.StartWith("view,scatter,backend,drawType,bladeCount,K,visibleBlades"));
                Assert.That(lines[0], Does.Contain("frameGpuMedianMs,frameGpuP99Ms"));
                Assert.That(lines[1], Does.StartWith("0,off,,,0,1,"));
                Assert.That(lines[2], Does.Contain(",1.5,,"));
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
