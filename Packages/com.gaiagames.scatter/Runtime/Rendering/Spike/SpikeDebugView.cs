namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Debug views of the spike shaders (design 12.2), selected by the global shader value _ScatterDebugView.
    /// Mirrored in SpikePasses.hlsl.
    /// </summary>
    public enum SpikeDebugView
    {
        /// <summary>Normal shading.</summary>
        Off = 0,

        /// <summary>Colour per draw: red, green, blue for the three blade draw types, lighter for LOD1, rocks white.</summary>
        DrawColour = 1,

        /// <summary>Main light only: light colour x diffuse x distance attenuation x shadow, no albedo.</summary>
        MainLight = 2,

        /// <summary>Ambient only: SampleSH, no albedo.</summary>
        Ambient = 3,

        /// <summary>Main light shadow attenuation only (1 = lit).</summary>
        Shadow = 4,

        /// <summary>Diffuse term only: wrapped N.L with the normal of the side seen.</summary>
        Diffuse = 5,
    }
}
