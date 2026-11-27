namespace Scatter
{
    /// <summary>
    /// Which blades of a layer cast shadows (design 7).
    /// </summary>
    public enum ScatterShadowMode
    {
        /// <summary>No shadow casting.</summary>
        Off = 0,

        /// <summary>Mesh LOD0 only, submitted to the first shadow cascades (design 6.2).</summary>
        Near = 1,

        /// <summary>Every mesh LOD.</summary>
        All = 2,
    }
}
