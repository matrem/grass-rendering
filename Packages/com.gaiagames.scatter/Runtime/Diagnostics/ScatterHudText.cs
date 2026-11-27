using System.Globalization;
using System.Text;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// Builds the HUD text (design 12.1). Allocates the result string: call at the HUD refresh rate only,
    /// and only while the HUD is visible.
    /// </summary>
    public sealed class ScatterHudText
    {
        private const string Red = "<color=#FF5050>";
        private const string EndColour = "</color>";
        private const string NotAvailable = "-";
        private const float Percentile99 = 0.99f;

        private readonly StringBuilder _builder = new StringBuilder(1024);

        public string Build(string header, in FrameStats stats, FrameTimeHistory history, in ScatterStageTimings timings, ScatterQuality quality, string tierName, ScatterCsvLogger recordingLogger)
        {
            float frameTargetMs = quality != null ? quality.FrameTimeTargetMs : 0f;
            float averageMs = history.AverageMs();

            _builder.Clear();
            _builder.Append(header).Append('\n');
            _builder.Append("Tier ").Append(tierName);
            if (quality != null)
            {
                _builder.Append("  (frame target ");
                AppendNumber(frameTargetMs).Append(" ms)");
            }

            _builder.Append("\n\nFrame\n  FPS ");
            AppendNumber(averageMs > 0f ? 1000f / averageMs : 0f);
            AppendMs("   frame ", averageMs, frameTargetMs);
            AppendMs("   p99 ", history.Percentile(Percentile99), frameTargetMs);
            AppendMs("   worst ", history.WorstMs(), frameTargetMs);
            AppendMs("\n  CPU ", stats.CpuFrameMs, frameTargetMs);
            AppendMs(" (main ", stats.CpuMainThreadMs, 0f);
            AppendMs(", render ", stats.CpuRenderThreadMs, 0f);
            AppendMs(")   GPU ", stats.GpuFrameMs, frameTargetMs);
            AppendGcAllocated(stats.GcAllocatedInFrameBytes);

            _builder.Append("\n\nScatter timings");
            AppendMs("\n  CPU main ", timings.CpuMainMs, quality != null ? quality.ScatterCpuMainBudgetMs : 0f);
            AppendMs("   generation ", timings.GenerationGpuMs, quality != null ? quality.GenerationGpuBudgetMs : 0f);
            AppendMs("   cull ", timings.CullGpuMs, 0f);
            AppendMs("\n  depth ", timings.DepthGpuMs, 0f);
            AppendMs("   colour ", timings.ColourGpuMs, 0f);
            AppendMs("   shadows ", timings.ShadowsGpuMs, 0f);
            AppendMs("   motion vectors ", timings.MotionVectorsGpuMs, 0f);

            if (recordingLogger != null)
            {
                _builder.Append("\n\nCSV recording ").Append(recordingLogger.RowCount).Append(" / ").Append(recordingLogger.CapacityRows).Append(" frames");
                if (recordingLogger.DroppedRows > 0)
                {
                    _builder.Append(Red).Append(", ").Append(recordingLogger.DroppedRows).Append(" dropped").Append(EndColour);
                }
            }

            return _builder.ToString();
        }

        private void AppendMs(string label, float valueMs, float limitMs)
        {
            _builder.Append(label);
            if (float.IsNaN(valueMs))
            {
                _builder.Append(NotAvailable);
                return;
            }

            bool overLimit = limitMs > 0f && valueMs > limitMs;
            if (overLimit)
            {
                _builder.Append(Red);
            }

            AppendNumber(valueMs).Append(" ms");
            if (overLimit)
            {
                _builder.Append(EndColour);
            }
        }

        private void AppendGcAllocated(long bytes)
        {
            _builder.Append("\n  GC alloc in frame ");
            if (bytes < 0)
            {
                _builder.Append(NotAvailable);
                return;
            }

            if (bytes > 0)
            {
                _builder.Append(Red).Append(bytes).Append(" B").Append(EndColour);
                return;
            }

            _builder.Append("0 B");
        }

        private StringBuilder AppendNumber(float value)
        {
            return _builder.Append(value.ToString("F2", CultureInfo.InvariantCulture));
        }
    }
}
