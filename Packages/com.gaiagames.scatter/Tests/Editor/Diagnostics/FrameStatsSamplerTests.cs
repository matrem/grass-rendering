using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Diagnostics
{
    public sealed class FrameStatsSamplerTests
    {
        [Test]
        public void Sample_DoesNotAllocate()
        {
            using (var sampler = new FrameStatsSampler(300))
            {
                sampler.Sample();

                Assert.That(() => sampler.Sample(), Is.Not.AllocatingGCMemory());
                Assert.That(sampler.History.Count, Is.EqualTo(2));
            }
        }
    }
}
