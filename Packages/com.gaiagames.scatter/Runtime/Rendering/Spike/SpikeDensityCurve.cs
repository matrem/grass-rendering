using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1b density curve over distance (design 5.2, 5.3): 1 up to <see cref="NearDistanceMeters"/>, then linear to
    /// <see cref="MidDensity"/> at <see cref="MidDistanceMeters"/> and to <see cref="FarDensity"/> at
    /// <see cref="FarDistanceMeters"/>, constant beyond. A blade is kept when its rank is below the density at its distance.
    /// Mirrored by SpikeDensity.hlsl (Shaders/Spike), checked by SpikeCullerTests and SpikeDensityCurveTests.
    /// </summary>
    public struct SpikeDensityCurve
    {
        /// <summary>Smallest density used for the width factor: avoids a division by zero.</summary>
        public const float MinDensity = 1e-4f;

        public float NearDistanceMeters;
        public float MidDistanceMeters;
        public float MidDensity;
        public float FarDistanceMeters;
        public float FarDensity;

        /// <summary>The example curve of design 5.3: 100% up to 30 m, 25% at 80 m, 6% at 200 m.</summary>
        public static SpikeDensityCurve Default => new SpikeDensityCurve
        {
            NearDistanceMeters = 30f,
            MidDistanceMeters = 80f,
            MidDensity = 0.25f,
            FarDistanceMeters = 200f,
            FarDensity = 0.06f,
        };

        /// <summary>Density (0 to 1) at <paramref name="distanceMeters"/> from the cull camera. SpikeDensityAtDistance in HLSL.</summary>
        public float Evaluate(float distanceMeters)
        {
            if (distanceMeters <= NearDistanceMeters)
            {
                return 1f;
            }

            if (distanceMeters <= MidDistanceMeters)
            {
                return Lerp(1f, MidDensity, (distanceMeters - NearDistanceMeters) / (MidDistanceMeters - NearDistanceMeters));
            }

            if (distanceMeters <= FarDistanceMeters)
            {
                return Lerp(MidDensity, FarDensity, (distanceMeters - MidDistanceMeters) / (FarDistanceMeters - MidDistanceMeters));
            }

            return FarDensity;
        }

        /// <summary>
        /// Width factor of a blade drawn at <paramref name="density"/>: 1/sqrt(density), at most
        /// <paramref name="maxFactor"/>. Fewer, wider far blades (design 10.1). SpikeWidenFactor in HLSL.
        /// </summary>
        public static float WidenFactor(float density, float maxFactor)
        {
            return Mathf.Min(1f / Mathf.Sqrt(Mathf.Max(density, MinDensity)), Mathf.Max(maxFactor, 1f));
        }

        /// <summary>_SpikeDensityDistances: x near, y mid, z far (meters).</summary>
        public Vector4 ToDistancesVector()
        {
            return new Vector4(NearDistanceMeters, MidDistanceMeters, FarDistanceMeters, 0f);
        }

        /// <summary>_SpikeDensityValues: x mid density, y far density, z widen max factor (1 = no widening).</summary>
        public Vector4 ToValuesVector(float widenMaxFactor)
        {
            return new Vector4(MidDensity, FarDensity, Mathf.Max(widenMaxFactor, 1f), 0f);
        }

        /// <summary>HLSL lerp. The GPU may round differently: tests compare with a tolerance.</summary>
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
