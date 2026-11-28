using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Diagnostics
{
    public sealed class UrpPassTimingsTests
    {
        [Test]
        public void Fill_WithoutRendering_GivesNaN_AndDoesNotAllocate()
        {
            using (var passTimings = new UrpPassTimings())
            {
                ScatterStageTimings timings = ScatterStageTimings.NotMeasured;
                passTimings.Fill(ref timings);

                Assert.That(float.IsNaN(timings.MotionVectorsGpuMs), Is.True, "No motion vectors pass without TAA.");
                Assert.That(() => passTimings.Fill(ref timings), Is.Not.AllocatingGCMemory());
            }
        }
    }
}
