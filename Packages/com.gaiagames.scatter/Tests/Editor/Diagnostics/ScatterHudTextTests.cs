using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine;

namespace Scatter.Tests.Diagnostics
{
    public sealed class ScatterHudTextTests
    {
        private ScatterQuality _quality;

        [SetUp]
        public void SetUp()
        {
            // Default targets: frame 16.7 ms, scatter CPU main 0.5 ms.
            _quality = ScriptableObject.CreateInstance<ScatterQuality>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_quality);
        }

        [Test]
        public void Build_ValueOverTarget_IsRed()
        {
            var stats = new FrameStats { CpuFrameMs = 10f, GpuFrameMs = 25f };
            ScatterStageTimings timings = ScatterStageTimings.NotMeasured;
            timings.CpuMainMs = 0.8f;

            string text = Build(stats, timings);

            Assert.That(text, Does.Contain("GPU <color="));
            Assert.That(text, Does.Contain("25.00 ms</color>"));
            Assert.That(text, Does.Contain("CPU main <color="));
            Assert.That(text, Does.Contain("CPU 10.00 ms"));
        }

        [Test]
        public void Build_NaNValue_ShowsDash()
        {
            var stats = new FrameStats { CpuMainThreadMs = float.NaN };

            string text = Build(stats, ScatterStageTimings.NotMeasured);

            Assert.That(text, Does.Contain("(main -"));
            Assert.That(text, Does.Contain("cull -"));
        }

        [TestCase(-1L, "GC alloc in frame -")]
        [TestCase(0L, "GC alloc in frame 0 B")]
        [TestCase(128L, "GC alloc in frame <color=#FF5050>128 B</color>")]
        public void Build_GcAllocated_ShowsDashZeroOrRed(long bytes, string expected)
        {
            var stats = new FrameStats { GcAllocatedInFrameBytes = bytes };

            string text = Build(stats, ScatterStageTimings.NotMeasured);

            Assert.That(text, Does.Contain(expected));
        }

        [Test]
        public void Build_StatusText_ShownAfterTheTier()
        {
            var history = new FrameTimeHistory(4);
            history.Add(10f);

            string text = new ScatterHudText().Build("SCATTER", new FrameStats(), history, ScatterStageTimings.NotMeasured, _quality, "Test", null, "Spike state");

            Assert.That(text, Does.Contain("Tier Test"));
            Assert.That(text.IndexOf("Spike state", System.StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("Tier Test", System.StringComparison.Ordinal)));
            Assert.That(text.IndexOf("Spike state", System.StringComparison.Ordinal), Is.LessThan(text.IndexOf("Frame", System.StringComparison.Ordinal)));
        }

        private string Build(in FrameStats stats, in ScatterStageTimings timings)
        {
            var history = new FrameTimeHistory(4);
            history.Add(10f);
            return new ScatterHudText().Build("SCATTER", stats, history, timings, _quality, "Test", null);
        }
    }
}
