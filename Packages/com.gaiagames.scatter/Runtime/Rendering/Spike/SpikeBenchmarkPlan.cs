using System.Collections.Generic;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// The cases of the spike benchmark. <see cref="Create"/> (Phase 1 S3), for each camera view: the baseline without
    /// scatter, every backend x blade draw type x blade count (procedural indexed with the default K), then the K sweep
    /// of the procedural indexed draw type for each backend. <see cref="CreateResolution"/> and <see cref="CreateLevers"/>:
    /// Phase 1b, BRG and procedural indexed only.
    /// </summary>
    public static class SpikeBenchmarkPlan
    {
        private static readonly SpikeDensityFalloff[] s_Falloffs = { SpikeDensityFalloff.Off, SpikeDensityFalloff.On, SpikeDensityFalloff.OnWiden };

        /// <summary>
        /// Phase 1b item 1, for each view, render scale and shadow map resolution: the baseline without scatter, then
        /// BRG procedural indexed at each blade count. Allocates: startup only.
        /// </summary>
        public static SpikeBenchmarkCase[] CreateResolution(int viewCount, int[] bladeCounts, int bladesPerInstance, float[] renderScales, int[] shadowResolutions)
        {
            var cases = new List<SpikeBenchmarkCase>();
            for (int view = 0; view < viewCount; view++)
            {
                for (int s = 0; s < renderScales.Length; s++)
                {
                    for (int r = 0; r < shadowResolutions.Length; r++)
                    {
                        var baseline = new SpikeBenchmarkCase { View = view, ScatterEnabled = false, BladesPerInstance = 1 };
                        baseline.RenderScale = renderScales[s];
                        baseline.ShadowResolution = shadowResolutions[r];
                        cases.Add(baseline);
                        for (int c = 0; c < bladeCounts.Length; c++)
                        {
                            SpikeBenchmarkCase benchmarkCase = CreateCase(view, SpikeBackend.Brg, SpikeDrawType.ProceduralIndexedIndirect, bladeCounts[c], bladesPerInstance);
                            benchmarkCase.RenderScale = renderScales[s];
                            benchmarkCase.ShadowResolution = shadowResolutions[r];
                            cases.Add(benchmarkCase);
                        }
                    }
                }
            }

            return cases.ToArray();
        }

        /// <summary>
        /// Phase 1b items 2 and 3, for each view: the baseline without scatter, then BRG procedural indexed with every
        /// density falloff (off, on, on with widening) x every shadow caster preset. Allocates: startup only.
        /// </summary>
        public static SpikeBenchmarkCase[] CreateLevers(int viewCount, int bladeCount, int bladesPerInstance)
        {
            var cases = new List<SpikeBenchmarkCase>();
            for (int view = 0; view < viewCount; view++)
            {
                cases.Add(new SpikeBenchmarkCase { View = view, ScatterEnabled = false, BladesPerInstance = 1 });
                for (int f = 0; f < s_Falloffs.Length; f++)
                {
                    for (int casters = 0; casters < SpikeShadowCasterMask.PresetCount; casters++)
                    {
                        SpikeBenchmarkCase benchmarkCase = CreateCase(view, SpikeBackend.Brg, SpikeDrawType.ProceduralIndexedIndirect, bladeCount, bladesPerInstance);
                        benchmarkCase.DensityFalloff = s_Falloffs[f];
                        benchmarkCase.ShadowCasters = (SpikeShadowCasters)casters;
                        cases.Add(benchmarkCase);
                    }
                }
            }

            return cases.ToArray();
        }

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
