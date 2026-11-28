namespace Scatter.Rendering.Spike
{
    /// <summary>Summary of one benchmark case: median and 99th percentile of every <see cref="SpikeBenchmarkMetric"/>.</summary>
    public sealed class SpikeBenchmarkResult
    {
        public const int MetricCount = 13;

        public SpikeBenchmarkCase Case;

        /// <summary>Median per metric, in milliseconds; NaN when the metric had no sample.</summary>
        public readonly float[] MedianMs = new float[MetricCount];

        /// <summary>99th percentile per metric, in milliseconds; NaN when the metric had no sample.</summary>
        public readonly float[] Percentile99Ms = new float[MetricCount];

        /// <summary>Frames with a value per metric.</summary>
        public readonly int[] SampleCount = new int[MetricCount];

        /// <summary>Visible blades (LOD0 + LOD1 entries requested by the cull), from the debug readback.</summary>
        public uint VisibleBlades;

        public uint VisibleLod0;
        public uint VisibleLod1;

        /// <summary>URP render scale applied during the case; NaN without URP.</summary>
        public float RenderScale = float.NaN;

        /// <summary>Main light shadow map resolution applied during the case; 0 without URP.</summary>
        public int ShadowResolution;

        public uint VisibleObjects;

        /// <summary>Entries skipped by visible list overflow; must be 0.</summary>
        public uint Overflow;
    }
}
