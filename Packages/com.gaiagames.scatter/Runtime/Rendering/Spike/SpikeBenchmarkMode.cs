namespace Scatter.Rendering.Spike
{
    /// <summary>Runs of the spike benchmark, chosen by the command line flag.</summary>
    public enum SpikeBenchmarkMode
    {
        /// <summary><c>-spikeBenchmark</c> (Phase 1 S3): every backend x draw type x blade count, then the K sweep.</summary>
        Full = 0,

        /// <summary><c>-spikeBenchmarkQuick</c>: 1M blades, every backend x draw type, few frames (checks the tooling).</summary>
        Quick = 1,

        /// <summary><c>-spikeBenchmarkResolution</c> (Phase 1b item 1): render scale x shadow map resolution x blade count.</summary>
        Resolution = 2,

        /// <summary><c>-spikeBenchmarkLevers</c> (Phase 1b items 2 and 3): density falloff x widening x shadow caster preset.</summary>
        Levers = 3,
    }
}
