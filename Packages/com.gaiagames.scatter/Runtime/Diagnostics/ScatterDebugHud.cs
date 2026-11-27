using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
#if SCATTER_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Scatter.Diagnostics
{
    /// <summary>
    /// On-screen debug HUD (design 12.1) and CSV recording control. Thin component: sampling, text and logging
    /// are done by <see cref="FrameStatsSampler"/>, <see cref="ScatterHudText"/> and <see cref="ScatterCsvLogger"/>.
    /// The text is rebuilt at a low rate and only while visible; sampling and recording never allocate.
    /// The label is drawn by the <see cref="PanelRenderer"/> of the same GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class ScatterDebugHud : MonoBehaviour
    {
        private const string CsvFolderName = "ScatterLogs";
        private const float HudFontSize = 13f;
        private const float HudMarginPixels = 8f;
        private const float HudPaddingPixels = 6f;

        [SerializeField, Tooltip("Scatter quality per Unity quality level (index = level). Gives the targets shown in red.")]
        private ScatterQuality[] _qualityPerLevel = Array.Empty<ScatterQuality>();

        [SerializeField, Min(30), Tooltip("Frames kept for the average, 99th percentile and worst frame (frames).")]
        private int _historyFrames = 300;

        [SerializeField, Range(1f, 30f), Tooltip("HUD text refresh rate, in hertz.")]
        private float _refreshRateHz = 4f;

        [SerializeField, Tooltip("Show the HUD when the component is enabled.")]
        private bool _visibleOnStart = true;

        [SerializeField, Min(60), Tooltip("CSV recording capacity, in frames (36000 = 10 minutes at 60 FPS).")]
        private int _csvCapacityFrames = 36000;

#if SCATTER_INPUT_SYSTEM
        [SerializeField, Tooltip("Key that shows or hides the HUD.")]
        private Key _toggleKey = Key.F1;

        [SerializeField, Tooltip("Key that starts or stops CSV recording.")]
        private Key _recordKey = Key.F2;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField, Tooltip("Key that shows or hides the HUD.")]
        private KeyCode _toggleKey = KeyCode.F1;

        [SerializeField, Tooltip("Key that starts or stops CSV recording.")]
        private KeyCode _recordKey = KeyCode.F2;
#endif

        private PanelRenderer _panelRenderer;
        private PanelRenderer.VersionedUIReloadCallback _onUIReload;
        private Label _label;
        private FrameStatsSampler _sampler;
        private ScatterHudText _text;
        private ScatterCsvLogger _csvLogger;
        private string[] _qualityNames;
        private string _header;
        private ScatterStageTimings _stageTimings;
        private bool _visible;
        private bool _recording;
        private float _nextRefreshTime;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                _nextRefreshTime = 0f;
                if (_label != null)
                {
                    _label.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        public bool IsRecording => _recording;

        /// <summary>Whole-frame values; null while the component is disabled.</summary>
        public FrameStatsSampler Sampler => _sampler;

        /// <summary>Scatter timings to show and record, set every frame by the scatter driver.</summary>
        public ScatterStageTimings StageTimings
        {
            get => _stageTimings;
            set => _stageTimings = value;
        }

        /// <summary>Path of the last CSV written by <see cref="StopRecording"/>, or null.</summary>
        public string LastCsvPath { get; private set; }

        public void StartRecording()
        {
            _csvLogger.Clear();
            _recording = true;
        }

        /// <summary>Stops recording and writes the CSV under persistentDataPath. Allocates (end of a run).</summary>
        public void StopRecording()
        {
            if (!_recording)
            {
                return;
            }

            _recording = false;
            string fileName = "frames_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
            LastCsvPath = Path.Combine(Application.persistentDataPath, CsvFolderName, fileName);
            _csvLogger.WriteToFile(LastCsvPath);
            Debug.Log($"Scatter CSV written: {LastCsvPath} ({_csvLogger.RowCount} frames, {_csvLogger.DroppedRows} dropped)");
        }

        private void OnEnable()
        {
            _sampler = new FrameStatsSampler(_historyFrames);
            _text = new ScatterHudText();
            _csvLogger = new ScatterCsvLogger(FrameCsvColumns.CreateNames(), _csvCapacityFrames);
            _qualityNames = QualitySettings.names;
            _header = BuildHeader();
            _stageTimings = ScatterStageTimings.NotMeasured;
            _visible = _visibleOnStart;
            _nextRefreshTime = 0f;
            _label ??= CreateLabel();
            _label.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;

            // Invoked at once if the panel is already loaded, then on every UI reload.
            _panelRenderer = GetComponent<PanelRenderer>();
            _onUIReload ??= OnUIReload;
            _panelRenderer.RegisterUIReloadCallback(_onUIReload);
        }

        private void OnDisable()
        {
            StopRecording();
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(_onUIReload);
            }

            _label.RemoveFromHierarchy();
            _sampler?.Dispose();
            _sampler = null;
        }

        private void Update()
        {
            HandleKeys();
            _sampler.Sample();
            if (_recording)
            {
                FrameCsvColumns.Record(_csvLogger, Time.frameCount, _sampler.Latest, _stageTimings);
            }

            if (_visible && Time.unscaledTime >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.unscaledTime + 1f / _refreshRateHz;
                RefreshText();
            }
        }

        private void RefreshText()
        {
            if (_label.panel == null)
            {
                return;
            }

            int level = QualitySettings.GetQualityLevel();
            string tierName = level >= 0 && level < _qualityNames.Length ? _qualityNames[level] : "?";
            ScatterQuality quality = ScatterQuality.ForQualityLevel(_qualityPerLevel, level);
            _label.text = _text.Build(_header, _sampler.Latest, _sampler.History, _stageTimings, quality, tierName, _recording ? _csvLogger : null);
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            // A reload can rebuild or clear the root: attach the label again when it is no longer a child.
            if (_label.parent == root)
            {
                return;
            }

            root.Add(_label);
            _nextRefreshTime = 0f;
        }

        private static Label CreateLabel()
        {
            var label = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
            IStyle style = label.style;
            style.position = Position.Absolute;
            style.left = HudMarginPixels;
            style.top = HudMarginPixels;
            style.paddingLeft = HudPaddingPixels;
            style.paddingRight = HudPaddingPixels;
            style.paddingTop = HudPaddingPixels;
            style.paddingBottom = HudPaddingPixels;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            style.color = Color.white;
            style.fontSize = HudFontSize;
            style.whiteSpace = WhiteSpace.Pre;
            return label;
        }

        private string BuildHeader()
        {
#if SCATTER_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            return "SCATTER  [" + _toggleKey + "] hide  [" + _recordKey + "] record CSV";
#else
            return "SCATTER";
#endif
        }

        private void HandleKeys()
        {
#if SCATTER_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool togglePressed = keyboard[_toggleKey].wasPressedThisFrame;
            bool recordPressed = keyboard[_recordKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool togglePressed = Input.GetKeyDown(_toggleKey);
            bool recordPressed = Input.GetKeyDown(_recordKey);
#else
            bool togglePressed = false;
            bool recordPressed = false;
#endif
            if (togglePressed)
            {
                Visible = !_visible;
            }

            if (recordPressed)
            {
                if (_recording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }
        }
    }
}
