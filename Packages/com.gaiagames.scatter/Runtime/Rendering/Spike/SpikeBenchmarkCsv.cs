using System;
using System.Globalization;
using System.IO;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Writes the benchmark results as CSV: one row per case, median and 99th percentile of every metric, and the
    /// environment (GPU, graphics API, resolution). Dot decimals whatever the culture. Allocates: end of the run only.
    /// </summary>
    public static class SpikeBenchmarkCsv
    {
        public static void Write(TextWriter writer, SpikeBenchmarkResult[] results, int count, string gpu, string graphicsApi, int width, int height)
        {
            writer.Write("view,scatter,backend,drawType,bladeCount,K,renderScale,shadowResolution,densityFalloff,shadowCasters,"
                + "visibleBlades,visibleLod0,visibleLod1,visibleObjects,overflow");
            string[] metrics = Enum.GetNames(typeof(SpikeBenchmarkMetric));
            for (int m = 0; m < metrics.Length; m++)
            {
                writer.Write("," + ToCamelCase(metrics[m]) + "MedianMs," + ToCamelCase(metrics[m]) + "P99Ms");
            }

            writer.WriteLine(",frames,gpu,graphicsApi,width,height");
            for (int i = 0; i < count; i++)
            {
                SpikeBenchmarkResult result = results[i];
                SpikeBenchmarkCase benchmarkCase = result.Case;
                writer.Write(Format(benchmarkCase.View));
                writer.Write(benchmarkCase.ScatterEnabled ? ",on," : ",off,");
                writer.Write(benchmarkCase.ScatterEnabled ? benchmarkCase.Backend.ToString() : string.Empty);
                writer.Write(',');
                writer.Write(benchmarkCase.ScatterEnabled ? benchmarkCase.DrawType.ToString() : string.Empty);
                writer.Write("," + Format(benchmarkCase.ScatterEnabled ? benchmarkCase.BladeCount : 0));
                writer.Write("," + Format(benchmarkCase.BladesPerInstance));
                writer.Write("," + Format(result.RenderScale) + "," + (result.ShadowResolution > 0 ? Format(result.ShadowResolution) : string.Empty));
                writer.Write("," + (benchmarkCase.ScatterEnabled ? benchmarkCase.DensityFalloff.ToString() : string.Empty));
                writer.Write("," + (benchmarkCase.ScatterEnabled ? benchmarkCase.ShadowCasters.ToString() : string.Empty));
                writer.Write("," + Format(result.VisibleBlades) + "," + Format(result.VisibleLod0) + "," + Format(result.VisibleLod1));
                writer.Write("," + Format(result.VisibleObjects) + "," + Format(result.Overflow));
                for (int m = 0; m < SpikeBenchmarkResult.MetricCount; m++)
                {
                    writer.Write("," + Format(result.MedianMs[m]) + "," + Format(result.Percentile99Ms[m]));
                }

                writer.Write("," + Format(result.SampleCount[(int)SpikeBenchmarkMetric.FrameGpu]));
                writer.WriteLine(",\"" + gpu + "\"," + graphicsApi + "," + Format(width) + "," + Format(height));
            }
        }

        private static string ToCamelCase(string name)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string Format(float value)
        {
            return float.IsNaN(value) ? string.Empty : value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Format(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
