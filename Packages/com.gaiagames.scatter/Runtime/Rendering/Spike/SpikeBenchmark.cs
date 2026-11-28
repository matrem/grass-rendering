using System;
using System.Globalization;
using System.IO;
using Scatter.Diagnostics;
using UnityEngine;
#if SCATTER_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Spike benchmark (Phase 1 S3, Phase 1b) in the spike scene: runs the cases of a <see cref="SpikeBenchmarkMode"/>
    /// from fixed camera views and writes a CSV to persistentDataPath/ScatterLogs. Starts with a command line flag (then
    /// quits when done) or with F10 (full run). Render scale and shadow map resolution changes are undone at the end. Runs before <see cref="ScatterSpike"/> each frame, so the case settings and the
    /// camera view apply to the same frame's cull. If the player quits before the end, the finished cases are written.
    /// The work is done by <see cref="SpikeBenchmarkRunner"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SpikeBenchmark : MonoBehaviour
    {
        public const string FullFlag = "-spikeBenchmark";
        public const string QuickFlag = "-spikeBenchmarkQuick";

        /// <summary>Phase 1b item 1: render scale x shadow map resolution x blade count.</summary>
        public const string ResolutionFlag = "-spikeBenchmarkResolution";

        /// <summary>Phase 1b items 2 and 3: density falloff x widening x shadow caster preset.</summary>
        public const string LeversFlag = "-spikeBenchmarkLevers";

        /// <summary>Added to a benchmark flag: the Object layer (rocks) is off for the whole run (blades only). CSV name ends in _noobjects.</summary>
        public const string NoObjectsFlag = "-spikeBenchmarkNoObjects";

        /// <summary>Runs <see cref="GpuMarkerProbe"/> on the first view and quits: which GPU markers the build can time.</summary>
        public const string ProbeFlag = "-spikeTimingProbe";
        private const int ProbeWarmupFrames = 120;
        private const int ProbeMeasureFrames = 60;
        private const string CsvFolderName = "ScatterLogs";
        private const int QuickBladeCount = 1 << 20;
        private const int QuickWarmupFrames = 30;
        private const int QuickMeasureFrames = 60;

        [SerializeField, Tooltip("Spike driver whose system is measured.")]
        private ScatterSpike _spike;

        [SerializeField, Tooltip("Debug HUD, hidden during the run (optional).")]
        private ScatterDebugHud _hud;

        [Header("Views")]
        [SerializeField, Tooltip("Camera positions of the views (meters).")]
        private Vector3[] _viewPositions = { new Vector3(0f, 1.8f, -60f), new Vector3(0f, 10f, -70f) };

        [SerializeField, Tooltip("Camera rotations of the views (Euler angles, degrees), same order as the positions.")]
        private Vector3[] _viewEulerAngles = { new Vector3(10f, 0f, 0f), new Vector3(25f, 0f, 0f) };

        [Header("Cases")]
        [SerializeField, Tooltip("Blade counts measured for every backend and draw type (full run).")]
        private int[] _bladeCounts = { 1 << 20, 1 << 21, 1 << 22 };

        [SerializeField, Range(1, SpikeCuller.MaxBladesPerInstance), Tooltip("K of the procedural indexed draws outside the K sweep.")]
        private int _defaultBladesPerInstance = 16;

        [SerializeField, Tooltip("K values of the sweep (procedural indexed draws, full run).")]
        private int[] _bladesPerInstanceSweep = { 2, 4, 8, 16, 32, 64 };

        [SerializeField, Min(1), Tooltip("Blade count of the K sweep.")]
        private int _sweepBladeCount = 1 << 22;

        [Header("Phase 1b cases")]
        [SerializeField, Tooltip("Blade counts of the resolution run (procedural indexed, BRG).")]
        private int[] _resolutionBladeCounts = { 1 << 20, 1 << 22 };

        [SerializeField, Tooltip("URP render scales of the resolution run.")]
        private float[] _renderScales = { 1f, 0.5f, 0.25f };

        [SerializeField, Tooltip("Main light shadow map resolutions of the resolution run (texels per side).")]
        private int[] _shadowResolutions = { 2048, 512 };

        [SerializeField, Min(1), Tooltip("Blade count of the levers run (density falloff, shadow casters).")]
        private int _leversBladeCount = 1 << 22;

        [Header("Frames")]
        [SerializeField, Min(0), Tooltip("Frames ignored after each case change (GPU timings arrive a few frames late).")]
        private int _warmupFrames = 60;

        [SerializeField, Min(1), Tooltip("Frames measured per case.")]
        private int _measureFrames = 300;

#if SCATTER_INPUT_SYSTEM
        [SerializeField, Tooltip("Key starting the full benchmark.")]
        private Key _startKey = Key.F10;
#endif

        private FrameStatsSampler _sampler;
        private SpikeBenchmarkRunner _runner;
        private SpikeBenchmarkMode _mode;
        private SpikeUrpSettings _urpSettings;
        private bool _objectsEnabled = true;
        private bool _objectsEnabledBefore;
        private bool _quitWhenDone;
        private bool _probing;
        private int _probeStartFrame;
        private GpuMarkerProbe _probe;

        public bool IsRunning => _runner != null;

        /// <summary>Path of the last CSV written, or null.</summary>
        public string LastCsvPath { get; private set; }

        /// <summary>Starts a run. Allocates the plan and the results.</summary>
        public void StartBenchmark(SpikeBenchmarkMode mode)
        {
            if (IsRunning || _spike == null || _spike.System == null)
            {
                return;
            }

            var views = new Pose[_viewPositions.Length];
            for (int i = 0; i < views.Length; i++)
            {
                Vector3 euler = i < _viewEulerAngles.Length ? _viewEulerAngles[i] : Vector3.zero;
                views[i] = new Pose(_viewPositions[i], Quaternion.Euler(euler));
            }

            SpikeBenchmarkCase[] cases;
            int warmupFrames = _warmupFrames;
            int measureFrames = _measureFrames;
            switch (mode)
            {
                case SpikeBenchmarkMode.Quick:
                    cases = SpikeBenchmarkPlan.Create(views.Length, new[] { QuickBladeCount }, _defaultBladesPerInstance, new[] { _defaultBladesPerInstance }, QuickBladeCount);
                    warmupFrames = QuickWarmupFrames;
                    measureFrames = QuickMeasureFrames;
                    break;
                case SpikeBenchmarkMode.Resolution:
                    cases = SpikeBenchmarkPlan.CreateResolution(views.Length, _resolutionBladeCounts, _defaultBladesPerInstance, _renderScales, _shadowResolutions);
                    break;
                case SpikeBenchmarkMode.Levers:
                    cases = SpikeBenchmarkPlan.CreateLevers(views.Length, _leversBladeCount, _defaultBladesPerInstance);
                    break;
                default:
                    cases = SpikeBenchmarkPlan.Create(views.Length, _bladeCounts, _defaultBladesPerInstance, _bladesPerInstanceSweep, _sweepBladeCount);
                    break;
            }

            _mode = mode;
            _urpSettings = SpikeUrpSettings.ForActivePipeline();
            _objectsEnabledBefore = _spike.System.Config.ObjectsEnabled;
            _runner = new SpikeBenchmarkRunner(_spike.System, _spike.CullCamera.transform, cases, views, warmupFrames, measureFrames, _urpSettings, _objectsEnabled);
            _sampler = new FrameStatsSampler(measureFrames);
            _spike.ForceStatsReadback = true;
            if (_hud != null)
            {
                _hud.Visible = false;
            }

            Debug.Log($"Spike benchmark {mode}: {cases.Length} cases, {warmupFrames} + {measureFrames} frames each, {Screen.width}x{Screen.height}, objects {(_objectsEnabled ? "on" : "off")}.");
        }

        private void Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            _objectsEnabled = Array.IndexOf(arguments, NoObjectsFlag) < 0;
            if (Array.IndexOf(arguments, ProbeFlag) >= 0)
            {
                // GPU marker probe (GpuMarkerProbe): first view, default settings, then quits.
                _probing = true;
                _probeStartFrame = Time.frameCount + ProbeWarmupFrames;
                if (_hud != null)
                {
                    _hud.Visible = false;
                }
            }
            else if (Array.IndexOf(arguments, QuickFlag) >= 0)
            {
                _quitWhenDone = true;
                StartBenchmark(SpikeBenchmarkMode.Quick);
            }
            else if (Array.IndexOf(arguments, FullFlag) >= 0)
            {
                _quitWhenDone = true;
                StartBenchmark(SpikeBenchmarkMode.Full);
            }
            else if (Array.IndexOf(arguments, ResolutionFlag) >= 0)
            {
                _quitWhenDone = true;
                StartBenchmark(SpikeBenchmarkMode.Resolution);
            }
            else if (Array.IndexOf(arguments, LeversFlag) >= 0)
            {
                _quitWhenDone = true;
                StartBenchmark(SpikeBenchmarkMode.Levers);
            }
        }

        private void OnDisable()
        {
            RestoreUrpSettings();
            _probe?.Dispose();
            _probe = null;
            _sampler?.Dispose();
            _sampler = null;
            _runner = null;
        }

        private void LateUpdate()
        {
#if SCATTER_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (!IsRunning && keyboard != null && keyboard[_startKey].wasPressedThisFrame)
            {
                StartBenchmark(SpikeBenchmarkMode.Full);
            }
#endif
            if (_probing)
            {
                UpdateProbe();
                return;
            }

            if (!IsRunning)
            {
                return;
            }

            _sampler.Sample();
            _runner.Step(_sampler.Latest, _spike.Timings);
            if (_runner.IsDone)
            {
                Finish();
            }
        }

        private void UpdateProbe()
        {
            if (_viewPositions.Length > 0)
            {
                Vector3 euler = _viewEulerAngles.Length > 0 ? _viewEulerAngles[0] : Vector3.zero;
                _spike.CullCamera.transform.SetPositionAndRotation(_viewPositions[0], Quaternion.Euler(euler));
            }

            if (_probe == null)
            {
                if (Time.frameCount >= _probeStartFrame)
                {
                    _probe = new GpuMarkerProbe();
                    _probe.Start();
                }

                return;
            }

            _probe.Sample();
            if (_probe.FrameCount < ProbeMeasureFrames)
            {
                return;
            }

            string fileName = "gpu_markers_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt";
            string path = _probe.WriteReport(Path.Combine(Application.persistentDataPath, CsvFolderName), fileName);
            Debug.Log(_probe.Report() + "GPU marker probe written: " + path);
            _probe.Dispose();
            _probe = null;
            _probing = false;
            Application.Quit();
        }

        /// <summary>Writes the first <paramref name="caseCount"/> results. Allocates: end of a run only.</summary>
        private void WriteCsv(int caseCount, string suffix)
        {
            string fileName = "spike_benchmark_" + _mode.ToString().ToLowerInvariant() + (_objectsEnabled ? string.Empty : "_noobjects") + suffix + "_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
            string folder = Path.Combine(Application.persistentDataPath, CsvFolderName);
            Directory.CreateDirectory(folder);
            LastCsvPath = Path.Combine(folder, fileName);
            using (var writer = new StreamWriter(LastCsvPath))
            {
                SpikeBenchmarkCsv.Write(writer, _runner.Results, caseCount, SystemInfo.graphicsDeviceName,
                    SystemInfo.graphicsDeviceType.ToString(), Screen.width, Screen.height);
            }

            Debug.Log("Spike benchmark written: " + LastCsvPath);
        }

        private void OnApplicationQuit()
        {
            // Stopped before the end (window closed): keep the finished cases.
            if (IsRunning && _runner.CaseIndex > 0)
            {
                WriteCsv(Mathf.Min(_runner.CaseIndex, _runner.CaseCount), "_partial");
            }

            RestoreUrpSettings();
        }

        /// <summary>In the Editor the runs change the project URP asset itself: always put its values back.</summary>
        private void RestoreUrpSettings()
        {
            _urpSettings?.Restore();
            _urpSettings = null;
        }

        private void Finish()
        {
            RestoreUrpSettings();
            WriteCsv(_runner.CaseCount, string.Empty);
            _runner = null;
            _spike.System.SetObjectsEnabled(_objectsEnabledBefore);
            _sampler.Dispose();
            _sampler = null;
            _spike.ForceStatsReadback = false;
            if (_hud != null)
            {
                _hud.Visible = true;
            }

            if (_quitWhenDone)
            {
                Application.Quit();
            }
        }
    }
}
