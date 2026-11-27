using UnityEngine;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// Ring buffer of the last frame times, with average, percentile and worst-frame queries.
    /// No query allocates (design 12: frame rates are judged on the 99th percentile).
    /// </summary>
    public sealed class FrameTimeHistory
    {
        private readonly float[] _samplesMs;
        private readonly float[] _scratchMs;
        private int _count;
        private int _next;

        public FrameTimeHistory(int capacityFrames)
        {
            Debug.Assert(capacityFrames > 0, "FrameTimeHistory needs a positive capacity.");
            _samplesMs = new float[capacityFrames];
            _scratchMs = new float[capacityFrames];
        }

        public int Count => _count;
        public int CapacityFrames => _samplesMs.Length;

        public void Add(float frameTimeMs)
        {
            _samplesMs[_next] = frameTimeMs;
            _next = (_next + 1) % _samplesMs.Length;
            if (_count < _samplesMs.Length)
            {
                _count++;
            }
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
        }

        public float AverageMs()
        {
            if (_count == 0)
            {
                return 0f;
            }

            float sumMs = 0f;
            for (int i = 0; i < _count; i++)
            {
                sumMs += _samplesMs[i];
            }

            return sumMs / _count;
        }

        public float WorstMs()
        {
            float worstMs = 0f;
            for (int i = 0; i < _count; i++)
            {
                worstMs = Mathf.Max(worstMs, _samplesMs[i]);
            }

            return worstMs;
        }

        /// <summary>
        /// Nearest-rank percentile: the smallest sample such that at least <paramref name="fraction"/>
        /// of the samples are lower or equal. 0.99 gives the 99th percentile.
        /// </summary>
        public float Percentile(float fraction)
        {
            if (_count == 0)
            {
                return 0f;
            }

            int rank = Mathf.CeilToInt(Mathf.Clamp01(fraction) * _count);
            int index = Mathf.Clamp(rank - 1, 0, _count - 1);
            System.Array.Copy(_samplesMs, _scratchMs, _count);
            return SelectKthSmallest(_scratchMs, _count, index);
        }

        // Hoare quickselect: partially reorders values, returns the k-th smallest (k is 0-based).
        private static float SelectKthSmallest(float[] values, int count, int k)
        {
            int left = 0;
            int right = count - 1;
            while (left < right)
            {
                float pivot = values[(left + right) >> 1];
                int i = left;
                int j = right;
                while (i <= j)
                {
                    while (values[i] < pivot)
                    {
                        i++;
                    }

                    while (values[j] > pivot)
                    {
                        j--;
                    }

                    if (i <= j)
                    {
                        float swap = values[i];
                        values[i] = values[j];
                        values[j] = swap;
                        i++;
                        j--;
                    }
                }

                if (k <= j)
                {
                    right = j;
                }
                else if (k >= i)
                {
                    left = i;
                }
                else
                {
                    return values[k];
                }
            }

            return values[k];
        }
    }
}
