using System.Collections.Generic;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// The cases of the Phase 1 benchmark (S3), for each camera view: the baseline without scatter, every backend x
    /// blade draw type x blade count (procedural indexed with the default K), then the K sweep of the procedural
    /// indexed draw type for each backend.
    /// </summary>
    public static class SpikeBenchmarkPlan
    {
        private static readonly SpikeBackend[] s_Backends = { SpikeBackend.Brg, SpikeBackend.Indirect };

        private static readonly SpikeDrawType[] s_DrawTypes =
        {
            SpikeDrawType.MeshIndirect, SpikeDrawType.ProceduralIndirect, SpikeDrawType.ProceduralIndexedIndirect,
        };

        /// <summary>Allocates: startup only.</summary>
        public static SpikeBenchmarkCase[] Create(int viewCount, int[] bladeCounts, int defaultBladesPerInstance, int[] bladesPerInstanceSweep, int sweepBladeCount)
        {
            var cases = new List<SpikeBenchmarkCase>();
            for (int view = 0; view < viewCount; view++)
            {
                cases.Add(new SpikeBenchmarkCase { View = view, ScatterEnabled = false, BladesPerInstance = 1 });
                for (int b = 0; b < s_Backends.Length; b++)
                {
                    for (int d = 0; d < s_DrawTypes.Length; d++)
                    {
                        for (int c = 0; c < bladeCounts.Length; c++)
                        {
                            cases.Add(CreateCase(view, s_Backends[b], s_DrawTypes[d], bladeCounts[c], defaultBladesPerInstance));
                        }
                    }
                }

                for (int b = 0; b < s_Backends.Length; b++)
                {
                    for (int k = 0; k < bladesPerInstanceSweep.Length; k++)
                    {
                        if (bladesPerInstanceSweep[k] == defaultBladesPerInstance && Contains(bladeCounts, sweepBladeCount))
                        {
                            continue; // already measured above
                        }

                        cases.Add(CreateCase(view, s_Backends[b], SpikeDrawType.ProceduralIndexedIndirect, sweepBladeCount, bladesPerInstanceSweep[k]));
                    }
                }
            }

            return cases.ToArray();
        }

        private static SpikeBenchmarkCase CreateCase(int view, SpikeBackend backend, SpikeDrawType drawType, int bladeCount, int bladesPerInstance)
        {
            return new SpikeBenchmarkCase
            {
                View = view,
                ScatterEnabled = true,
                Backend = backend,
                DrawType = drawType,
                BladeCount = bladeCount,
                BladesPerInstance = drawType == SpikeDrawType.ProceduralIndexedIndirect ? bladesPerInstance : 1,
            };
        }

        private static bool Contains(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
