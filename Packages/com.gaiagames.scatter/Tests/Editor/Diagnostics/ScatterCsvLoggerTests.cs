using System.Globalization;
using System.IO;
using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Diagnostics
{
    public sealed class ScatterCsvLoggerTests
    {
        [Test]
        public void WriteTo_WritesHeaderAndRows_WithNaNAsEmpty()
        {
            var logger = new ScatterCsvLogger(new[] { "frame", "gpuMs" }, 4);
            logger.BeginRow();
            logger.Set(0, 1);
            logger.Set(1, 2.5);
            logger.EndRow();
            logger.BeginRow();
            logger.Set(0, 2);
            logger.Set(1, double.NaN);
            logger.EndRow();

            var writer = new StringWriter { NewLine = "\n" };
            logger.WriteTo(writer);

            Assert.That(writer.ToString(), Is.EqualTo("frame,gpuMs\n1,2.5\n2,\n"));
        }

        [Test]
        public void WriteTo_WithCommaDecimalCulture_UsesDotDecimal()
        {
            var logger = new ScatterCsvLogger(new[] { "frame", "gpuMs" }, 1);
            logger.BeginRow();
            logger.Set(0, 1);
            logger.Set(1, 2.5);
            logger.EndRow();

            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            var writer = new StringWriter { NewLine = "\n" };
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                logger.WriteTo(writer);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }

            Assert.That(writer.ToString(), Is.EqualTo("frame,gpuMs\n1,2.5\n"));
        }

        [Test]
        public void BeginRow_WhenFull_DropsRowAndCountsIt()
        {
            var logger = new ScatterCsvLogger(new[] { "frame" }, 1);
            logger.BeginRow();
            logger.Set(0, 1);
            logger.EndRow();
            logger.BeginRow();
            logger.Set(0, 2);
            logger.EndRow();

            Assert.That(logger.RowCount, Is.EqualTo(1));
            Assert.That(logger.DroppedRows, Is.EqualTo(1));
        }

        [Test]
        public void Record_DoesNotAllocate()
        {
            var logger = new ScatterCsvLogger(FrameCsvColumns.CreateNames(), 16);
            var stats = new FrameStats { FrameTimeMs = 16f, GpuFrameMs = 10f };
            ScatterStageTimings timings = ScatterStageTimings.NotMeasured;
            FrameCsvColumns.Record(logger, 0, stats, timings);

            Assert.That(() => FrameCsvColumns.Record(logger, 1, stats, timings), Is.Not.AllocatingGCMemory());
        }
    }
}
