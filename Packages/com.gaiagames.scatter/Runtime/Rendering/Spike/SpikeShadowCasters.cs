namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1b presets of the blade shadow casters (design 6.2), cycled with F12. Cascades are the splits of the main
    /// light shadow view. The Object layer (rocks) always casts in every cascade. The Graphics.Render*Indirect backend
    /// cannot limit the cascades: it casts LOD0 (or LOD0 and LOD1) in every cascade.
    /// </summary>
    public enum SpikeShadowCasters
    {
        /// <summary>LOD0 and LOD1 in every cascade (Phase 1 behaviour).</summary>
        AllLodsAllCascades = 0,

        /// <summary>LOD0 only, in every cascade.</summary>
        Lod0AllCascades = 1,

        /// <summary>LOD0 only, in the first two cascades.</summary>
        Lod0FirstTwoCascades = 2,

        /// <summary>LOD0 only, in the first cascade.</summary>
        Lod0FirstCascade = 3,

        /// <summary>No blade casts a shadow.</summary>
        None = 4,
    }
}
