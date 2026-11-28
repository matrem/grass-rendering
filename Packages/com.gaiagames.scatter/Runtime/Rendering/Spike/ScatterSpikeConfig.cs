using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Startup settings of <see cref="ScatterSpikeSystem"/> (Implementation Phase 1). Buffers are sized from
    /// the maximum counts; the active counts and the draw variant change at runtime.
    /// </summary>
    public struct ScatterSpikeConfig
    {
        public int MaxBladeCount;
        public int BladeCount;
        public int ObjectCount;
        public Rect AreaXZ;
        public float Lod1DistanceMeters;
        public float MaxDistanceMeters;
        public float BladeHeightMeters;
        public float BladeWidthMeters;
        public float BladeBend;
        public int BladesPerInstance;
        public SpikeDrawType BladeDrawType;
        public SpikeRegionOffsetMode OffsetMode;
        public SpikeBackend Backend;
        public bool ObjectsEnabled;

        /// <summary>Off: no cull and no draw (baseline of the measurements).</summary>
        public bool ScatterEnabled;

        /// <summary>Phase 1b: density falloff of the blades (rank below the density at their distance), with or without widening.</summary>
        public SpikeDensityFalloff DensityFalloff;

        public SpikeDensityCurve DensityCurve;

        /// <summary>Largest width factor of <see cref="SpikeDensityFalloff.OnWiden"/> (width x 1/sqrt(density), capped).</summary>
        public float WidenMaxFactor;

        /// <summary>Phase 1b: which blade LODs cast shadows, into which cascades (design 6.2).</summary>
        public SpikeShadowCasters ShadowCasters;

        public static ScatterSpikeConfig Default => new ScatterSpikeConfig
        {
            MaxBladeCount = 1 << 22,
            BladeCount = 1 << 20,
            ObjectCount = 1 << 14,
            AreaXZ = new Rect(-75f, -75f, 150f, 150f),
            Lod1DistanceMeters = 20f,
            MaxDistanceMeters = 120f,
            BladeHeightMeters = 0.45f,
            BladeWidthMeters = 0.04f,
            BladeBend = 0.25f,
            BladesPerInstance = 16,
            BladeDrawType = SpikeDrawType.MeshIndirect,
            OffsetMode = SpikeRegionOffsetMode.VisibleOffset,
            Backend = SpikeBackend.Brg,
            ObjectsEnabled = true,
            ScatterEnabled = true,
            DensityFalloff = SpikeDensityFalloff.Off,
            DensityCurve = SpikeDensityCurve.Default,
            WidenMaxFactor = 3f,
            ShadowCasters = SpikeShadowCasters.AllLodsAllCascades,
        };
    }
}
