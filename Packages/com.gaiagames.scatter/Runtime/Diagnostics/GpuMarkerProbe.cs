using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// Lists the profiler markers that have a GPU time in the running build (design 12.1): attaches a GPU recorder to
    /// every marker of the Render category or flagged for GPU sampling, records a number of frames, and reports the
    /// markers with GPU time and their average. Used to choose the marker names of <see cref="UrpPassTimings"/>, which
    /// differ between the Editor and players. Diagnostic tool: allocates when it starts and when it reports.
    /// </summary>
    public sealed class GpuMarkerProbe : IDisposable
    {
        private const double NanosecondsToMilliseconds = 1e-6;

        private readonly List<ProfilerRecorder> _recorders = new List<ProfilerRecorder>();
        private readonly List<string> _names = new List<string>();
        private readonly List<string> _categories = new List<string>();
        private double[] _totalNanoseconds;
        private int[] _framesWithSample;
        private int _frames;

        public int MarkerCount => _recorders.Count;

        public int FrameCount => _frames;

        /// <summary>Creates the recorders. Call once the frame has rendered at least once.</summary>
        public void Start()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (int i = 0; i < handles.Count; i++)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handles[i]);
                bool gpuFlag = (description.Flags & MarkerFlags.SampleGPU) != 0;
                if (description.Category.Name != ProfilerCategory.Render.Name && !gpuFlag)
                {
                    continue;
                }

                var recorder = new ProfilerRecorder(handles[i], 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.GpuRecorder);
                if (!recorder.Valid)
                {
                    recorder.Dispose();
                    continue;
                }

                recorder.Start();
                _recorders.Add(recorder);
                _names.Add(description.Name);
                _categories.Add(description.Category.Name);
            }

            _totalNanoseconds = new double[_recorders.Count];
            _framesWithSample = new int[_recorders.Count];
            _frames = 0;
        }

        /// <summary>Records the last GPU value of every marker. Call once per frame after <see cref="Start"/>.</summary>
        public void Sample()
        {
            _frames++;
            for (int i = 0; i < _recorders.Count; i++)
            {
                long value = _recorders[i].LastValue;
                if (value > 0)
                {
                    _totalNanoseconds[i] += value;
                    _framesWithSample[i]++;
                }
            }
        }

        /// <summary>The markers with GPU time, slowest first: name, category, average ms over the frames with a sample.</summary>
        public string Report()
        {
            var lines = new List<KeyValuePair<double, string>>();
            for (int i = 0; i < _recorders.Count; i++)
            {
                if (_framesWithSample[i] == 0)
                {
                    continue;
                }

                double averageMs = _totalNanoseconds[i] / _framesWithSample[i] * NanosecondsToMilliseconds;
                lines.Add(new KeyValuePair<double, string>(averageMs, string.Format(CultureInfo.InvariantCulture,
                    "{0,9:0.0000} ms  {1,4}/{2} frames  [{3}] {4}", averageMs, _framesWithSample[i], _frames, _categories[i], _names[i])));
            }

            lines.Sort((a, b) => b.Key.CompareTo(a.Key));
            var report = new StringBuilder();
            report.AppendFormat(CultureInfo.InvariantCulture, "GPU marker probe: {0} markers recorded, {1} with GPU time, {2} frames\n",
                _recorders.Count, lines.Count, _frames);
            for (int i = 0; i < lines.Count; i++)
            {
                report.Append(lines[i].Value).Append('\n');
            }

            return report.ToString();
        }

        /// <summary>Writes <see cref="Report"/> to a text file; returns its path.</summary>
        public string WriteReport(string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllText(path, Report());
            return path;
        }

        public void Dispose()
        {
            for (int i = 0; i < _recorders.Count; i++)
            {
                _recorders[i].Dispose();
            }

            _recorders.Clear();
        }
    }
}
