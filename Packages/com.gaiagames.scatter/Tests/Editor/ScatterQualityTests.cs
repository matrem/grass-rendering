using NUnit.Framework;
using UnityEngine;

namespace Scatter.Tests
{
    public sealed class ScatterQualityTests
    {
        private ScatterQuality _low;
        private ScatterQuality _high;

        [SetUp]
        public void SetUp()
        {
            _low = ScriptableObject.CreateInstance<ScatterQuality>();
            _high = ScriptableObject.CreateInstance<ScatterQuality>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_low);
            Object.DestroyImmediate(_high);
        }

        [Test]
        public void ForQualityLevel_ClampsLevelToTiers()
        {
            ScatterQuality[] tiers = { _low, _high };

            Assert.That(ScatterQuality.ForQualityLevel(tiers, 0), Is.SameAs(_low));
            Assert.That(ScatterQuality.ForQualityLevel(tiers, 1), Is.SameAs(_high));
            Assert.That(ScatterQuality.ForQualityLevel(tiers, 5), Is.SameAs(_high));
            Assert.That(ScatterQuality.ForQualityLevel(tiers, -1), Is.SameAs(_low));
        }

        [Test]
        public void ForQualityLevel_WithoutTiers_ReturnsNull()
        {
            Assert.That(ScatterQuality.ForQualityLevel(null, 0), Is.Null);
            Assert.That(ScatterQuality.ForQualityLevel(System.Array.Empty<ScatterQuality>(), 0), Is.Null);
        }
    }
}
