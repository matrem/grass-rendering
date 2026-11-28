namespace Scatter.Rendering.Spike
{
    /// <summary>Phase 1b density falloff of the spike blades (design 5.2, 5.3), cycled with F11.</summary>
    public enum SpikeDensityFalloff
    {
        /// <summary>Every blade up to the maximum distance (Phase 1 behaviour).</summary>
        Off = 0,

        /// <summary>A blade is kept when its rank is below the density at its distance (<see cref="SpikeDensityCurve"/>).</summary>
        On = 1,

        /// <summary>As <see cref="On"/>, and the kept blades are widened by 1/sqrt(density), capped (design 10.1).</summary>
        OnWiden = 2,
    }
}
