using UnityEngine;

namespace Scatter
{
    /// <summary>
    /// Settings of one quality tier (design 2: quality tier; design 12: targets).
    /// GPU buffers are sized from these values at startup.
    /// </summary>
    [CreateAssetMenu(menuName = "Scatter/Scatter Quality", fileName = "ScatterQuality")]
    public sealed class ScatterQuality : ScriptableObject
    {
        private const float MinRingWidthMeters = 1f;

        [Header("Content")]
        [SerializeField, Range(0f, 4f), Tooltip("Multiplier applied to the density of every layer (unitless).")]
        private float _densityMultiplier = 1f;

        [SerializeField, Tooltip("Outer distance of each ring, in meters, ascending (design 5.3). The last one is the scatter range.")]
        private float[] _ringOuterDistancesMeters = { 30f, 80f, 200f };

        [SerializeField, Tooltip("Highest shadow mode any layer may use on this tier (design 7).")]
        private ScatterShadowMode _maxShadowMode = ScatterShadowMode.All;

        [SerializeField, Min(1), Tooltip("Maximum number of chunk generations dispatched per frame (design 5.1).")]
        private int _generationBudgetChunksPerFrame = 4;

        [Header("Targets (design 12), shown in red in the HUD when exceeded")]
        [SerializeField, Min(1f), Tooltip("Whole-frame time target, 99th percentile, in milliseconds.")]
        private float _frameTimeTargetMs = 16.7f;

        [SerializeField, Min(0.1f), Tooltip("Scatter GPU time budget (generation + cull + all passes), in milliseconds.")]
        private float _scatterGpuBudgetMs = 2.5f;

        [SerializeField, Min(0.05f), Tooltip("Generation GPU time budget in any single frame, in milliseconds.")]
        private float _generationGpuBudgetMs = 0.4f;

        [SerializeField, Min(0.05f), Tooltip("Scatter CPU main-thread time budget, in milliseconds.")]
        private float _scatterCpuMainBudgetMs = 0.5f;

        public float DensityMultiplier => _densityMultiplier;
        public ScatterShadowMode MaxShadowMode => _maxShadowMode;
        public int GenerationBudgetChunksPerFrame => _generationBudgetChunksPerFrame;
        public float FrameTimeTargetMs => _frameTimeTargetMs;
        public float ScatterGpuBudgetMs => _scatterGpuBudgetMs;
        public float GenerationGpuBudgetMs => _generationGpuBudgetMs;
        public float ScatterCpuMainBudgetMs => _scatterCpuMainBudgetMs;

        public int RingCount => _ringOuterDistancesMeters.Length;

        /// <summary>Scatter range: outer distance of the last ring, in meters.</summary>
        public float ScatterRangeMeters => _ringOuterDistancesMeters[_ringOuterDistancesMeters.Length - 1];

        public float GetRingOuterDistanceMeters(int ring)
        {
            return _ringOuterDistancesMeters[ring];
        }

        /// <summary>
        /// Picks the tier for a Unity quality level; levels past the end of the list use the last tier.
        /// </summary>
        public static ScatterQuality ForQualityLevel(ScatterQuality[] tiers, int qualityLevel)
        {
            if (tiers == null || tiers.Length == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(qualityLevel, 0, tiers.Length - 1);
            return tiers[index];
        }

        private void OnValidate()
        {
            if (_ringOuterDistancesMeters == null || _ringOuterDistancesMeters.Length == 0)
            {
                _ringOuterDistancesMeters = new[] { 30f, 80f, 200f };
            }

            float previousMeters = 0f;
            for (int i = 0; i < _ringOuterDistancesMeters.Length; i++)
            {
                _ringOuterDistancesMeters[i] = Mathf.Max(_ringOuterDistancesMeters[i], previousMeters + MinRingWidthMeters);
                previousMeters = _ringOuterDistancesMeters[i];
            }
        }
    }
}
