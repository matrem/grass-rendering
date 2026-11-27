using Scatter.Diagnostics;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Collects the per-frame values of one benchmark case and summarises them (median, 99th percentile).
    /// Buffers are allocated in the constructor; <see cref="Add"/> and <see cref="Summarize"/> do not allocate.
    /// </summary>
    public sealed class SpikeBenchmarkRecorder
    {
        private const float Median = 0.5f;
        private const float Percentile99 = 0.99f;

        private readonly FrameTimeHistory[] _values = new FrameTimeHistory[SpikeBenchmarkResult.MetricCount];

        public SpikeBenchmarkRecorder(int capacityFrames)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] = new FrameTimeHistory(capacityFrames);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i].Clear();
            }
        }

        /// <summary>Adds one frame value; NaN (not measured) is skipped.</summary>
        public void Add(SpikeBenchmarkMetric metric, float valueMs)
        {
            if (!float.IsNaN(valueMs))
            {
                _values[(int)metric].Add(valueMs);
            }
        }

        public void Summarize(SpikeBenchmarkResult result)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                FrameTimeHistory values = _values[i];
                result.SampleCount[i] = values.Count;
                result.MedianMs[i] = values.Count > 0 ? values.Percentile(Median) : float.NaN;
                result.Percentile99Ms[i] = values.Count > 0 ? values.Percentile(Percentile99) : float.NaN;
            }
        }
    }
}
