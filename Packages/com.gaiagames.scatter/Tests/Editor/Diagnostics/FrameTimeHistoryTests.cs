using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Diagnostics
{
    public sealed class FrameTimeHistoryTests
    {
        [Test]
        public void Percentile_UsesNearestRank()
        {
            var history = new FrameTimeHistory(100);
            for (int i = 100; i >= 1; i--)
            {
                history.Add(i);
            }

            Assert.That(history.Percentile(0.99f), Is.EqualTo(99f));
            Assert.That(history.Percentile(0.5f), Is.EqualTo(50f));
            Assert.That(history.Percentile(1f), Is.EqualTo(100f));
            Assert.That(history.Percentile(0f), Is.EqualTo(1f));
        }

        [Test]
        public void Percentile_WithFewSamples_ReturnsWorst()
        {
            var history = new FrameTimeHistory(300);
            history.Add(5f);
            history.Add(40f);
            history.Add(6f);

            Assert.That(history.Percentile(0.99f), Is.EqualTo(40f));
        }

        [Test]
        public void Add_PastCapacity_KeepsOnlyLatestFrames()
        {
            var history = new FrameTimeHistory(3);
            history.Add(50f);
            history.Add(1f);
            history.Add(2f);
            history.Add(3f);

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.WorstMs(), Is.EqualTo(3f));
            Assert.That(history.AverageMs(), Is.EqualTo(2f));
        }

        [Test]
        public void Queries_DoNotAllocate()
        {
            var history = new FrameTimeHistory(300);
            for (int i = 0; i < 400; i++)
            {
                history.Add(i % 17);
            }

            history.Percentile(0.99f);

            Assert.That(() =>
            {
                history.Add(4f);
                history.Percentile(0.99f);
                history.WorstMs();
                history.AverageMs();
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
