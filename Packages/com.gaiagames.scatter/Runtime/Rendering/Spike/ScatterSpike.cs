using System.Text;
using Scatter.Diagnostics;
using UnityEngine;
#if SCATTER_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Scene driver of the Phase 1 spike: holds the settings, handles the keys and feeds the debug HUD.
    /// The work is done by <see cref="ScatterSpikeSystem"/>.
    /// Keys: F3 blade draw type, F4 region offset mode, F5 blade count, F6 K, F7 objects, F8 debug view (SpikeDebugView), F9 backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScatterSpike : MonoBehaviour
    {
        private const float StatsRefreshSeconds = 0.25f;

        private static readonly int s_DebugView = Shader.PropertyToID("_ScatterDebugView");
        private static readonly int[] s_BladesPerInstanceSteps = { 2, 4, 8, 16, 32, 64 };

        [SerializeField, Tooltip("Shaders of the spike. Empty: the asset of the package Resources folder.")]
        private ScatterSpikeResources _resources;

        [SerializeField, Tooltip("Camera the cull runs for. Empty: Camera.main at enable.")]
        private Camera _cullCamera;

        [SerializeField, Tooltip("Debug HUD receiving the timings and the spike state (optional).")]
        private ScatterDebugHud _hud;

        [Header("Content")]
        [SerializeField, Tooltip("Blade counts cycled with F5 (blades). The largest sizes the GPU buffers.")]
        private int[] _bladeCountSteps = { 1 << 18, 1 << 20, 1 << 21, 1 << 22 };

        [SerializeField, Min(0), Tooltip("Index in the blade count steps at start.")]
        private int _initialBladeCountStep = 1;

        [SerializeField, Min(1), Tooltip("Number of Object layer instances (small rocks).")]
        private int _objectCount = 1 << 14;

        [SerializeField, Min(1f), Tooltip("Side of the square area covered by blades and objects, centred on the origin (meters).")]
        private float _areaSizeMeters = 150f;

        [SerializeField, Min(0.01f), Tooltip("Blade height (meters).")]
        private float _bladeHeightMeters = 0.45f;

        [SerializeField, Min(0.001f), Tooltip("Blade width at the root (meters).")]
        private float _bladeWidthMeters = 0.04f;

        [SerializeField, Range(0f, 1f), Tooltip("Tip offset along the facing direction, fraction of the height.")]
        private float _bladeBend = 0.25f;

        [Header("Cull")]
        [SerializeField, Min(1f), Tooltip("Distance where blades switch from LOD0 (7 vertices) to LOD1 (5 vertices), in meters.")]
        private float _lod1DistanceMeters = 20f;

        [SerializeField, Min(1f), Tooltip("Distance beyond which nothing is drawn (meters).")]
        private float _maxDistanceMeters = 120f;

        [Header("Draw")]
        [SerializeField, Tooltip("Blade draw command type at start (F3 cycles).")]
        private SpikeDrawType _bladeDrawType = SpikeDrawType.MeshIndirect;

        [SerializeField, Tooltip("How BRG commands point at their region of the visible list (F4 cycles).")]
        private SpikeRegionOffsetMode _offsetMode = SpikeRegionOffsetMode.VisibleOffset;

        [SerializeField, Range(1, SpikeCuller.MaxBladesPerInstance), Tooltip("K: blades per instance of procedural indexed draws (F6 cycles).")]
        private int _bladesPerInstance = 16;

        [SerializeField, Tooltip("Draw backend at start (F9 switches): BRG or the Graphics.Render*Indirect baseline.")]
        private SpikeBackend _backend = SpikeBackend.Brg;

#if SCATTER_INPUT_SYSTEM
        [SerializeField, Tooltip("Key cycling the blade draw type.")]
        private Key _drawTypeKey = Key.F3;

        [SerializeField, Tooltip("Key cycling the region offset mode.")]
        private Key _offsetModeKey = Key.F4;

        [SerializeField, Tooltip("Key cycling the blade count.")]
        private Key _bladeCountKey = Key.F5;

        [SerializeField, Tooltip("Key cycling K.")]
        private Key _bladesPerInstanceKey = Key.F6;

        [SerializeField, Tooltip("Key toggling the Object layer.")]
        private Key _objectsKey = Key.F7;

        [SerializeField, Tooltip("Key cycling the debug views: off, colour per draw, main light only, ambient only.")]
        private Key _debugViewKey = Key.F8;

        [SerializeField, Tooltip("Key switching the draw backend.")]
        private Key _backendKey = Key.F9;
#endif

        private readonly StringBuilder _status = new StringBuilder(256);
        private ScatterSpikeSystem _system;
        private int _bladeCountStep;
        private SpikeDebugView _debugView;
        private float _nextStatsTime;
        private UrpPassTimings _passTimings;
        private ScatterStageTimings _timings = ScatterStageTimings.NotMeasured;

        public ScatterSpikeSystem System => _system;

        public Camera CullCamera => _cullCamera;

        /// <summary>Timings of the last frame: spike CPU, cull GPU and URP pass GPU times (NaN = not measured).</summary>
        public ScatterStageTimings Timings => _timings;

        /// <summary>Requests the debug counters readback even when the HUD is hidden (benchmark).</summary>
        public bool ForceStatsReadback { get; set; }

        private void OnEnable()
        {
            if (_resources == null)
            {
                _resources = ScatterSpikeResources.Load();
            }

            if (_cullCamera == null)
            {
                _cullCamera = Camera.main;
            }

            if (_resources == null || _cullCamera == null)
            {
                Debug.LogError("ScatterSpike needs its resources asset and a camera.", this);
                enabled = false;
                return;
            }

            _bladeCountStep = Mathf.Clamp(_initialBladeCountStep, 0, _bladeCountSteps.Length - 1);
            float half = _areaSizeMeters * 0.5f;
            ScatterSpikeConfig config = ScatterSpikeConfig.Default;
            config.MaxBladeCount = _bladeCountSteps[_bladeCountSteps.Length - 1];
            config.BladeCount = _bladeCountSteps[_bladeCountStep];
            config.ObjectCount = _objectCount;
            config.AreaXZ = new Rect(-half, -half, _areaSizeMeters, _areaSizeMeters);
            config.Lod1DistanceMeters = _lod1DistanceMeters;
            config.MaxDistanceMeters = _maxDistanceMeters;
            config.BladeHeightMeters = _bladeHeightMeters;
            config.BladeWidthMeters = _bladeWidthMeters;
            config.BladeBend = _bladeBend;
            config.BladesPerInstance = _bladesPerInstance;
            config.BladeDrawType = _bladeDrawType;
            config.OffsetMode = _offsetMode;
            config.Backend = _backend;
            _system = new ScatterSpikeSystem(_resources, config);
            _passTimings = new UrpPassTimings();
            Shader.SetGlobalFloat(s_DebugView, 0f);
            UpdateStatus();
        }

        private void OnDisable()
        {
            _system?.Dispose();
            _system = null;
            _passTimings?.Dispose();
            _passTimings = null;
            Shader.SetGlobalFloat(s_DebugView, 0f);
            if (_hud != null)
            {
                _hud.StatusText = null;
                _hud.StageTimings = ScatterStageTimings.NotMeasured;
            }
        }

        private void OnValidate()
        {
            if (_bladeCountSteps == null || _bladeCountSteps.Length == 0)
            {
                _bladeCountSteps = new[] { 1 << 20 };
            }

            for (int i = 0; i < _bladeCountSteps.Length; i++)
            {
                _bladeCountSteps[i] = Mathf.Max(_bladeCountSteps[i], 1);
            }

            _maxDistanceMeters = Mathf.Max(_maxDistanceMeters, _lod1DistanceMeters);
        }

        private void LateUpdate()
        {
            // LateUpdate: after the camera moved this frame, before rendering.
            HandleKeys();
            bool hudVisible = _hud != null && _hud.isActiveAndEnabled && _hud.Visible;
            bool readStats = (hudVisible || ForceStatsReadback) && Time.unscaledTime >= _nextStatsTime;
            if (readStats)
            {
                _nextStatsTime = Time.unscaledTime + StatsRefreshSeconds;
            }

            _system.Tick(_cullCamera, readStats);
            _timings = ScatterStageTimings.NotMeasured;
            _timings.CpuMainMs = _system.TickCpuMs;
            _timings.CullGpuMs = _system.Config.ScatterEnabled ? _system.CullGpuMs : float.NaN;
            _passTimings.Fill(ref _timings);
            if (_hud != null)
            {
                _hud.StageTimings = _timings;
                if (readStats && hudVisible)
                {
                    UpdateStatus();
                }
            }
        }

        private void HandleKeys()
        {
#if SCATTER_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool changed = false;
            if (keyboard[_drawTypeKey].wasPressedThisFrame)
            {
                _system.SetBladeDrawType((SpikeDrawType)(((int)_system.Config.BladeDrawType + 1) % 3));
                changed = true;
            }

            if (keyboard[_offsetModeKey].wasPressedThisFrame)
            {
                _system.SetOffsetMode((SpikeRegionOffsetMode)(((int)_system.Config.OffsetMode + 1) % 3));
                changed = true;
            }

            if (keyboard[_bladeCountKey].wasPressedThisFrame)
            {
                _bladeCountStep = (_bladeCountStep + 1) % _bladeCountSteps.Length;
                _system.SetBladeCount(_bladeCountSteps[_bladeCountStep]);
                changed = true;
            }

            if (keyboard[_bladesPerInstanceKey].wasPressedThisFrame)
            {
                _system.SetBladesPerInstance(NextBladesPerInstance(_system.Config.BladesPerInstance));
                changed = true;
            }

            if (keyboard[_objectsKey].wasPressedThisFrame)
            {
                _system.SetObjectsEnabled(!_system.Config.ObjectsEnabled);
                changed = true;
            }

            if (keyboard[_backendKey].wasPressedThisFrame)
            {
                _system.SetBackend(_system.Config.Backend == SpikeBackend.Brg ? SpikeBackend.Indirect : SpikeBackend.Brg);
                changed = true;
            }

            if (keyboard[_debugViewKey].wasPressedThisFrame)
            {
                _debugView = (SpikeDebugView)(((int)_debugView + 1) % 6);
                Shader.SetGlobalFloat(s_DebugView, (float)_debugView);
                changed = true;
            }

            if (changed)
            {
                UpdateStatus();
            }
#endif
        }

        private static int NextBladesPerInstance(int current)
        {
            for (int i = 0; i < s_BladesPerInstanceSteps.Length; i++)
            {
                if (s_BladesPerInstanceSteps[i] > current)
                {
                    return s_BladesPerInstanceSteps[i];
                }
            }

            return s_BladesPerInstanceSteps[0];
        }

        /// <summary>Builds the HUD status text. Allocates a string: on settings change and at the stats rate only.</summary>
        private void UpdateStatus()
        {
            if (_hud == null || _system == null)
            {
                return;
            }

            ScatterSpikeConfig config = _system.Config;
            _status.Clear();
            _status.Append("Spike  [F9] backend ").Append(config.Backend);
            _status.Append("\n  [F3] blades ").Append(config.BladeDrawType);
            if (config.BladeDrawType == SpikeDrawType.ProceduralIndexedIndirect)
            {
                _status.Append("  [F6] K ").Append(config.BladesPerInstance);
            }

            if (config.Backend == SpikeBackend.Brg)
            {
                _status.Append("\n  [F4] offset ").Append(config.OffsetMode);
            }

            _status.Append("\n  [F5] blades ").Append(config.BladeCount);
            _status.Append("  [F7] objects ").Append(config.ObjectsEnabled ? config.ObjectCount : 0);
            _status.Append("  [F8] view ").Append(_debugView);
            _status.Append("\n  visible LOD0 ").Append(_system.GetCounter(ScatterSpikeSystem.BladeLod0Region));
            _status.Append("  LOD1 ").Append(_system.GetCounter(ScatterSpikeSystem.BladeLod1Region));
            _status.Append("  objects ").Append(_system.GetCounter(ScatterSpikeSystem.ObjectRegion));
            uint overflow = _system.GetCounter(_system.Culler.OverflowCounterIndex);
            _status.Append(overflow > 0 ? "\n  <color=#FF5050>overflow " : "\n  overflow ").Append(overflow);
            if (overflow > 0)
            {
                _status.Append("</color>");
            }

            _hud.StatusText = _status.ToString();
        }
    }
}
