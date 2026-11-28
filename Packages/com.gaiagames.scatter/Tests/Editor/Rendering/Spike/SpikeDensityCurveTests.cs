using NUnit.Framework;
using Scatter.Rendering.Spike;

namespace Scatter.Tests.Rendering.Spike
{
    /// <summary>The Phase 1b density curve and width factor (C# side; SpikeCullerTests checks the HLSL side on the GPU).</summary>
    public sealed class SpikeDensityCurveTests
    {
        private const float Tolerance = 1e-5f;

        [TestCase(0f, 1f)]
        [TestCase(30f, 1f)]
        [TestCase(55f, 0.625f)]
        [TestCase(80f, 0.25f)]
        [TestCase(140f, 0.155f)]
        [TestCase(200f, 0.06f)]
        [TestCase(500f, 0.06f)]
        public void Default_IsPiecewiseLinear_ThenConstant(float distanceMeters, float expected)
        {
            Assert.That(SpikeDensityCurve.Default.Evaluate(distanceMeters), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Default_NeverIncreasesWithDistance()
        {
            SpikeDensityCurve curve = SpikeDensityCurve.Default;
            float previous = curve.Evaluate(0f);
            for (float distance = 0.5f; distance < 300f; distance += 0.5f)
            {
                float density = curve.Evaluate(distance);
                Assert.That(density, Is.LessThanOrEqualTo(previous), $"{distance} m");
                previous = density;
            }
        }

        [TestCase(1f, 3f, 1f)]
        [TestCase(0.25f, 3f, 2f)]
        [TestCase(0.06f, 3f, 3f)]
        [TestCase(0.06f, 8f, 4.0824829f)]
        [TestCase(0f, 3f, 3f)]
        [TestCase(0.25f, 0.5f, 1f)]
        public void WidenFactor_IsInverseSquareRoot_Capped_AndNeverNarrows(float density, float maxFactor, float expected)
        {
            Assert.That(SpikeDensityCurve.WidenFactor(density, maxFactor), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Vectors_HoldTheValuesTheHlslReads()
        {
            SpikeDensityCurve curve = SpikeDensityCurve.Default;

            Assert.That(curve.ToDistancesVector(), Is.EqualTo(new UnityEngine.Vector4(30f, 80f, 200f, 0f)));
            Assert.That(curve.ToValuesVector(3f), Is.EqualTo(new UnityEngine.Vector4(0.25f, 0.06f, 3f, 0f)));
            Assert.That(curve.ToValuesVector(0f).z, Is.EqualTo(1f), "A max factor below 1 means no widening.");
        }
    }
}
